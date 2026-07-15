using System;
using Unity.VisualScripting;
//using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

public class ControlTargets : MonoBehaviour
{
    public GameObject FittsTarget;
    public GameObject dwell_target;
    public GameObject targets;
    public GameObject nemo_target;
    public GameObject semo_target;
    public GameObject round_target;
    public GameObject menuTargets;

    public GameObject nemo;
    public GameObject semo;

    Color[] colors = new Color[] { Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow };
    private int totalTargetNumber = 11;
    private float defaultDistance = 1f;

    private float targetSize = Mathf.Tan(Mathf.Deg2Rad * 2.5f) *2*2 ; // for 5 degrees
    //private float targetSize = Mathf.Tan(Mathf.Deg2Rad * 1.5f) * 2; // for 3 degrees

    private float wide = Mathf.Tan(15f * Mathf.Deg2Rad);

    // Start is called before the first frame update
    void Awake()
    {
        MakeDwellTarget();

        makeTargets();
        makeMenuTargets();
        FittsTarget.SetActive(false);
    }
    private void Start()
    {
        transform.position = GameManager.instance.head.position ;
    }
    // Update is called once per frame
    void Update()
    {
        

    }
    void MakeDwellTarget()
    {
        dwell_target = Instantiate(FittsTarget, new Vector3(0, defaultDistance/8f, defaultDistance), Quaternion.identity);
        dwell_target.transform.localScale = Vector3.one * targetSize;
        dwell_target.transform.SetParent(transform);
        dwell_target.name = "DwellTarget";
        dwell_target.GetComponent<TargetBehaviour>().MakeButton();
    }
    public void SetCurrentTarget(int num)
    {
        //Debug.Log("set current target" + num);
        for (int i = 0; i < totalTargetNumber; i++)
        {
            if (i == num)
            {
                targets.transform.GetChild(i).GetComponent<TargetBehaviour>().MakeCurrentTarget();
            }
            else
            {
                targets.transform.GetChild(i).GetComponent<TargetBehaviour>().UnMakeCurrentTarget();
            }
        }
    }
    public void SetCurrentMenuTarget(int num)
    {
        //Debug.Log("set current target" + num);
        for (int i = 0; i < menuTargets.transform.childCount; i++)
        {
            if (i == num)
            {
                menuTargets.transform.GetChild(i).GetComponent<TargetBehaviour>().MakeCurrentTarget();
            }
            else
            {
                menuTargets.transform.GetChild(i).GetComponent<TargetBehaviour>().UnMakeCurrentTarget();
            }
        }
    }
    public void sendTap()
    {
        getOneTarget(GameManager.instance.study.fittsLaw.endNum).GetComponent<TargetBehaviour>().CheckTap();
        dwell_target.GetComponent<TargetBehaviour>().CheckTap();
    }
    public void ShowTargets(bool show)
    {
        for (int i = 0; i < totalTargetNumber; i++)
        {
            targets.transform.GetChild(i).gameObject.SetActive(show);
        }
    }
    public void ShowMenuTargets(bool show)
    {
        menuTargets.gameObject.SetActive(show);
    }
    public void ShowDwellTarget(bool show)
    {
        {
            dwell_target.transform.localPosition = new Vector3(0, 0, defaultDistance);
        }
        dwell_target.GetComponent<TargetBehaviour>().MakeButton();
        dwell_target.SetActive(show);
        if (GameManager.instance.current_scene == GameManager.SCENE.BEFORE_TRIAL && GameManager.instance.study.fittsLaw.menu && show)
        {
            GameObject which;
            int endnum = GameManager.instance.study.fittsLaw.endNum;
            int color = endnum % 6;
            if (GameManager.instance.study.fittsLaw.endNum > 6)
            {
                semo.transform.GetChild(0).gameObject.GetComponent<Image>().color = colors[color];
                semo.SetActive(true);
                nemo.SetActive(false);
                semo.transform.localPosition = dwell_target.transform.localPosition + new Vector3(0, 0.30f, 0);
                semo.transform.localScale = dwell_target.transform.localScale;

            }
            else
            {
                nemo.transform.GetChild(0).gameObject.GetComponent<Image>().color = colors[color];
                semo.SetActive(false);
                nemo.SetActive(true);
                nemo.transform.localPosition = dwell_target.transform.localPosition + new Vector3(0, 0.30f, 0);
                nemo.transform.localScale = dwell_target.transform.localScale;
            }

        }
        else
        {
            semo.SetActive(false);
            nemo.SetActive(false);
        }
        
    }
    public void SetCondition(StudyDesign.Study.SelectionType selectionType)
    {
        for (int i = 0; i < totalTargetNumber; i++)
        {
            targets.transform.GetChild(i).GetComponent<TargetBehaviour>().ResetTarget(_SELECTION_TYPE: selectionType);
        }
        for (int i = 0; i < menuTargets.transform.childCount; i++)
        {


            menuTargets.transform.GetChild(i).GetComponent<TargetBehaviour>().ResetTarget(_SELECTION_TYPE: selectionType);

        }
    }
    public void SetSelectionTechnique(StudyDesign.Study.SelectionType type)
    {
        for (int i = 0; i < targets.transform.childCount; i++)
        {
            targets.transform.GetChild(i).GetComponent<TargetBehaviour>().SetSelectionMethod(type);
        }
        dwell_target.GetComponent<TargetBehaviour>().SetSelectionMethod(type);
        for (int i = 0; i < menuTargets.transform.childCount; i++)
        {

            
            menuTargets.transform.GetChild(i).GetComponent<TargetBehaviour>().SetSelectionMethod(type);

        }
    }
    public void SetStudyType()
    {
        //Debug.Log(GameManager.instance.study.currentStudyType);
        //Debug.Log(GameManager.instance.study.fittsLaw.randomArray);
        //Debug.Log(GameManager.instance.study.currentStudyType);
        if (GameManager.instance.study.currentStudyType == StudyDesign.Study.StudyType.Menu)
        {
            arrangeMenuTargets(GameManager.instance.study.fittsLaw.randomArray);
            //Debug.Log("Random array in setstudy " + GameManager.instance.study.fittsLaw.randomArray);
        }
    }
    void makeTargets()
    {
        targets = new GameObject("Targets");
        targets.transform.parent = transform;
        targets.transform.position = transform.position + new Vector3(0, 0, defaultDistance);
        for (int i = 0; i < totalTargetNumber; i++)
        {
            //Make Target Objects and move to child of the main object.
            GameObject copiedTarget = Instantiate(FittsTarget, new Vector3(0, 0, 0), Quaternion.identity);
            copiedTarget.transform.SetParent(targets.transform);
            copiedTarget.name = "Target_" + i;
            copiedTarget.transform.localPosition = new Vector3(wide * Convert.ToSingle(Math.Sin(i * Math.PI / totalTargetNumber * 2)), wide * Convert.ToSingle(Math.Cos(i * Math.PI / totalTargetNumber * 2)), 0);
            copiedTarget.transform.localScale = Vector3.one * targetSize;

        }

    }
    public void arrangeMenuTargets(int[] randomArray)
    {
        if (randomArray.Length != 12)
        {
            Debug.Log("Array is not 12");
            return;
        }
        Debug.Log("Arranging menu targets" + string.Join(", ", randomArray));
        float gap = 0.3f/4;
        float[] xs = new float[]
        {-3*gap,-1*gap,gap,3*gap,
        -4*gap,-2*gap,2*gap,4*gap,
        -3*gap,-1*gap,gap,3*gap
        };
        float[] ys = new float[]
        {
            3*gap,3*gap,3*gap,3*gap,
            0f,0f,0f,0f,
            -3*gap,-3*gap,-3*gap,-3*gap,
        };
        for (int i = 0; i < randomArray.Length; i++)
        {
            //GameObject t = GameObject.Find("MenuTarget_" + i);
            menuTargets.transform.GetChild(randomArray[i]).localPosition = new Vector3(xs[i], ys[i], 0f);
            //t.transform.localPosition = new Vector3(xs[i], ys[i], 0f);
            //Debug.Log(i + " Menu going to" + xs[i] + "  " + ys[i]);
        }


    }
    void makeMenuTargets()
    {
        menuTargets.transform.localPosition =  new Vector3(0, 0, defaultDistance);
       
        for (int i = 0; i < 6; i++)
        {
            GameObject copiedTarget = Instantiate(nemo_target, new Vector3(0, 0, 0), Quaternion.identity);
            copiedTarget.transform.SetParent(menuTargets.transform);
            copiedTarget.name = "MenuTarget_" + i;
            copiedTarget.transform.GetChild(1).gameObject.GetComponent<Image>().color = colors[i];

            copiedTarget.transform.localScale = Vector3.one * targetSize;
        }
        for (int i = 6; i < 12; i++)
        {
            GameObject copiedTarget = Instantiate(semo_target, new Vector3(0, 0, 0), Quaternion.identity);
            copiedTarget.transform.SetParent(menuTargets.transform);
            copiedTarget.name = "MenuTarget_" + i;
            copiedTarget.transform.GetChild(1).gameObject.GetComponent<Image>().color = colors[i - 6];

            copiedTarget.transform.localScale = Vector3.one * targetSize;
        }

        nemo_target.SetActive(false);
        semo_target.SetActive(false);
        //round_target.SetActive(false);
    }
    public Transform getOneTarget(int targetNum)
    {
        if (GameManager.instance.study.fittsLaw.menu)
        {
            return menuTargets.transform.GetChild(targetNum);
        }
        else
        {
            return targets.transform.GetChild(targetNum);
        }

    }
}
