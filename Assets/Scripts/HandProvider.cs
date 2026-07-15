using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
using System;
using System.Text;
using System.Linq;
using static OVRHand;
using UnityEngine.EventSystems;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;

public class HandProvider : MonoBehaviour
{
    public OVRHand hand;
    public OVRSkeleton handSkeleton;
    public RayInteractor rightHandRayInteractor;
    private DebugLogToUI debug;

    RayInteractor rayInteractor;

    private GameObject TypeField;
    private GameObject TestingField;
    private GameObject Camera;
    private GameObject FeedbackField;

    private bool TaskFieldOn;
    private bool SaveConditionSatisfied;
    private bool DataCollectingSatisfied;
    //private System.Diagnostics.Stopwatch Time = new System.Diagnostics.Stopwatch();

    public static int calsati;
    public static bool seeing;

    private bool PinchedSatisfied;
    private bool pinchstart;
    private bool IsPinchedIndex;
    private bool IsPinchedMiddle;
    private bool IsPinchedRing;

    public static Vector3 middleHitPos;
    private Vector3 IndexTipLocation;
    private Vector3 MiddleTipLocation;
    private Vector3 RingTipLocation;
    private Vector3 ThumbTipLocation;

    private float IndexPinchStr;
    private float MiddlePinchStr;
    private float RingPinchStr;


    private string pinchingData;
    public static int step;
    //private TextMeshProUGUI Uitext;

    private List<string> Dataset = new List<string>();
    private List<bool> PinchData = new List<bool>();
    private List<Vector3> GazeData = new List<Vector3>();
    public Ray rightHandray;

    float lastClick = 0.0f;
    float THRESHOLD = 0.2f;
    private bool previousTap;
    private bool currentTap;

    void Awake()
    {
        //if (!hand) hand = GetComponent<OVRHand>();
        //if (!handSkeleton) handSkeleton = GetComponent<OVRSkeleton>();
    }
    void Start()
    {
        rayInteractor = GetComponent<RayInteractor>();
        previousTap = false;
        currentTap = true;

        //Camera = GameObject.Find("OVRCameraRig");
    }


    void Update()
    {

        rightHandray = rightHandRayInteractor.Ray;


        currentTap = hand.GetFingerIsPinching(HandFinger.Index);
        if (CheckTap())
        {
            if (lastClick < Time.realtimeSinceStartup - THRESHOLD)
            {
                GameManager.instance.targetControl.sendTap();
                lastClick = Time.realtimeSinceStartup;
            }
        }

     

        previousTap = currentTap;
    }
    public bool CheckTap()
    {
        if (!previousTap)
        {
            if (currentTap)
            {
                return true;
            }
        }
        return false;
    }
}
