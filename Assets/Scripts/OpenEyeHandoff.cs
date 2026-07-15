using System.Collections;
using UnityEngine;

/// <summary>
/// MainStudy side: on PC "Recalibrate" (TCP launchApp), launch OpenEye and quit
/// to free the PC TCP socket for calibration.
/// Add to the same GameObject as OpenEyeGazeReceiver.
/// </summary>
public class OpenEyeHandoff : MonoBehaviour
{
    [Header("Target app (OpenEye Player Settings > Android package name)")]
    [SerializeField] string openEyePackageName = "org.MixedRealityToolkit.MRTK3Sample";

    [Header("Sources")]
    public OpenEyeGazeReceiver receiver;

    [Header("Behavior")]
    [SerializeField] float disconnectDelaySec = 0.3f;

    bool _launching;

    void Awake()
    {
        if (receiver == null)
            receiver = GetComponent<OpenEyeGazeReceiver>();
        if (receiver == null)
            receiver = FindObjectOfType<OpenEyeGazeReceiver>();
    }

    void OnEnable()
    {
        if (receiver != null)
            receiver.OnLaunchApp += HandleLaunchApp;
    }

    void OnDisable()
    {
        if (receiver != null)
            receiver.OnLaunchApp -= HandleLaunchApp;
    }

    void HandleLaunchApp(string packageFromPc)
    {
        string package = string.IsNullOrEmpty(packageFromPc) ? openEyePackageName : packageFromPc;
        LaunchOpenEye(package);
    }

    public void LaunchNow()
    {
        LaunchOpenEye(openEyePackageName);
    }

    void LaunchOpenEye(string package)
    {
        if (_launching)
            return;
        _launching = true;
        StartCoroutine(LaunchRoutine(package));
    }

    IEnumerator LaunchRoutine(string package)
    {
        Debug.Log($"[OpenEyeHandoff] Handoff to {package}");

        if (receiver != null)
            receiver.Disconnect();

        yield return new WaitForSeconds(disconnectDelaySec);

        if (!QuestAppLauncher.TryLaunch(package))
        {
            Debug.LogError($"[OpenEyeHandoff] Could not launch {package}. Is OpenEye installed?");
            _launching = false;
            yield break;
        }

        QuestAppLauncher.QuitCurrentApp();
    }
}
