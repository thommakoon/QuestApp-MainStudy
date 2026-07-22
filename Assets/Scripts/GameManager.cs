using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StudyDesign;
using System;


public class GameManager : MonoBehaviour
{
    public enum SCENE { IDLE, PREP, PRACTICE, BEFORE_TRIAL, TRIAL, AFTER_TRIAL, BREAK, END, FINISHED }
    public SCENE current_scene = SCENE.IDLE;
    public static GameManager instance;
    public TextMeshPro debugText;
    public TextMeshPro InfoText;
    // Start is called before the first frame update

    public ControlTargets targetControl;

    public CursorController cursorController;
    public StudyDesign.Study study;
    public Transform head;


    [SerializeField] private TrialData<HandCursorData> data_to_save_hand;
    [SerializeField] private TrialData<HeadCursorData> data_to_save_head;
    [SerializeField] private TrialData<EyeCursorData> data_to_save_eye;

    public float prep_progress = 0f;
    public float after_progress = 0f;
    public float break_progress = 0f;
    private float end_progress = 0.0f;
    const float TIME_AFTER = 10.0f;
    private const float bound = 15.0f;
    private float TIME_END = 3.0f;
    float TIME_BREAK = 3f;
    public int maxMsg = 5;
    private Queue<string> msg = new Queue<string>();


    //StudyDesign.FittsLaw fittsLaw;
    [SerializeField]
    public bool Tapped;
    public AudioSource audioSource_success;
    public AudioSource audioSource_fail;
    private float defaultDistance = 2.0f;
    private float MINIMUM_VELOCITY = 1.0f;

    [Header("Trial logging")]
    [SerializeField] float trialLogRateHz = 100f;
    float _trialLogAccum;
    int _trialLogSeq;

    bool _returnToIdleAfterTrial;
    string _lastCommandedCondition = "";
    int _lastCommandedReps;
    float _trialElapsedSec;
    bool _endingTimedTrial;

