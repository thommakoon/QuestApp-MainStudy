using System.Collections;
using UnityEngine;

/// <summary>
/// MainStudy side: on PC "Recalibrate" (TCP launchApp), launch OpenEye then quit.
/// </summary>
public class OpenEyeHandoff : MonoBehaviour
{
    [Header("Target app (OpenEye Player Settings > Android package name)")]
    [SerializeField] string openEyePackageName = "org.MixedRealityToolkit.MRTK3Sample";

    [Header("Sources")]
    public OpenEyeGazeReceiver receiver;

    [Header("Behavior")]
    [SerializeField] float quitAfterLaunchSec = 0.8f;

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
        Debug.Log($"[OpenEyeHandoff] launchApp received → {package}");
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

        if (!QuestAppLauncher.TryLaunch(package))
        {
            Debug.LogError($"[OpenEyeHandoff] Could not launch {package}. Is OpenEye installed? Check <queries>.");
            _launching = false;
            yield break;
        }

        yield return new WaitForSecondsRealtime(quitAfterLaunchSec);

        if (receiver != null)
            receiver.Disconnect();

        yield return null;
        QuestAppLauncher.QuitCurrentApp();
    }
}
