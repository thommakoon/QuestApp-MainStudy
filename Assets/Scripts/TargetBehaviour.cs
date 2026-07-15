using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Oculus.Interaction;

public class TargetBehaviour : PointableUnityEventWrapper
//, IMixedRealityFocusHandler
//, IMixedRealityGestureHandler
{

    //[SerializeField]
    //private InteractableUnityEventWrapper eventWrapper;



    public Image indicator;
    const float MAX_DWELL_TIME = 1.0f;
    private static Color DEFAULT_COLOUR = Color.grey;
    private static Color SUCCESS_COLOUR = Color.green;
    private static Color TARGET_COLOUR = Color.red;
    //public UnityEvent onDwellEnd;
    public enum DWELL_EVENT_TYPE { OFF, ON };
    public enum TARGET_TYPE { BUTTON, TARGET, PLANE, NONE };
    //public enum SELECTION_TYPE { DWELL, CLICK };
    public DWELL_EVENT_TYPE current_dwell_status;
    public TARGET_TYPE current_target_type;
    //public SELECTION_TYPE current_selection_type;
    public StudyDesign.Study.SelectionType current_selection_type;
    public bool isClicked = false;
    public float currentDwellTime;

    public UnityEvent dwellStartEvent;
    public UnityEvent dwellEndEvent;
    public UnityEvent dwellSuccessEvent;
    public UnityEvent ClickEvent;

    public Material material;
    // Start is called before the first frame update

    void Start()
    {



        //ResetTarget(_TARGET_TYPE: TARGET_TYPE.TARGET);
        if (this.name.Contains("Menu"))
        {

        }
        else
        {
            material = transform.GetChild(1).GetComponent<Renderer>().material;
        }
        //ResetTarget();
        dwellStartEvent.AddListener(() =>
        {
            //Debug.Log("Start " + this.gameObject.name);
            UpdateDwellStatus(DWELL_EVENT_TYPE.ON);
            isClicked = false;
        });

        dwellEndEvent.AddListener(() =>
        {
            //Debug.Log("END " + this.gameObject.name);
            try
            {
                indicator.fillAmount = 0f;
            }
            catch
            {

            }
            UpdateDwellStatus(DWELL_EVENT_TYPE.OFF);
            ResetTime();
            isClicked = false;
        });

        dwellSuccessEvent.AddListener(() =>
        {
            //when there is a successful dwell -> do next trial
            ResetTime();
            isClicked = false;
            Debug.Log("dwellSuccess! from " + this.transform.name);
            if (current_target_type == TARGET_TYPE.BUTTON) // if it is centre button
            {
                if (GameManager.instance.current_scene == GameManager.SCENE.PREP
                    || GameManager.instance.current_scene == GameManager.SCENE.IDLE)
                {
                    // PC-commanded MainStudy: ignore center start while idle/prep
                    if (GameManager.instance.current_scene == GameManager.SCENE.IDLE)
                        return;
                    GameManager.instance.SceneChange(GameManager.SCENE.BEFORE_TRIAL);
                }
                else if (GameManager.instance.current_scene == GameManager.SCENE.BEFORE_TRIAL)
                {
                    GameManager.instance.SceneChange(GameManager.SCENE.TRIAL);
                }
            }
            else // normal target
            {
                //go to next step

                GameManager.instance.study.fittsLaw.nextStep(success: true);
                //make success sound

            }

        });
        ClickEvent.AddListener(() =>
        {

            ResetTime();
            isClicked = false;
            Debug.Log("clicked from " + transform.name);
            if (current_target_type == TARGET_TYPE.BUTTON)
            {
                if (GameManager.instance.current_scene == GameManager.SCENE.PREP
                    || GameManager.instance.current_scene == GameManager.SCENE.IDLE)
                {
                    if (GameManager.instance.current_scene == GameManager.SCENE.IDLE)
                        return;
                    GameManager.instance.SceneChange(GameManager.SCENE.BEFORE_TRIAL);
                }
                else if (GameManager.instance.current_scene == GameManager.SCENE.BEFORE_TRIAL)
                {
                    GameManager.instance.SceneChange(GameManager.SCENE.TRIAL);
                }
            }
            else if (current_target_type == TARGET_TYPE.TARGET)
            {
                if (GameManager.instance.current_scene == GameManager.SCENE.TRIAL)
                {
                    if (current_dwell_status == DWELL_EVENT_TYPE.ON)
                    {
                        GameManager.instance.study.fittsLaw.nextStep(success: true);
                    }
                    else
                    {
                        GameManager.instance.study.fittsLaw.nextStep(success: false);
                    }
                }
            }
        });

    }
    void Update()
    {
        //if (GameManager.instance.study.currentCursor == StudyDesign.Study.CursorType.MM || GameManager.instance.study.currentCursor == StudyDesign.Study.CursorType.FF)
        //{
        //    RaycastHit hit;
        //    if (Physics.Raycast(GameManager.instance.head.position, GameManager.instance.mmcursor.multimodalCursorDirection, out hit))
        //    {
        //        if (hit.transform.gameObject == transform.gameObject)
        //        {
        //            if (current_dwell_status == DWELL_EVENT_TYPE.OFF)
        //            {
        //                dwellStartEvent.Invoke();
        //            }
        //            current_dwell_status = DWELL_EVENT_TYPE.ON;
        //        }
        //        else
        //        {
        //            if (current_dwell_status == DWELL_EVENT_TYPE.OFF)
        //            {
        //                dwellEndEvent.Invoke();
        //            }
        //            current_dwell_status = DWELL_EVENT_TYPE.OFF;
        //        }
        //    }
        //    else
        //    {
        //        if (current_dwell_status == DWELL_EVENT_TYPE.OFF)
        //        {
        //            dwellEndEvent.Invoke();
        //        }
        //        current_dwell_status = DWELL_EVENT_TYPE.OFF;
        //    }
        //}

        // only in 'ongoing' process
        if (current_dwell_status == DWELL_EVENT_TYPE.ON && current_target_type != TARGET_TYPE.NONE && current_target_type != TARGET_TYPE.PLANE)
        {
            AddTime();
            GameManager.instance.study.fittsLaw.onGoing = true;
            //if (current_selection_type == SELECTION_TYPE.CLICK)
            if (!GameManager.instance.study.fittsLaw.menu || current_target_type == TARGET_TYPE.BUTTON) ChangeColour(SUCCESS_COLOUR);
            //else
            //ChangeColour(Color.Lerp(TARGET_COLOUR, SUCCESS_COLOUR, currentDwellTime / MAX_DWELL_TIME));
            if (current_selection_type == StudyDesign.Study.SelectionType.Dwell)
            {
                indicator.fillAmount = currentDwellTime / MAX_DWELL_TIME;
            }

            if (CheckTime() && current_selection_type == StudyDesign.Study.SelectionType.Dwell)    //Check the current dwell time exceeds the threshold
            {
                dwellSuccessEvent.Invoke();
            }
            //else if (current_selection_type == StudyDesign.Study.SelectionType.Click)
            //{
            //    //check air tap
            //    if (!GameManager.instance.Tapped)
            //    {
            //        dwellSuccessEvent.Invoke();
            //    }
            //}
        }
        else
        {
            GameManager.instance.study.fittsLaw.onGoing = false;
            ChangeColour(current_target_type);
            try
            {
                indicator.fillAmount = 0f;
            }
            catch
            {

            }
        }

    }

    public void MakeCurrentTarget()
    {
        current_target_type = TARGET_TYPE.TARGET;
        ResetTime();
        current_dwell_status = DWELL_EVENT_TYPE.OFF;
    }
    public void UnMakeCurrentTarget()
    {
        current_target_type = TARGET_TYPE.NONE;
        ResetTime();
        current_dwell_status = DWELL_EVENT_TYPE.OFF;
    }
    public void MakeButton()
    {
        current_target_type = TARGET_TYPE.BUTTON;
    }
    public void CheckTap()
    {
        if (current_selection_type == StudyDesign.Study.SelectionType.Click)
        {
            if (current_target_type == TARGET_TYPE.TARGET)
            {
                ClickEvent.Invoke();
            }
            else if (current_target_type == TARGET_TYPE.BUTTON)
            {
                if (current_dwell_status == DWELL_EVENT_TYPE.ON)
                {
                    Debug.Log("Check Tap");
                    ClickEvent.Invoke();
                }
            }
        }

    }
    public void OnEnable()
    {
        //CoreServices.InputSystem?.RegisterHandler<IMixedRealityGestureHandler>(this);
        //ResetTarget();
    }
    public void OnDisable()
    {
        //ResetTarget();
        dwellEndEvent.Invoke();
        //CoreServices.InputSystem?.UnregisterHandler<IMixedRealityGestureHandler>(this);
    }

    public void AddTime()
    {
        currentDwellTime += Time.deltaTime;
    }
    public void ResetTarget(
        DWELL_EVENT_TYPE _EVENT_TYPE = DWELL_EVENT_TYPE.OFF,
        TARGET_TYPE _TARGET_TYPE = TARGET_TYPE.NONE,
        StudyDesign.Study.SelectionType _SELECTION_TYPE = StudyDesign.Study.SelectionType.Dwell)
    {
        ResetTime();
        current_dwell_status = _EVENT_TYPE;
        current_target_type = (current_target_type != TARGET_TYPE.PLANE) ? _TARGET_TYPE : TARGET_TYPE.PLANE;
        current_selection_type = _SELECTION_TYPE;
        //thisRend = GetComponent<Renderer>();

        ChangeColour(current_target_type);
    }
    private void ResetTime()
    {
        currentDwellTime = 0f;
    }
    private void ChangeColour(Color c)
    {
        if (this.name.Contains("Menu"))
        {
            return;
        }

        if ((current_target_type != TARGET_TYPE.PLANE))
        {
            //thisRend.material.SetColor("_Color", c);
            material.color = c;

        }
        else
        {
            //thisRend.material.SetColor("_Color", Color.clear);
            material.color = Color.clear;
        }

    }
    private void ChangeColour(TARGET_TYPE _TARGET_TYPE)
    {
        //material.color. = 0f;

        switch (_TARGET_TYPE)
        {
            case TARGET_TYPE.BUTTON:
                ChangeColour(TARGET_COLOUR);
                break;
            case TARGET_TYPE.TARGET:
                if (GameManager.instance.study.fittsLaw.menu)
                {
                    return;
                }
                ChangeColour(TARGET_COLOUR);
                break;
            case TARGET_TYPE.NONE:
                if (GameManager.instance.study.fittsLaw.menu)
                {
                    return;
                }
                ChangeColour(DEFAULT_COLOUR);
                break;
            case TARGET_TYPE.PLANE:
                ChangeColour(Color.clear);
                break;
        }

    }
    private bool CheckTime()
    {
        bool result = currentDwellTime >= MAX_DWELL_TIME ? true : false;
        return result;
    }
    public void dwellEnd()
    {
        Debug.Log(this.transform.name + " dwell end");

        //onDwellEnd.Invoke();
    }
    public void SetSelectionMethod(StudyDesign.Study.SelectionType type)
    {
        current_selection_type = type;
    }


    public void UpdateDwellStatus(DWELL_EVENT_TYPE type)
    {
        current_dwell_status = type;
        //Debug.Log(type.ToString());
    }

    public void SaySomething(string msg = "hello")
    {
        Debug.Log(msg);
    }

    //public void OnFocusEnter(FocusEventData eventData)
    //{
    //    dwellStartEvent.Invoke();
    //    //throw new NotImplementedException();
    //}

    //public void OnFocusExit(FocusEventData eventData)
    //{
    //    dwellEndEvent.Invoke();
    //}

    //public void OnInputUp(InputEventData eventData)
    //{
    //    isClicked = true;
    //    Debug.Log("UP");
    //    //CheckTap();
    //}

    //public void OnInputDown(InputEventData eventData)
    //{
    //    isClicked = false;
    //}

    //public void OnPointerDown(MixedRealityPointerEventData eventData)
    //{
    //    //throw new NotImplementedException();
    //    //Debug.Log("Down" + eventData.ToString());
    //    //CheckTap();
    //}

    //public void OnPointerDragged(MixedRealityPointerEventData eventData)
    //{
    //    //throw new NotImplementedException();
    //}

    //public void OnPointerUp(MixedRealityPointerEventData eventData)
    //{
    //    //throw new NotImplementedException();
    //}

    //public void OnPointerClicked(MixedRealityPointerEventData eventData)
    //{
    //    //throw new NotImplementedException();
    //    //Debug.Log("Clicked" + eventData.ToString());
    //}
    //public void OnGestureStarted(InputEventData eventData)
    //{
    //    //Debug.Log("Gesture started: " + eventData.MixedRealityInputAction.Description);

    //}

    //public void OnGestureUpdated(InputEventData eventData)
    //{
    //}

    //public void OnGestureCompleted(InputEventData eventData)
    //{

    //    if (eventData.MixedRealityInputAction.Description.ToString() == "Select")
    //    {
    //        Debug.Log("Gesture completed: " + eventData.MixedRealityInputAction.Description + eventData.MixedRealityInputAction.GetType() + " " + eventData.MixedRealityInputAction.ToString());
    //        GameManager.instance.debugText.text = "CLICK";
    //        CheckTap();
    //    }
    //}

    //public void OnGestureCanceled(InputEventData eventData)
    //{
    //}
}