    float TrialLogIntervalSec => trialLogRateHz > 0f ? 1f / trialLogRateHz : 0f;
    public void makeSound(bool success)
    {
        if (success)
        {
            if (audioSource_success == null)
            {
                Debug.Log("no audio source detected");
            }
            else
            {
                audioSource_success.Play();
            }
        }
        else
        {
            audioSource_fail.Play();
        }
    }
    private void Awake()
    {
        instance = this;
    }
    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }
    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }
    private void Start()
    {

        //head = Microsoft.MixedReality.Toolkit.Utilities.CameraCache.Main.transform; //TODO

        Camera mainCamera = Camera.main;
        head = mainCamera.transform;
        targetControl = GameObject.Find("TargetPlane").GetComponent<ControlTargets>();
        study = null;
        _returnToIdleAfterTrial = false;
        current_scene = SCENE.IDLE;
        Debug.Log("[MainStudy] IDLE — waiting for PC mainStudyStart");
    }

    private void Update()
    {
        SceneUpdate();
        debugText.transform.localPosition = new Vector3(debugText.transform.localPosition.x, debugText.transform.localPosition.y, 2f);
        InfoText.transform.localPosition = new Vector3(InfoText.transform.localPosition.x, InfoText.transform.localPosition.y, 2f);
    }

    /// <summary>Called from OpenEyeGazeReceiver (main thread) when PC sends mainStudyStart.</summary>
    public bool TryStartFromPc(int subNum, int subsubNum, string conditionName, int reps, float durationSec = 300f)
    {
        if (current_scene != SCENE.IDLE)
        {
            Debug.LogWarning($"[MainStudy] ignore start — not IDLE (scene={current_scene})");
            return false;
        }

        if (!Enum.TryParse(conditionName, ignoreCase: true, out Study.ConditionType condition))
        {
            Debug.LogWarning($"[MainStudy] unknown condition '{conditionName}'");
            return false;
        }

        int safeReps = Mathf.Max(1, reps);
        float safeDur = Mathf.Max(1f, durationSec);
        study = Study.FromPcCommand(subNum, subsubNum, condition, safeReps, safeDur);
        _lastCommandedCondition = condition.ToString();
        _lastCommandedReps = safeReps;
        _returnToIdleAfterTrial = false;
        _endingTimedTrial = false;
        _trialElapsedSec = 0f;
        SceneChange(SCENE.BEFORE_TRIAL);
        Debug.Log($"[MainStudy] started {_lastCommandedCondition} x{_lastCommandedReps} {safeDur}s each for {subNum}-{subsubNum}");
        return true;
    }

    public bool TimedTrialExpired()
    {
        if (study == null || study.trialDurationSec <= 0f)
            return true;
        return _trialElapsedSec >= study.trialDurationSec;
    }

    public void EndTimedTrialIfNeeded()
    {
        if (_endingTimedTrial || current_scene != SCENE.TRIAL || study == null)
            return;
        if (!study.commandedMode || study.trialDurationSec <= 0f)
            return;
        if (!TimedTrialExpired())
            return;

        _endingTimedTrial = true;
        Debug.Log($"[MainStudy] timed trial ended at {_trialElapsedSec:F1}s / {study.trialDurationSec:F0}s");
        study.fittsLaw.finish();
        SceneChange(SCENE.AFTER_TRIAL);
        study.NextStep();
    }

    public void MarkCommandedConditionComplete()
    {
        _returnToIdleAfterTrial = true;
    }

    /// <summary>PC Stop: abort current condition, optionally save mid-trial data, return IDLE.</summary>
    public void AbortToIdleFromPc()
    {
        Debug.Log($"[MainStudy] abort → IDLE (was {current_scene})");
        _endingTimedTrial = false;
        _returnToIdleAfterTrial = false;
        after_progress = 0f;
        break_progress = 0f;

        if (current_scene == SCENE.TRIAL && study != null)
        {
            // Save partial trial JSON if we were logging.
            try
            {
                switch (study.currentCursor)
                {
                    case Study.CursorType.Eye:
                        data_to_save_eye?.SaveDataJson();
                        break;
                    case Study.CursorType.Head:
                        data_to_save_head?.SaveDataJson();
                        break;
                    case Study.CursorType.Hand:
                        data_to_save_hand?.SaveDataJson();
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MainStudy] abort save failed: {e.Message}");
            }
            study.fittsLaw?.finish();
        }

        targetControl.ShowDwellTarget(false);
        targetControl.ShowTargets(false);
        targetControl.ShowMenuTargets(false);

        NotifyPcConditionDone(ok: false, stopped: true);
        SceneChange(SCENE.IDLE);
    }

    void NotifyPcConditionDone(bool ok, bool stopped = false)
    {
        var receiver = FindObjectOfType<OpenEyeGazeReceiver>();
        if (receiver == null)
            return;
        int sub = study != null ? study.sub_num : 0;
        int subsub = study != null ? study.subsub_num : 0;
        receiver.TrySendMainStudyDone(
            ok: ok,
            subNum: sub,
            subsubNum: subsub,
            condition: _lastCommandedCondition ?? "",
            reps: _lastCommandedReps,
            stopped: stopped);
    }

    void HandleLog(string message, string stackTrace, LogType type)
    {
        if (msg.Count > maxMsg)
        {
            msg.Dequeue();
        }
        msg.Enqueue(message);
        debugText.text = FromQueueToString();
    }

    string FromQueueToString()
    {
        return string.Join("\n", msg.ToArray());
    }
    public void SceneChange(SCENE scene)
    {
        current_scene = scene;
        switch (scene)
        {

            case SCENE.IDLE:
                targetControl.ShowDwellTarget(false);
                targetControl.ShowTargets(false);
                targetControl.ShowMenuTargets(false);
                break;
            case SCENE.PREP:
                break;
            case SCENE.BEFORE_TRIAL:
                // Main study is Fitts-only; Menu layout path unused.
                break;
            case SCENE.TRIAL:
                switch (study.currentCursor)
                {
                    case Study.CursorType.Eye:
                        data_to_save_eye = new TrialData<EyeCursorData>(study);
                        data_to_save_eye.log_sample_rate_hz = trialLogRateHz;
                        break;
                    case Study.CursorType.Head:
                        data_to_save_head = new TrialData<HeadCursorData>(study);
                        data_to_save_head.log_sample_rate_hz = trialLogRateHz;
                        break;
                    case Study.CursorType.Hand:
                        data_to_save_hand = new TrialData<HandCursorData>(study);
                        data_to_save_hand.log_sample_rate_hz = trialLogRateHz;
                        break;
                }
                _trialLogAccum = 0f;
                _trialLogSeq = 0;
                _trialElapsedSec = 0f;
                _endingTimedTrial = false;
                if (study.fittsLaw != null && !study.fittsLaw.menu)
                {
                    study.fittsLaw.ClearSelectionLogs();
                    study.fittsLaw.MarkSelectionStart();
                }
                if (targetControl != null)
                    targetControl.ResetRingSequence();
                break;

            case SCENE.AFTER_TRIAL:
                switch (study.currentCursor)
                {
                    case Study.CursorType.Eye:
                        data_to_save_eye.SaveDataJson();
                        break;
                    case Study.CursorType.Head:
                        data_to_save_head.SaveDataJson();
                        break;
                    case Study.CursorType.Hand:
                        data_to_save_hand.SaveDataJson();
                        break;
                }
                after_progress = 0.0f;
                break;

            case SCENE.BREAK:
                break_progress = 0.0f;
                break;

            case SCENE.END:
                break;

            case SCENE.FINISHED:
                break;
        }
    }
    public void SceneUpdate()
    {
        switch (current_scene)
        {
            case SCENE.IDLE:
                InfoText.text = "IDLE — waiting for PC Main Study commander\n"
                    + "(OpenEye: set sub / condition / reps → Start condition)";
                targetControl.ShowDwellTarget(false);
                targetControl.ShowTargets(false);
                targetControl.ShowMenuTargets(false);
                break;

            case SCENE.PREP:
                if (study == null)
                {
                    InfoText.text = "Initilizing System... ";
                    targetControl.ShowDwellTarget(false);
                }
                else
                {
                    InfoText.text = "Thank you for participating this study.\nPlease select the start button to begin\n" + "current condition is " + study.currentCursor + " :   " + study.currentSelection;
                    targetControl.ShowDwellTarget(true);
                }
                targetControl.ShowTargets(false);
                targetControl.ShowMenuTargets(false);
                prep_progress += Time.deltaTime;
                break;

            case SCENE.BEFORE_TRIAL:

                {
                    string durHint = study.trialDurationSec > 0f
                        ? $"\n{study.trialDurationSec / 60f:0.#} min Fitts each"
                        : "";
                    InfoText.text = "current condition is " + study.currentCursor + " : " + study.currentSelection
                        + "\nrep " + study.currentRep + "/" + study.TOTAL_REP
                        + durHint
                        + "\nSelect center to start Fitts";
                }
                targetControl.ShowTargets(false);
                targetControl.ShowMenuTargets(false);
                targetControl.ShowDwellTarget(true);
                break;

            case SCENE.TRIAL:

                if (study.fittsLaw.menu)
                {
                    targetControl.ShowTargets(false);
                    targetControl.ShowMenuTargets(true);
                }
                else
                {
                    targetControl.ShowTargets(true);
                    targetControl.ShowMenuTargets(false);
                }

                targetControl.ShowDwellTarget(false);

                study.fittsLaw.current_elapsed_time += Time.deltaTime;
                if (study.commandedMode && study.trialDurationSec > 0f)
                {
                    _trialElapsedSec += Time.deltaTime;
                    float remain = Mathf.Max(0f, study.trialDurationSec - _trialElapsedSec);
                    int remainSec = Mathf.CeilToInt(remain);
                    InfoText.text = $"{study.currentCondition}  rep {study.currentRep + 1}/{study.TOTAL_REP}\n"
                        + $"time left {remainSec / 60:00}:{remainSec % 60:00}";
                    EndTimedTrialIfNeeded();
                    if (current_scene != SCENE.TRIAL)
                        break;
                }
                else
                {
                    InfoText.text = "";
                }

                _trialLogAccum += Time.deltaTime;
                float logInterval = TrialLogIntervalSec;
                if (logInterval > 0f)
                {
                    int samplesDue = 0;
                    while (_trialLogAccum >= logInterval && samplesDue < 8)
                    {
                        RecordTrialFrame();
                        _trialLogAccum -= logInterval;
                        samplesDue++;
                    }
                }

                if (study.fittsLaw.check_timeout() && study.fittsLaw.onGoing == false)
                {
                    study.fittsLaw.nextStep(success: false, eventType: "timeout");
                }
                break;

            case SCENE.AFTER_TRIAL:
                after_progress += Time.deltaTime;
                targetControl.ShowDwellTarget(false);
                targetControl.ShowTargets(false);
                targetControl.ShowMenuTargets(false);
                if (after_progress >= 3.0f)
                {
                    if (_returnToIdleAfterTrial)
                    {
                        _returnToIdleAfterTrial = false;
                        NotifyPcConditionDone(ok: true);
                        SceneChange(SCENE.IDLE);
                    }
                    else
                    {
                        SceneChange(SCENE.BEFORE_TRIAL);
                    }
                }
                break;

            case SCENE.BREAK:
                InfoText.text = "This session was finished. \nPlease take off the headset and take a rest.\n" +
    "Break time remaining :" + (int)System.Math.Round(break_progress) + "/" + TIME_BREAK;


                targetControl.ShowTargets(false);
                targetControl.ShowDwellTarget(false);
                targetControl.ShowMenuTargets(false);

                break_progress += Time.deltaTime;
                if (break_progress >= TIME_BREAK)
                {
                    SceneChange(SCENE.BEFORE_TRIAL);
                }
                break;

            case SCENE.END:
                targetControl.ShowDwellTarget(false);
                targetControl.ShowMenuTargets(false);
                targetControl.ShowTargets(false);
                InfoText.text = "Whole study was finished. \nPlease take off the headset.\nThank you for participating the study.";
                end_progress += Time.deltaTime;
                if (end_progress >= TIME_END)
                {
                    SceneChange(SCENE.FINISHED);
                }
                break;

            case SCENE.FINISHED:
                break;
        }
    }

    void RecordTrialFrame()
    {
        float currentTime = Time.realtimeSinceStartup;
        long unixTimeMilliseconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        string formattedTime = DateTime.Now.ToString("yyyy MM dd HH mm ss fff");
        Transform endTargetTf = targetControl.getOneTarget(study.fittsLaw.endNum);
        Vector3 currentTargetPosition = endTargetTf.position;
        TargetBehaviour targetBehaviour = endTargetTf.GetComponent<TargetBehaviour>();
        float currentDwellTime = targetBehaviour != null ? targetBehaviour.currentDwellTime : 0f;

        switch (study.currentCursor)
        {
            case Study.CursorType.Eye:
            {
                var gp = cursorController.gazeProvider;
                Ray eyeRay = new Ray(cursorController.eyePosition, cursorController.eyeDirection);
                EyeCursorData currentEyeCursor = new EyeCursorData(
                    eyeRay,
                    neonGazeT: gp != null ? gp.LastNeonTimestampSec : double.NaN,
                    neonGazeTNs: gp != null ? gp.LastNeonTimestampNs : 0,
                    questGazeReceivedUnixMs: gp != null ? gp.LastQuestReceivedUnixMs : 0);
                float eye_angularDistance = Vector3.Angle((currentTargetPosition - cursorController.eyePosition), cursorController.eyeDirection);
                data_to_save_eye.Add(new FrameData<EyeCursorData>(
                    currentTime, unixTimeMilliseconds, formattedTime, study.fittsLaw, head, currentEyeCursor,
                    currentTargetPosition, eye_angularDistance, RayHitName(eyeRay), currentDwellTime, _trialLogSeq));
                break;
            }
            case Study.CursorType.Head:
            {
                Ray headRay = new Ray(cursorController.headPosition, cursorController.headDirection);
                HeadCursorData currentHeadCursor = new HeadCursorData(headRay);
                float head_angularDistance = Vector3.Angle((currentTargetPosition - currentHeadCursor.origin), currentHeadCursor.direction);
                data_to_save_head.Add(new FrameData<HeadCursorData>(
                    currentTime, unixTimeMilliseconds, formattedTime, study.fittsLaw, head, currentHeadCursor,
                    currentTargetPosition, head_angularDistance, RayHitName(headRay), currentDwellTime, _trialLogSeq));
                break;
            }
            case Study.CursorType.Hand:
            {
                HandCursorData currentHandCursor = new HandCursorData(cursorController.Handray);
                float hand_angularDistance = Vector3.Angle((currentTargetPosition - currentHandCursor.origin), currentHandCursor.direction);
                data_to_save_hand.Add(new FrameData<HandCursorData>(
                    currentTime, unixTimeMilliseconds, formattedTime, study.fittsLaw, head, currentHandCursor,
                    currentTargetPosition, hand_angularDistance, RayHitName(cursorController.Handray), currentDwellTime, _trialLogSeq));
                break;
            }
        }

        _trialLogSeq++;
    }

    static string RayHitName(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit))
            return hit.transform.name;
        return "None";
    }

    //public void SetInputMethod(string method)
    //{
    //    if (method.ToLower().Contains("head"))
    //    {
    //        inputMethod.SetHeadPointer();
    //    }
    //    else if (method.ToLower().Contains("eye"))
    //    {
    //        inputMethod.SetEyePointer();
    //    }
    //    else if (method.ToLower().Contains("hand"))
    //    {
    //        inputMethod.SetHandRayPointer();
    //    }
    //}
}
