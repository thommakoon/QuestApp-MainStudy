using UnityEngine;

/// <summary>
/// Head-forward pointing ray from the VR center eye (for HeadDwell / standing tests).
/// </summary>
public class HeadGazeProvider : MonoBehaviour
{
    [Tooltip("Optional. Defaults to CenterEyeAnchor / Camera.main.")]
    public Transform referenceCamera;

    [Tooltip("Debug plane distance (m) for ray visualizer end point.")]
    public float rayPlaneDistanceM = 1f;

    public bool HasValidGaze { get; private set; }
    public Vector3 eyeposition;
    public Vector3 eyeforward;
    public Vector3 LastWorldTarget { get; private set; }

    Transform _cameraTransform;

    void OnEnable()
    {
        _cameraTransform = null;
    }

    void Update()
    {
        Transform camTf = ResolveCameraTransform();
        if (camTf == null)
        {
            HasValidGaze = false;
            return;
        }

        eyeposition = camTf.position;
        eyeforward = camTf.forward.normalized;
        LastWorldTarget = eyeposition + eyeforward * rayPlaneDistanceM;
        HasValidGaze = true;

        transform.SetPositionAndRotation(eyeposition, Quaternion.LookRotation(eyeforward, camTf.up));
    }

    Transform ResolveCameraTransform()
    {
        if (referenceCamera != null)
            return referenceCamera;

        if (_cameraTransform != null && _cameraTransform.gameObject.activeInHierarchy)
            return _cameraTransform;

        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
            return _cameraTransform;
        }

        var centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null)
        {
            _cameraTransform = centerEye.transform;
            return _cameraTransform;
        }

        return null;
    }
}
