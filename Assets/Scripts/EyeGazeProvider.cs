using System;
using UnityEngine;

public class EyeGazeProvider : MonoBehaviour
{
    public enum GazeSource
    {
        OVR,
        OpenEye,
    }

    [Header("Source")]
    public GazeSource gazeSource = GazeSource.OpenEye;
    public OpenEyeGazeReceiver openEyeReceiver;
    [Tooltip("Meters in front of the reference frame (matches OpenEye calibration plane).")]
    public float gazePlaneDistanceM = 1.0f;

    [Header("OVR (Quest Pro / Quest 2 eye tracking only)")]
    public EyeId Eye;
    [Range(0f, 1f)]
    public float ConfidenceThreshold = 0.5f;

    [Header("Shared")]
    [Tooltip("OVR only. OpenEye uses CenterEyeAnchor / VR camera (same as OpenEye DotGridCalibrator).")]
    public Transform ReferenceFrame;
    public GameObject currentCursorVisual;
    public string targetTag = "TargetObject";

    private Camera mainCamera;
    private OVRPlugin.EyeGazesState _currentEyeGazesState;
    private Quaternion _initialRotationOffset;
    private Transform _viewTransform;
    private Action<string> _onPermissionGranted;
    private static int _trackingInstanceCount;
    private const OVRPermissionsRequester.Permission EyeTrackingPermission =
        OVRPermissionsRequester.Permission.EyeTracking;

    private long _lastOpenEyeSeq;

    public bool EyeTrackingEnabled =>
        gazeSource == GazeSource.OpenEye
            ? openEyeReceiver != null && openEyeReceiver.CurrentState == OpenEyeGazeReceiver.State.Connected
            : OVRPlugin.eyeTrackingEnabled;

    public float Confidence { get; private set; }
    public bool HasValidGaze { get; private set; }
    public long GazeSequence { get; private set; }
    public Vector2 LastPlaneMeters { get; private set; }
    public Vector3 LastWorldTarget { get; private set; }
    public Transform OpenEyeReferenceTransform { get; private set; }
    /// <summary>OpenEye/Neon unix seconds (payload.t). NaN if not OpenEye.</summary>
    public double LastNeonTimestampSec { get; private set; } = double.NaN;
    /// <summary>OpenEye/Neon unix ns (payload.t_ns). 0 if absent.</summary>
    public long LastNeonTimestampNs { get; private set; }
    /// <summary>Quest unix ms when TCP gazeVisual was received. 0 if absent.</summary>
    public long LastQuestReceivedUnixMs { get; private set; }
    public Vector3 eyeforward;
    public Vector3 eyeposition;

    private void Awake()
    {
        _onPermissionGranted = OnPermissionGranted;
    }

    void Start()
    {
        EnsureVrCamera();
        PrepareHeadDirection();

        if (openEyeReceiver == null)
            openEyeReceiver = FindObjectOfType<OpenEyeGazeReceiver>();
    }

    private void OnEnable()
    {
        if (gazeSource != GazeSource.OVR)
            return;

        _trackingInstanceCount++;
        if (!StartEyeTracking())
            enabled = false;
    }

    private void OnDisable()
    {
        if (gazeSource != GazeSource.OVR)
            return;

        if (--_trackingInstanceCount == 0)
            OVRPlugin.StopEyeTracking();
    }

    private void OnDestroy()
    {
        OVRPermissionsRequester.PermissionGranted -= _onPermissionGranted;
    }

    void Update()
    {
        if (gazeSource == GazeSource.OpenEye)
            UpdateOpenEyeGaze();
        else
            UpdateOvrGaze();
    }

    void UpdateOpenEyeGaze()
    {
        HasValidGaze = false;
        if (openEyeReceiver == null)
            return;

        Transform refTf = ResolveOpenEyeReferenceFrame();
        if (refTf == null)
            return;

        if (!openEyeReceiver.TryGetLatestGaze(ref _lastOpenEyeSeq, out var sample))
            return;

        Vector3 localTarget = new Vector3(sample.planeMeters.x, sample.planeMeters.y, gazePlaneDistanceM);
        Vector3 worldTarget = refTf.TransformPoint(localTarget);
        OpenEyeReferenceTransform = refTf;
        LastWorldTarget = worldTarget;

        // Match OpenEye DotGridCalibrator: referenceForward ?? xrCamera.transform.
        eyeposition = refTf.position;
        Vector3 dir = worldTarget - eyeposition;
        if (dir.sqrMagnitude < 1e-6f)
            return;

        eyeforward = dir.normalized;
        LastPlaneMeters = sample.planeMeters;
        LastNeonTimestampSec = sample.timestamp;
        LastNeonTimestampNs = sample.timestampNs;
        LastQuestReceivedUnixMs = sample.questReceivedUnixMs;
        Confidence = 1f;
        HasValidGaze = true;
        GazeSequence = _lastOpenEyeSeq;

        transform.SetPositionAndRotation(eyeposition, Quaternion.LookRotation(eyeforward, refTf.up));

        if (currentCursorVisual != null)
            currentCursorVisual.transform.position = worldTarget;
    }

