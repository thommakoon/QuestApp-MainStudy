using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// TCP client for the OpenEye PC GUI (Neon + mapping). Receives mapped gaze on a z=1m plane.
/// PC runs the server on port 5051; Quest connects as client.
/// Clock sync: Neon-style timeEcho round-trip (driven by OpenEye PC).
/// </summary>
public class OpenEyeGazeReceiver : MonoBehaviour
{
    [Header("PC running openeye-quest-gui")]
    public string serverIp = "192.168.0.50";
    public int serverPort = 5051;

    [Header("Connection")]
    [SerializeField] bool autoConnectOnStart = true;
    [SerializeField] bool autoReconnect = true;
    [SerializeField] float reconnectIntervalSec = 2f;

    public enum State { Disconnected, Connecting, Connected, Failed }
    public State CurrentState { get; private set; } = State.Disconnected;

    public struct GazeSample
    {
        /// <summary>Neon / OpenEye timestamp in unix seconds (payload.t).</summary>
        public double timestamp;
        /// <summary>Unix epoch ns from Neon (same units as gaze.csv). 0 if absent.</summary>
        public long timestampNs;
        /// <summary>Quest wall-clock unix ms when this TCP packet was received.</summary>
        public long questReceivedUnixMs;
        public Vector2 planeMeters;
        public bool valid;
    }

    [Serializable] class MsgTypeOnly { public string type; }
    [Serializable] class PayloadGaze { public double t; public long t_ns; public float x; public float y; }
    [Serializable] class PayloadLaunch { public string package; }
    [Serializable] class PayloadTimeEcho { public long pc_t1_ms; public long quest_tH_ms; }
    [Serializable] class PayloadMainStudyStart
    {
        public int sub_num;
        public int subsub_num;
        public string condition;
        public int reps;
        public int duration_sec;
    }
    [Serializable] class PayloadMainStudyDone
    {
        public bool ok;
        public bool stopped;
        public int sub_num;
        public int subsub_num;
        public string condition;
        public int reps;
    }
    [Serializable] class PayloadShowRay { public bool visible; }
    [Serializable] class MsgGaze { public string type; public PayloadGaze payload; }
    [Serializable] class MsgLaunch { public string type; public PayloadLaunch payload; }
    [Serializable] class MsgTimeEcho { public string type; public PayloadTimeEcho payload; }
    [Serializable] class MsgMainStudyStart { public string type; public PayloadMainStudyStart payload; }
    [Serializable] class MsgMainStudyDone { public string type; public PayloadMainStudyDone payload; }
    [Serializable] class MsgShowRay { public string type; public PayloadShowRay payload; }

    public event Action<string> OnLaunchApp;

    volatile bool _pendingLaunchApp;
    volatile string _pendingLaunchPackage = "";
    volatile bool _pendingMainStudyStart;
    volatile bool _pendingMainStudyStop;
    volatile bool _pendingShowRay;
    volatile bool _pendingShowRayVisible = true;
    volatile int _pendingMsSub;
    volatile int _pendingMsSubsub;
    volatile int _pendingMsReps;
    volatile int _pendingMsDurationSec;
    volatile string _pendingMsCondition = "";

    TcpClient _client;
    NetworkStream _stream;
    Thread _recvThread;
    CancellationTokenSource _cts;
    readonly object _sendLock = new object();

    static readonly DateTime UnixEpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    const int GazeBufCap = 8;
    readonly object _gazeLock = new object();
    readonly GazeSample[] _gazeBuf = new GazeSample[GazeBufCap];
    int _gazeHead;
    long _gazeSeq;

    public long GazeSequence => _gazeSeq;
    public bool HasGaze => _gazeSeq > 0;

    bool _didStartupBounce;
    float _connectedAt = -1f;
    long _gazeSeqWhenConnected;

    void Start()
    {
        if (GetComponent<OpenEyeHandoff>() == null)
            gameObject.AddComponent<OpenEyeHandoff>();

        if (autoConnectOnStart)
            _ = StartConnectLoop();

        StartCoroutine(StartupTcpBounce());
    }

