using TMPro;
using UnityEngine;

/// <summary>
/// In-headset debug text for OpenEye TCP + gaze ray status.
/// Add to OpenEyeGaze. Leave Text empty to auto-create a HUD on the VR camera.
/// </summary>
public class OpenEyeGazeDebugHud : MonoBehaviour
{
    [Header("Sources (auto-found if empty)")]
    public OpenEyeGazeReceiver receiver;
    public EyeGazeProvider gazeProvider;

    [Header("UI")]
    public TextMeshProUGUI text;
    [SerializeField] bool autoCreateHud = true;
    [SerializeField] float refreshHz = 5f;

    [Header("Auto HUD layout (world space, in front of camera)")]
    [SerializeField] Vector3 hudLocalPosition = new Vector3(0f, -0.1f, 0.45f);
    [SerializeField] Vector2 hudSize = new Vector2(600f, 260f);
    [SerializeField] float hudFontSize = 28f;

    float _nextRefresh;
    float _nextHudRetry;
    OpenEyeGazeReceiver.State _lastLoggedState = OpenEyeGazeReceiver.State.Disconnected;

    void Start()
    {
        if (receiver == null)
            receiver = FindObjectOfType<OpenEyeGazeReceiver>();
        if (gazeProvider == null)
            gazeProvider = FindObjectOfType<EyeGazeProvider>();

        TryCreateHud();
    }

    void LateUpdate()
    {
        if (text == null)
        {
            TryCreateHud();
            return;
        }

        if (receiver != null && receiver.CurrentState != _lastLoggedState)
        {
            Debug.Log($"[OpenEye HUD] TCP state = {receiver.CurrentState}");
            _lastLoggedState = receiver.CurrentState;
        }

        if (Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + (refreshHz > 0f ? 1f / refreshHz : 0.2f);

        text.text = BuildStatusText();
    }

    void TryCreateHud()
    {
        if (!autoCreateHud || text != null)
            return;
        if (Time.unscaledTime < _nextHudRetry)
            return;

        _nextHudRetry = Time.unscaledTime + 0.5f;
        text = CreateHudText();
    }

    string BuildStatusText()
    {
        if (receiver == null)
            return "OpenEye debug\nreceiver: MISSING";

        string tcp = $"TCP: {receiver.CurrentState}\n" +
                     $"PC: {receiver.serverIp}:{receiver.serverPort}";

        string gaze;
        if (gazeProvider == null)
        {
            gaze = "gaze: provider MISSING";
        }
        else if (gazeProvider.gazeSource != EyeGazeProvider.GazeSource.OpenEye)
        {
            gaze = $"gaze: source = {gazeProvider.gazeSource} (not OpenEye)";
        }
        else if (!receiver.HasGaze)
        {
            gaze = "gaze: waiting for packets\n(PC: Start Gaze Tracking + Visualize)";
        }
        else if (!gazeProvider.HasValidGaze)
        {
            gaze = $"gaze: packets={receiver.GazeSequence}\nray: not ready";
        }
        else
        {
            Vector2 p = gazeProvider.LastPlaneMeters;
            Vector3 f = gazeProvider.eyeforward;
            Vector3 o = gazeProvider.eyeposition;
            string refName = gazeProvider.OpenEyeReferenceTransform != null
                ? gazeProvider.OpenEyeReferenceTransform.name
                : "?";
            gaze = $"gaze: packets={receiver.GazeSequence}\n" +
                   $"plane xy=({p.x:F3}, {p.y:F3}) m\n" +
                   $"ref={refName}\n" +
                   $"origin=({o.x:F2},{o.y:F2},{o.z:F2})\n" +
                   $"ray fwd=({f.x:F2},{f.y:F2},{f.z:F2})\n" +
                   $"valid=YES";
        }

        return $"OpenEye debug\n{tcp}\n{gaze}";
    }

    static Camera FindVrCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        var centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            var cam = centerEye.GetComponent<Camera>();
            if (cam != null)
                return cam;
        }

        foreach (var cam in Camera.allCameras)
        {
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                return cam;
        }

        return null;
    }

    TextMeshProUGUI CreateHudText()
    {
        var cam = FindVrCamera();
        if (cam == null)
        {
            Debug.LogWarning("[OpenEye HUD] VR camera not ready yet; retrying...");
            return null;
        }

        var canvasGo = new GameObject("OpenEyeDebugCanvas");
        canvasGo.transform.SetParent(cam.transform, false);
        canvasGo.transform.localPosition = hudLocalPosition;
        canvasGo.transform.localRotation = Quaternion.identity;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = hudSize;
        rt.localScale = Vector3.one * 0.0012f;

        var textGo = new GameObject("OpenEyeDebugText");
        textGo.transform.SetParent(canvasGo.transform, false);

        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.fontSize = hudFontSize;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = Color.white;
        tmp.text = "OpenEye debug\ninitializing...";
        tmp.raycastTarget = false;

        Debug.Log($"[OpenEye HUD] created on camera: {cam.name}");
        return tmp;
    }
}