    Transform ResolveOpenEyeReferenceFrame()
    {
        EnsureVrCamera();
        if (mainCamera != null)
            return mainCamera.transform;

        var centerEye = GameObject.Find("CenterEyeAnchor");
        return centerEye != null ? centerEye.transform : null;
    }

    void EnsureVrCamera()
    {
        if (mainCamera != null && mainCamera.enabled && mainCamera.gameObject.activeInHierarchy)
            return;

        mainCamera = Camera.main;
        if (mainCamera != null)
            return;

        var centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
            mainCamera = centerEye.GetComponent<Camera>();

        if (mainCamera != null)
            return;

        foreach (var cam in Camera.allCameras)
        {
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                mainCamera = cam;
                return;
            }
        }
    }

    void UpdateOvrGaze()
    {
        HasValidGaze = false;
        if (!checkEyetracking())
            return;

        var eyeGaze = _currentEyeGazesState.EyeGazes[(int)Eye];
        var pose = eyeGaze.Pose.ToOVRPose().ToHeadSpacePose();

        Quaternion eyeRotation = mainCamera.transform.rotation * CalculateEyeRotation(pose.orientation);
        eyeforward = eyeRotation * Vector3.forward;
        eyeposition = mainCamera.transform.position;
        transform.position = pose.position;
        transform.rotation = eyeRotation;

        HasValidGaze = true;
        GazeSequence++;

        if (currentCursorVisual != null)
            currentCursorVisual.transform.position = eyeposition + eyeforward.normalized;
    }

    private void OnPermissionGranted(string permissionId)
    {
        if (permissionId == OVRPermissionsRequester.GetPermissionId(EyeTrackingPermission))
        {
            OVRPermissionsRequester.PermissionGranted -= _onPermissionGranted;
            enabled = true;
        }
    }

    private bool StartEyeTracking()
    {
        if (!OVRPermissionsRequester.IsPermissionGranted(EyeTrackingPermission))
        {
            OVRPermissionsRequester.PermissionGranted -= _onPermissionGranted;
            OVRPermissionsRequester.PermissionGranted += _onPermissionGranted;
            return false;
        }

        if (!OVRPlugin.StartEyeTracking())
        {
            Debug.LogWarning($"[{nameof(EyeGazeProvider)}] Failed to start OVR eye tracking.");
            return false;
        }

        return true;
    }

    private bool checkEyetracking()
    {
        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref _currentEyeGazesState))
            return false;

        var eyeGaze = _currentEyeGazesState.EyeGazes[(int)Eye];
        if (!eyeGaze.IsValid)
            return false;

        Confidence = eyeGaze.Confidence;
        if (Confidence < ConfidenceThreshold)
            return false;

        return true;
    }

    private Quaternion CalculateEyeRotation(Quaternion eyeRotation)
    {
        var eyeRotationWorldSpace = _viewTransform.rotation * eyeRotation;
        var lookDirection = eyeRotationWorldSpace * Vector3.forward;
        var targetRotation = Quaternion.LookRotation(lookDirection, _viewTransform.up);
        return targetRotation * _initialRotationOffset;
    }

    private void PrepareHeadDirection()
    {
        const string transformName = "HeadLookAtDirection";
        _viewTransform = new GameObject(transformName).transform;

        if (ReferenceFrame)
            _viewTransform.SetPositionAndRotation(ReferenceFrame.position, ReferenceFrame.rotation);
        else
            _viewTransform.SetPositionAndRotation(transform.position, Quaternion.identity);

        _viewTransform.parent = transform.parent;
        _initialRotationOffset = Quaternion.Inverse(_viewTransform.rotation) * transform.rotation;
    }

    public enum EyeId
    {
        Left = OVRPlugin.Eye.Left,
        Right = OVRPlugin.Eye.Right
    }

    public enum EyeTrackingMode
    {
        HeadSpace,
        WorldSpace,
        TrackingSpace
    }
}
