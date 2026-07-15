using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class CursorController : MonoBehaviour
{
    public HandProvider handProvider;
    public EyeGazeProvider gazeProvider;
    public HeadGazeProvider headGazeProvider;
    public Ray Handray;
    public Vector3 eyePosition;
    public Vector3 eyeDirection;
    public Vector3 headPosition;
    public Vector3 headDirection;

    private float defaultDistance = 2.0f;
    private GameObject hitObject;
    private bool hitting = false;
    public string selectedTarget;

    // Start is called before the first frame update
    void Start()
    {
        handProvider = GameObject.Find("HandProviderTransform").GetComponent<HandProvider>();
        var gazeTransform = GameObject.Find("GazeProviderTransform");
        gazeProvider = gazeTransform.GetComponent<EyeGazeProvider>();
        headGazeProvider = gazeTransform.GetComponent<HeadGazeProvider>();
        if (headGazeProvider == null)
            headGazeProvider = gazeTransform.AddComponent<HeadGazeProvider>();
    }

    // Update is called once per frame
    void Update()
    {
        Handray = handProvider.rightHandray;
        eyePosition = gazeProvider.eyeposition;
        eyeDirection = gazeProvider.eyeforward;

        bool useHead = GameManager.instance.study.currentCursor == StudyDesign.Study.CursorType.Head;
        if (headGazeProvider != null)
            headGazeProvider.enabled = useHead;

        if (useHead && headGazeProvider != null)
        {
            headPosition = headGazeProvider.eyeposition;
            headDirection = headGazeProvider.eyeforward;
            OnPointing(new Ray(headPosition, headDirection));
        }
        else if (GameManager.instance.study.currentCursor == StudyDesign.Study.CursorType.Hand)
        {
            OnPointing(Handray);
        }
        else
        {
            OnPointing(new Ray(eyePosition, eyeDirection));
        }

    }
    private void OnPointing(Ray ray)
    {
        RaycastHit hit;

        GameObject currentTargetObject = GameManager.instance.targetControl.getOneTarget(GameManager.instance.study.fittsLaw.endNum).gameObject;
        TargetBehaviour targetBehaviour = currentTargetObject.GetComponent<TargetBehaviour>();
        if (Physics.Raycast(ray, out hit))
        {
            GameObject go = hit.transform.gameObject;
            if ((go.tag == "TargetSphere"))
            {
                TargetBehaviour cB = go.GetComponent<TargetBehaviour>();
                if ((cB.current_target_type == TargetBehaviour.TARGET_TYPE.TARGET) || (cB.current_target_type == TargetBehaviour.TARGET_TYPE.BUTTON))
                {
                    if (cB.current_dwell_status == TargetBehaviour.DWELL_EVENT_TYPE.OFF) { cB.dwellStartEvent.Invoke(); }
                    cB.current_dwell_status = TargetBehaviour.DWELL_EVENT_TYPE.ON;
                }
                //else
                //{
                //    if(cB.current_dwell_status == TargetBehaviour.DWELL_EVENT_TYPE.OFF) { cB.dwellEndEvent.Invoke(); }
                //    cB.current_dwell_status = TargetBehaviour.DWELL_EVENT_TYPE.OFF;
                //}
            }
            else
            {
                targetBehaviour.dwellEndEvent.Invoke();
                GameManager.instance.targetControl.dwell_target.GetComponent<TargetBehaviour>().dwellEndEvent.Invoke();
            }
            selectedTarget = hit.transform.name;

        }
        else
        {
            targetBehaviour.dwellEndEvent.Invoke();
            GameManager.instance.targetControl.dwell_target.GetComponent<TargetBehaviour>().dwellEndEvent.Invoke();
        }
        //else if (hitting )
        //{
        //    if (hitObject != null && hitObject.transform.tag == "TargetSphere")
        //        hitObject.SendMessage("FocusExit", SendMessageOptions.DontRequireReceiver);
        //    hitting = false;
        //    hitObject = null;
        //    selectedTarget = "NONE";
        //}

    }
}