    System.Collections.IEnumerator StartupTcpBounce()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (!Application.isPlaying || _didStartupBounce)
            yield break;
        _didStartupBounce = true;
        Debug.Log("[OpenEye] startup TCP bounce (heal Intent-launch dead socket)");
        Disconnect();
        yield return new WaitForSecondsRealtime(0.35f);
        Connect();
    }

    System.Collections.IEnumerator GazeWatchdogBounce()
    {
        Disconnect();
        yield return new WaitForSecondsRealtime(0.4f);
        Connect();
    }

    void Update()
    {
        if (_pendingLaunchApp)
        {
            _pendingLaunchApp = false;
            string package = _pendingLaunchPackage ?? "";
            OnLaunchApp?.Invoke(package);
        }

        if (_pendingMainStudyStart)
        {
            _pendingMainStudyStart = false;
            string cond = _pendingMsCondition ?? "";
            int sub = _pendingMsSub;
            int subsub = _pendingMsSubsub;
            int reps = _pendingMsReps;
            int dur = _pendingMsDurationSec > 0 ? _pendingMsDurationSec : 300;
            if (GameManager.instance != null)
            {
                bool ok = GameManager.instance.TryStartFromPc(sub, subsub, cond, reps, dur);
                Debug.Log($"[OpenEye] mainStudyStart applied ok={ok} {sub}-{subsub} {cond} x{reps} {dur}s");
            }
            else
            {
                Debug.LogWarning("[OpenEye] mainStudyStart: GameManager missing");
            }
        }

        if (_pendingMainStudyStop)
        {
            _pendingMainStudyStop = false;
            _pendingMainStudyStart = false; // cancel a start that arrived same frame
            if (GameManager.instance != null)
            {
                GameManager.instance.AbortToIdleFromPc();
                Debug.Log("[OpenEye] mainStudyStop → AbortToIdleFromPc");
            }
            else
            {
                Debug.LogWarning("[OpenEye] mainStudyStop: GameManager missing");
            }
        }

        if (_pendingShowRay)
        {
            _pendingShowRay = false;
            GazeRayVisualizer.SetRaysVisible(_pendingShowRayVisible);
        }

        if (CurrentState == State.Connected && _connectedAt > 0f
            && Time.unscaledTime - _connectedAt > 2.5f
            && GazeSequence == _gazeSeqWhenConnected)
        {
            Debug.LogWarning("[OpenEye] connected but no gazeVisual — forcing reconnect");
            _connectedAt = Time.unscaledTime;
            _gazeSeqWhenConnected = GazeSequence;
            StartCoroutine(GazeWatchdogBounce());
        }
    }

    void OnDestroy() => Disconnect();

    void OnApplicationQuit() => Disconnect();

    async Task StartConnectLoop()
    {
        while (autoReconnect && Application.isPlaying)
        {
            if (CurrentState == State.Disconnected || CurrentState == State.Failed)
                Connect();

            try { await Task.Delay(TimeSpan.FromSeconds(reconnectIntervalSec)); }
            catch { }
        }
    }

    public void Connect()
    {
        if (CurrentState == State.Connecting || CurrentState == State.Connected)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = ConnectAsync(_cts.Token);
    }

    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }
        try { _recvThread?.Join(100); } catch { }
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        SetState(State.Disconnected);
    }

    async Task ConnectAsync(CancellationToken token)
    {
        SetState(State.Connecting);
        try
        {
            _client = new TcpClient { NoDelay = true };
            using (token.Register(() => { try { _client?.Close(); } catch { } }))
            {
                await _client.ConnectAsync(serverIp, serverPort);
            }

            if (!_client.Connected)
                throw new Exception("connect failed");

            _stream = _client.GetStream();
            SetState(State.Connected);
            _recvThread = new Thread(() => ReceiveLoop(token)) { IsBackground = true };
            _recvThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenEye] connect error: {e.Message}");
            SetState(State.Failed);
        }
    }

    void ReceiveLoop(CancellationToken token)
    {
        var headerBuf = new byte[4];
        try
        {
            while (!token.IsCancellationRequested)
            {
                ReadExact(_stream, headerBuf, 0, 4);
                int len = (headerBuf[0] << 24) | (headerBuf[1] << 16) | (headerBuf[2] << 8) | headerBuf[3];
                if (len <= 0 || len > 10_000_000)
                    throw new Exception($"invalid length: {len}");

                var payload = new byte[len];
                ReadExact(_stream, payload, 0, len);
                string json = Encoding.UTF8.GetString(payload);

                var head = JsonUtility.FromJson<MsgTypeOnly>(json);
                if (head == null || string.IsNullOrEmpty(head.type))
                    continue;

                if (head.type == "launchApp")
                {
                    var msg = JsonUtility.FromJson<MsgLaunch>(json);
                    _pendingLaunchPackage = msg?.payload?.package ?? "";
                    _pendingLaunchApp = true;
                    continue;
                }

                // Neon Time Echo analogue: PC sends t1 → Quest replies t1 + tH ASAP.
                if (head.type == "timeEcho")
                {
                    var echo = JsonUtility.FromJson<MsgTimeEcho>(json);
                    long t1 = echo?.payload != null ? echo.payload.pc_t1_ms : 0L;
                    long tH = (long)(DateTime.UtcNow - UnixEpochUtc).TotalMilliseconds;
                    var reply = new MsgTimeEcho
                    {
                        type = "timeEcho",
                        payload = new PayloadTimeEcho
                        {
                            pc_t1_ms = t1,
                            quest_tH_ms = tH,
                        },
                    };
                    TrySendJson(JsonUtility.ToJson(reply));
                    continue;
                }

                if (head.type == "mainStudyStart")
                {
                    var msg = JsonUtility.FromJson<MsgMainStudyStart>(json);
                    if (msg?.payload != null)
                    {
                        _pendingMsSub = msg.payload.sub_num;
                        _pendingMsSubsub = msg.payload.subsub_num;
                        _pendingMsReps = msg.payload.reps > 0 ? msg.payload.reps : 3;
                        _pendingMsDurationSec = msg.payload.duration_sec > 0 ? msg.payload.duration_sec : 300;
                        _pendingMsCondition = msg.payload.condition ?? "";
                        _pendingMainStudyStart = true;
                    }
                    continue;
                }

                if (head.type == "mainStudyStop")
                {
                    _pendingMainStudyStop = true;
                    continue;
                }

                if (head.type == "showRay")
                {
                    var msg = JsonUtility.FromJson<MsgShowRay>(json);
                    _pendingShowRayVisible = msg?.payload == null || msg.payload.visible;
                    _pendingShowRay = true;
                    continue;
                }

                if (head.type != "gazeVisual")
                    continue;

                var gazeMsg = JsonUtility.FromJson<MsgGaze>(json);
                if (gazeMsg?.payload == null)
                    continue;

                long questMs = (long)(DateTime.UtcNow - UnixEpochUtc).TotalMilliseconds;
                EnqueueGaze(new GazeSample
                {
                    timestamp = gazeMsg.payload.t,
                    timestampNs = gazeMsg.payload.t_ns,
                    questReceivedUnixMs = questMs,
                    planeMeters = new Vector2(gazeMsg.payload.x, gazeMsg.payload.y),
                    valid = true,
                });
            }
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                Debug.LogWarning($"[OpenEye] recv ended: {e.Message}");
        }

        Disconnect();
    }

    static void ReadExact(NetworkStream stream, byte[] buf, int off, int len)
    {
        int got = 0;
        while (got < len)
        {
            int r = stream.Read(buf, off + got, len - got);
            if (r <= 0)
                throw new Exception("socket closed");
            got += r;
        }
    }

    void EnqueueGaze(GazeSample sample)
    {
        lock (_gazeLock)
        {
            _gazeBuf[_gazeHead] = sample;
            _gazeHead = (_gazeHead + 1) % GazeBufCap;
            _gazeSeq++;
        }
    }

    public bool TrySendMainStudyDone(bool ok, int subNum, int subsubNum, string condition, int reps)
    {
        return TrySendMainStudyDone(ok, subNum, subsubNum, condition, reps, stopped: false);
    }

    public bool TrySendMainStudyDone(bool ok, int subNum, int subsubNum, string condition, int reps, bool stopped)
    {
        if (_stream == null || CurrentState != State.Connected)
            return false;

        // JsonUtility cannot serialize arbitrary extra fields on nested class easily
        // without adding the field — use stopped on payload.
        var msg = new MsgMainStudyDone
        {
            type = "mainStudyDone",
            payload = new PayloadMainStudyDone
            {
                ok = ok,
                stopped = stopped,
                sub_num = subNum,
                subsub_num = subsubNum,
                condition = condition ?? "",
                reps = reps,
            },
        };
        return TrySendJson(JsonUtility.ToJson(msg));
    }

    bool TrySendJson(string json)
    {
        if (_stream == null)
            return false;

        try
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            if (payload.Length <= 0 || payload.Length > 10_000_000)
                return false;

            byte[] header = new byte[4];
            int len = payload.Length;
            header[0] = (byte)((len >> 24) & 0xFF);
            header[1] = (byte)((len >> 16) & 0xFF);
            header[2] = (byte)((len >> 8) & 0xFF);
            header[3] = (byte)(len & 0xFF);

            lock (_sendLock)
            {
                _stream.Write(header, 0, 4);
                _stream.Write(payload, 0, payload.Length);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OpenEye] send error: {e.Message}");
            return false;
        }
    }

    public bool TryGetLatestGaze(ref long lastSeenSeq, out GazeSample sample)
    {
        lock (_gazeLock)
        {
            if (_gazeSeq == lastSeenSeq)
            {
                sample = default;
                return false;
            }

            int latestIdx = (_gazeHead - 1 + GazeBufCap) % GazeBufCap;
            sample = _gazeBuf[latestIdx];
            lastSeenSeq = _gazeSeq;
            return sample.valid;
        }
    }

    void SetState(State state)
    {
        CurrentState = state;
        Debug.Log($"[OpenEye] state = {state}");
        if (state == State.Connected)
        {
            _connectedAt = Time.unscaledTime;
            _gazeSeqWhenConnected = GazeSequence;
            SendSessionHello();
        }
        else if (state == State.Disconnected || state == State.Failed)
        {
            _connectedAt = -1f;
        }
    }

    void SendSessionHello()
    {
        string scene = "IDLE";
        if (GameManager.instance != null)
            scene = GameManager.instance.current_scene.ToString();

        string pkg = Application.identifier;
        string json =
            "{\"type\":\"sessionHello\",\"payload\":{\"package\":\"" + pkg +
            "\",\"scene\":\"" + scene + "\"}}";
        if (TrySendJson(json))
            Debug.Log($"[OpenEye] sessionHello package={pkg} scene={scene}");
    }
}
