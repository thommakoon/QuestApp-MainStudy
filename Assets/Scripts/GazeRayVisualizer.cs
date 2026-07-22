using UnityEngine;

/// Optional debug line showing the active pointing ray (eye or head).
[RequireComponent(typeof(LineRenderer))]
public class GazeRayVisualizer : MonoBehaviour
{
    public EyeGazeProvider gazeProvider;
    public HeadGazeProvider headGazeProvider;
    public float rayLength = 3f;
    public Color rayColor = new Color(0f, 0.85f, 1f, 0.9f);
    public Color headRayColor = new Color(1f, 0.75f, 0.2f, 0.9f);

    /// <summary>When false, eye/head debug ray and hand LineRenderers are hidden.</summary>
    public bool showRay = true;

    LineRenderer _line;

    public static void SetRaysVisible(bool visible)
    {
        foreach (var viz in FindObjectsOfType<GazeRayVisualizer>(true))
            viz.showRay = visible;

        // Hand Meta RayInteractor lasers (LineRenderers under HandProvider).
        foreach (var hp in FindObjectsOfType<HandProvider>(true))
        {
            if (hp == null || hp.rightHandRayInteractor == null)
                continue;
            foreach (var lr in hp.rightHandRayInteractor.GetComponentsInChildren<LineRenderer>(true))
                lr.enabled = visible;
        }

        Debug.Log($"[Ray] visible={visible}");
    }

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;
        _line.startWidth = 0.004f;
        _line.endWidth = 0.001f;
        _line.startColor = rayColor;
        _line.endColor = rayColor;
        _line.enabled = false;
    }

    void LateUpdate()
    {
        if (!showRay)
        {
            if (_line != null)
                _line.enabled = false;
            return;
        }

        EnsureProviders();

        bool preferHead = GameManager.instance != null
            && GameManager.instance.study != null
            && GameManager.instance.study.currentCursor == StudyDesign.Study.CursorType.Head;

        Vector3 origin;
        Vector3 end;
        Color color;

        if (preferHead && TryGetHeadRay(out origin, out end))
        {
            color = headRayColor;
        }
        else if (TryGetEyeRay(out origin, out end))
        {
            color = rayColor;
        }
        else if (TryGetHeadRay(out origin, out end))
        {
            color = headRayColor;
        }
        else
        {
            _line.enabled = false;
            return;
        }

        _line.enabled = true;
        _line.startColor = color;
        _line.endColor = color;
        _line.SetPosition(0, origin);
        _line.SetPosition(1, end);
    }

    void EnsureProviders()
    {
        if (gazeProvider == null)
            gazeProvider = FindObjectOfType<EyeGazeProvider>();
        if (headGazeProvider == null)
            headGazeProvider = FindObjectOfType<HeadGazeProvider>();
    }

    bool TryGetEyeRay(out Vector3 origin, out Vector3 end)
    {
        origin = end = Vector3.zero;
        if (gazeProvider == null || !gazeProvider.HasValidGaze)
            return false;

        origin = gazeProvider.eyeposition;
        end = gazeProvider.LastWorldTarget;
        if ((end - origin).sqrMagnitude < 1e-6f)
            end = origin + gazeProvider.eyeforward.normalized * rayLength;
        return true;
    }

    bool TryGetHeadRay(out Vector3 origin, out Vector3 end)
    {
        origin = end = Vector3.zero;
        if (headGazeProvider == null || !headGazeProvider.HasValidGaze)
            return false;

        origin = headGazeProvider.eyeposition;
        end = headGazeProvider.LastWorldTarget;
        if ((end - origin).sqrMagnitude < 1e-6f)
            end = origin + headGazeProvider.eyeforward.normalized * rayLength;
        return true;
    }
}
