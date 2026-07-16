using Newtonsoft.Json;
using OVRSimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StudyDesign
{

    public class Study
    {
        //public enum PostureType { WALK, STAND, TreadMill }

        public enum CursorType { Eye, Hand, Head }
        public enum SelectionType { Click, Dwell }
        public enum StudyType { Fitts, Menu }

        public enum ConditionType { EyePinch, HandPinch, EyeDwell, HeadDwell, HeadPinch }

        public int TOTAL_REP = 3;
        public int currentRep = 0;
        public int sub_num;
        public int subsub_num;
        //public CursorType[] Cursors;
        public CursorType currentCursor;
        //public SelectionType[] Selections;
        public SelectionType currentSelection;
        public ConditionType currentCondition;
        public ConditionType[] Conditions;
        public StudyType[] StudyTypes;
        public StudyType currentStudyType;

        public FittsLaw fittsLaw;

        public int currentConditionIndex = 0;
        public int currentStudyTypeIndex = 0;
        /// <summary>When true, finishing TOTAL_REP returns to IDLE instead of next condition.</summary>
        public bool commandedMode;
        /// <summary>Wall-clock seconds per Fitts rep when commanded (0 = end after one ring).</summary>
        public float trialDurationSec;

        //public int currentCursorIndex = 0;
        //public int currentSelectionIndex = 0;
        public Study(int _total_repetition = 3, bool demo = false, bool eyeTest = false)
        {
            Debug.Log("Making new study");
            UnControlledSetSubjectNumber();
            TOTAL_REP = _total_repetition;
            commandedMode = false;
            trialDurationSec = 0f;
            InitFixedConditions();
            StudyTypes = new StudyType[] { StudyType.Fitts };
            SetCondition();
        }

        /// <summary>PC-commanded single-condition Fitts block(s).</summary>
        public static Study FromPcCommand(int subNum, int subsubNum, ConditionType condition, int reps, float durationSec = 300f)
        {
            var study = new Study(skipAutoInit: true);
            study.commandedMode = true;
            study.TOTAL_REP = Mathf.Max(1, reps);
            study.trialDurationSec = Mathf.Max(1f, durationSec);
            study.sub_num = subNum;
            study.subsub_num = subsubNum;
            study.Conditions = new ConditionType[] { condition };
            study.StudyTypes = new StudyType[] { StudyType.Fitts };
            study.currentConditionIndex = 0;
            study.currentStudyTypeIndex = 0;
            study.currentRep = 0;
            study.SetCondition();
            Debug.Log($"PC commanded study sub={subNum}-{subsubNum} {condition} x{reps} duration={durationSec}s");
            return study;
        }

        Study(bool skipAutoInit)
        {
            // Used by FromPcCommand only.
        }

        void InitFixedConditions()
        {
            // Fixed study order (no latin square): EyeDwell → HandPinch → EyePinch.
            Conditions = new ConditionType[]
            {
                ConditionType.EyeDwell,
                ConditionType.HandPinch,
                ConditionType.EyePinch,
            };
        }
        public void SetCondition()
        {

            currentStudyType = StudyTypes[currentStudyTypeIndex];
            Debug.Log(currentStudyType);
            currentCondition = Conditions[currentConditionIndex];
            SetCursor();
            SetSelection();
            fittsLaw = new FittsLaw(11);
            Debug.Log("set condition" + currentCondition.ToString());
        }
        public void SetStudyType()
        {
            GameManager.instance.targetControl.SetStudyType();
        }
        public void SetCursor()
        {
            switch (currentCondition)
            {
                case ConditionType.EyePinch:
                case ConditionType.EyeDwell:
                    currentCursor = CursorType.Eye;
                    break;
                case ConditionType.HandPinch:
                    currentCursor = CursorType.Hand;
                    break;
                case ConditionType.HeadDwell:
                case ConditionType.HeadPinch:
                    currentCursor = CursorType.Head;
                    break;
                default:
                    currentCursor = CursorType.Hand;
                    break;
            }
        }

        public void SetSelection()
        {
            if (currentCondition.ToString().EndsWith("h"))
            {
                currentSelection = SelectionType.Click;
            }
            else
            {
                currentSelection = SelectionType.Dwell;
            }
            GameManager.instance.targetControl.SetSelectionTechnique(currentSelection);
        }
        public void NextStep()
        {
            Debug.Log("experiment goes to next step");

            currentRep++;
            if (currentRep >= TOTAL_REP) // if repetition ends for this condition
            {
                currentRep = 0;
                currentStudyTypeIndex = 0;

                if (commandedMode)
                {
                    GameManager.instance.MarkCommandedConditionComplete();
                    return;
                }

                currentConditionIndex++;
                if (currentConditionIndex >= Conditions.Length)
                {
                    GameManager.instance.SceneChange(GameManager.SCENE.END);
                    return;
                }

                currentCondition = Conditions[currentConditionIndex];
                currentStudyType = StudyTypes[currentStudyTypeIndex];
                SetCondition();
            }
            else
            {
                SetCondition();
            }
        }
        public void UnControlledSetSubjectNumber(int _total_repetition = 10)
        {
            Debug.Log("UnControlledSetSubjectNumber");
            int temp_subNum = 0;
            int temp_subsubNum = 0;
            while (true)
            {
                string path = string.Format("{0}/{1}-{2}/", Application.persistentDataPath, temp_subNum, temp_subsubNum);
                if (Directory.Exists(path))
                {
                    if (temp_subsubNum == 2)
                    {
                        temp_subNum++;
                        temp_subsubNum= 0;
                    } else
                    {
                        temp_subsubNum++;
                    }
                }
                else
                {
                    SetSubjectNumber(temp_subNum, temp_subsubNum, _total_repetition);
                    return;
                }
                if (temp_subNum > 1000)
                {
                    return;
                }
            }
        }
        public void SetSubjectNumber(int _sub_num, int _subsub_num, int _total_repetition = 10)
        {
            sub_num = _sub_num;
            subsub_num = _subsub_num;
            Debug.Log("subject number set to " + sub_num + "-" + subsub_num);
            if (sub_num != -1)
            {

            }
        }

    }
    public class FittsLaw
    {
        const float TIMEOUT = 5.0f;
        public int startNum;
        public int endNum;
        public int stepNum;
        public int stepCount;
        public int totalTargetNum;
        public float current_elapsed_time;
        public bool onGoing;
        public string[] success_record;
        public int[] randomArray;
        public bool menu;
        //public List<int> randomTargetList;

        public FittsLaw(int _totalTargetNum, bool _menu = false)
        {
            menu = _menu;
            onGoing = false;
            totalTargetNum = _totalTargetNum;
            stepCount = 0;
            startNum = 0;
            current_elapsed_time = 0f;
            stepNum = (int)(totalTargetNum / 2);
            startNum = totalTargetNum - stepNum;
            endNum = calculate_endNum(startNum);
            success_record = new string[_totalTargetNum + 1];
            //randomTargetList = new List<int>(totalTargetNum);
            //for (int i = 0; i < totalTargetNum; i++)
            //{
            //    randomTargetList.Add(i);
            //}
            //randomTargetList = GetShuffleList<int>(randomTargetList);


            if (menu)
            {
                //stepNum = 1; startNum = 0;endNum=calculate_endNum(startNum);
                endNum = 0;
                randomArray = getRandomArray();
                Debug.Log(string.Join(", ", randomArray));
                endNum = UnityEngine.Random.Range(0, randomArray.Length);
                totalTargetNum = 0;
                GameManager.instance.targetControl.SetCurrentMenuTarget(endNum);
            }
            else
            {
                GameManager.instance.targetControl.SetCurrentTarget(endNum);
            }
        }
        public int[] getRandomArray()
        {
            int[] originalArray = new int[12];
            for (int i = 0; i < originalArray.Length; i++)
            {
                originalArray[i] = i;
            }

            // Fisher-Yates shuffle algorithm
            for (int i = originalArray.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                // Swap elements
                int temp = originalArray[i];
                originalArray[i] = originalArray[j];
                originalArray[j] = temp;
            }

            // Create array for 12 selected numbers
            int[] selectedNumbers = new int[12];

            // Take first 12 numbers from shuffled array
            for (int i = 0; i < 12; i++)
            {
                selectedNumbers[i] = originalArray[i];
            }

            // Shuffle the selected numbers again using Fisher-Yates
            for (int i = selectedNumbers.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                // Swap elements
                int temp = selectedNumbers[i];
                selectedNumbers[i] = selectedNumbers[j];
                selectedNumbers[j] = temp;
            }
            string result = "Selected and shuffled numbers: ";
            for (int i = 0; i < selectedNumbers.Length; i++)
            {
                result += selectedNumbers[i].ToString();
                if (i < selectedNumbers.Length - 1)
                {
                    result += ", ";
                }
            }
            Debug.Log(result);
            return selectedNumbers;
        }
        public void nextStep(bool success = false)
        {
            Debug.Log("fittslaw next step");
            //GameManager.instance.makeSound(success);
            if (!menu)
            {
                if (success)
                {
                    success_record[endNum] = "O";

                }
                else { success_record[endNum] = "X"; }
            }

            current_elapsed_time = 0f;
            stepCount += 1;
            startNum = endNum;
            if (stepCount > totalTargetNum)
            {
                var study = GameManager.instance != null ? GameManager.instance.study : null;
                bool timedLoop = study != null && study.commandedMode && study.trialDurationSec > 0f
                    && GameManager.instance.current_scene == GameManager.SCENE.TRIAL
                    && !GameManager.instance.TimedTrialExpired();

                if (timedLoop)
                {
                    // Keep selecting for the remaining trial duration.
                    stepCount = 1;
                    Debug.Log("fitts law ring lap — continue timed trial");
                }
                else
                {
                    //finished one block
                    Debug.Log("fitts law task ended");
                    stepCount = 0;
                    finish();
                    GameManager.instance.SceneChange(GameManager.SCENE.AFTER_TRIAL);


                    GameManager.instance.study.NextStep();

                    return;
                }
            }

            if (menu)
            {
                endNum += 1;
            }
            else
            {
                endNum = calculate_endNum(startNum);
            }
            Debug.Log("Target is " + endNum);
            if (menu)
            {
                //stepNum = 1; startNum = 0;endNum=calculate_endNum(startNum);
                //endNum = 0;
                //randomArray = getRandomArray();
                GameManager.instance.targetControl.SetCurrentMenuTarget(endNum);
            }
            else
            {
                GameManager.instance.targetControl.SetCurrentTarget(endNum);
            }
            //for (int i = 0; i < TargetPosition.instance.targets.transform.childCount; i++)
            //{
            //    TargetPosition.instance.getOneTarget(i).GetComponent<TargetVisual>().unmakeCurrentTarget();
            //}

            //TargetPosition.instance.getOneTarget(endNum).GetComponent<TargetVisual>().makeCurrentTarget();
        }
        public bool check_timeout()
        {
            //Debug.Log(current_elapsed_time);
            if (current_elapsed_time > TIMEOUT)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void finish()
        {
            current_elapsed_time = 0f;
            //for (int i = 0; i < TargetPosition.instance.targets.transform.childCount; i++)
            //{
            //    {
            //        TargetPosition.instance.getOneTarget(i).GetComponent<TargetVisual>().unmakeCurrentTarget();
            //    }
            //}
        }
        public int calculate_endNum(int start)
        {
            int end = start + stepNum;
            if (end > totalTargetNum - 1)
            {
                end = end - totalTargetNum;
            }
            return end;
        }
    }
    [System.Serializable]
    public class EyeCursorData
    {
        public string cursor_type;
        public SerializableVector3 origin;
        public SerializableVector3 direction;
        /// <summary>OpenEye/Neon unix seconds (gazeVisual.t). NaN if unavailable.</summary>
        public double neonGazeT;
        /// <summary>OpenEye/Neon unix ns (gazeVisual.t_ns). 0 if unavailable.</summary>
        public long neonGazeTNs;
        /// <summary>Quest unix ms when gazeVisual TCP packet was received. 0 if unavailable.</summary>
        public long questGazeReceivedUnixMs;

        public EyeCursorData(Ray eyeRay,
            double neonGazeT = double.NaN,
            long neonGazeTNs = 0,
            long questGazeReceivedUnixMs = 0)
        {
            cursor_type = "EYE";
            origin = eyeRay.origin;
            direction = eyeRay.direction;
            this.neonGazeT = neonGazeT;
            this.neonGazeTNs = neonGazeTNs;
            this.questGazeReceivedUnixMs = questGazeReceivedUnixMs;
        }
    }

    [System.Serializable]
    public class HandCursorData
    {
        public string cursor_type;
        public SerializableVector3 origin;
        public SerializableVector3 direction;
        //public string target_name;
        public HandCursorData(Ray HandRay)
        {
            cursor_type = "HAND";
            origin = HandRay.origin;
            direction = HandRay.direction;
            //RaycastHit hit;
            //if (Physics.Raycast(HandRay, out hit))
            //{
            //    target_name = hit.transform.name;
            //}
            //else
            //{
            //    target_name = "";
            //}
        }
    }

    [System.Serializable]
    public class HeadCursorData
    {
        public string cursor_type;
        public SerializableVector3 origin;
        public SerializableVector3 direction;

        public HeadCursorData(Ray headRay)
        {
            cursor_type = "HEAD";
            origin = headRay.origin;
            direction = headRay.direction;
        }
    }

    public class FrameData<T>
    {
        public float timestamp { get; set; }
        public long unixTimeMilliseconds;
        /// <summary>Local wall-clock string (QuestApp format): "yyyy MM dd HH mm ss fff".</summary>
        public string formattedTime;
        public SerializableVector3 head_origin;
        public SerializableVector3 head_forward;
        public SerializableVector3 head_rotation;
        public SerializableVector3 eyeRayOrigin;
        public SerializableVector3 eyeRayDirection;
        public T cursorData;
        public SerializableVector3 target_position;
        public float cursor_angular_distance;
        public int start_num;
        public int end_num;
        public int step_num;
        public int sample_seq;
        /// <summary>Collider name under cursor ray, or "None".</summary>
        public string hit_target;
        /// <summary>Active end-target dwell accumulator (seconds).</summary>
        public float current_dwell_time;


        public FrameData(
            float _timestamp,
            long _unixTimeMilliseconds,
            string _formattedTime,
            StudyDesign.FittsLaw currentFittsLaw,
            Transform _head,
            T _cursorData,
            Vector3 _target_position,
            float _cursor_angular_distance,
            string _hitTarget,
            float _current_dwell_time,
            int _sample_seq = 0)
        {
            timestamp = _timestamp;
            unixTimeMilliseconds = _unixTimeMilliseconds;
            formattedTime = _formattedTime;
            head_origin = _head.position;
            head_forward = _head.forward;
            head_rotation = _head.rotation.eulerAngles;
            cursorData = _cursorData;
            target_position = _target_position;

            cursor_angular_distance = _cursor_angular_distance;
            start_num = currentFittsLaw.startNum;
            end_num = currentFittsLaw.endNum;
            step_num = currentFittsLaw.stepNum;
            sample_seq = _sample_seq;
            hit_target = _hitTarget;
            current_dwell_time = _current_dwell_time;
        }
    }

    [System.Serializable]
    public class TrialRecording<T>
    {
        public string file_name;
        public int sub_num;
        public int subsub_num;
        public string study_phase;
        public string study_type;
        public int total_rep;
        public string condition;
        public int repetition;
        public float trial_duration_sec;
        public float log_sample_rate_hz;
        public List<FrameData<T>> data;
    }

    [System.Serializable]
    public class TrialData<T>
    {
        public string file_name;
        public int sub_num;
        public int subsub_num;
        public float log_sample_rate_hz;
        public List<FrameData<T>> data;
        Study experiment;
        public TrialData(Study _experiment)
        {
            experiment = _experiment;

            sub_num = _experiment.sub_num;
            subsub_num = _experiment.subsub_num;
            file_name = SetFileName();
            data = new List<FrameData<T>>();
        }
        public string SetFileName()
        {
            //string builtFileName = $"subject{experiment.sub_num}_posture{GameManager.instance.currentPostureType}_cursor{GameManager.instance.currentCursorType.ToString()}_repetition{experiment.currentRepetition}_{GetCurrentDateTime()}";
            string builtFileName = $"subject{sub_num}_subsubNum{subsub_num}_cursor{experiment.currentCursor}_Selection{experiment.currentSelection}_repetition{experiment.currentRep}_{GetCurrentDateTime()}";
            return builtFileName;
        }
        public string GetCurrentDateTime()
        {
            DateTime now = DateTime.Now;
            string currentTime = $"{now.Year}{now.Month}{now.Day}{now.Hour}{now.Minute}{now.Second}";
            return currentTime;
        }
        public void Add(FrameData<T> _timeline)
        {
            data.Add(_timeline);
        }
        public void Clear()
        {
            file_name = string.Empty;
            sub_num = -1;
            data.Clear();
        }

        public void SaveDataJson() //TODO
        {
            file_name += string.Join("", experiment.fittsLaw.success_record);
            var envelope = new TrialRecording<T>
            {
                file_name = file_name,
                sub_num = sub_num,
                subsub_num = subsub_num,
                study_phase = "main",
                study_type = experiment.currentStudyType.ToString(),
                total_rep = experiment.TOTAL_REP,
                condition = experiment.currentCondition.ToString(),
                repetition = experiment.currentRep,
                trial_duration_sec = experiment.trialDurationSec,
                log_sample_rate_hz = log_sample_rate_hz,
                data = data,
            };
            string json = JsonConvert.SerializeObject(envelope, Formatting.Indented);
            string path = string.Format("{0}/{1}-{2}/", Application.persistentDataPath, sub_num, subsub_num);
            byte[] byteData = Encoding.UTF8.GetBytes(json);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            //if (!UnityEngine.Windows.Directory.Exists(path))
            //{
            //    UnityEngine.Windows.Directory.CreateDirectory(path);
            //}
            //UnityEngine.Windows.File.WriteAllBytes(path + file_name + ".json", byteData);
            File.WriteAllText(path + file_name + ".json", json);// CHECK!!
            Debug.Log(path + file_name + ".json" + " Saved");
        }
    }


    [System.Serializable]
    public struct SerializableVector3
    {
        /// <summary>
        /// x component
        /// </summary>
        public float x;

        /// <summary>
        /// y component
        /// </summary>
        public float y;

        /// <summary>
        /// z component
        /// </summary>
        public float z;

        [JsonIgnore]
        public Vector3 UnityVector
        {
            get
            {
                return new Vector3(x, y, z);
            }
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="rX"></param>
        /// <param name="rY"></param>
        /// <param name="rZ"></param>
        public SerializableVector3(float rX, float rY, float rZ)
        {
            x = rX;
            y = rY;
            z = rZ;
        }

        /// <summary>
        /// Returns a string representation of the object
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return System.String.Format("[{0}, {1}, {2}]", x, y, z);
        }

        /// <summary>
        /// Automatic conversion from SerializableVector3 to Vector3
        /// </summary>
        /// <param name="rValue"></param>
        /// <returns></returns>
        public static implicit operator Vector3(SerializableVector3 rValue)
        {
            return new Vector3(rValue.x, rValue.y, rValue.z);
        }

        /// <summary>
        /// Automatic conversion from Vector3 to SerializableVector3
        /// </summary>
        /// <param name="rValue"></param>
        /// <returns></returns>
        public static implicit operator SerializableVector3(Vector3 rValue)
        {
            return new SerializableVector3(rValue.x, rValue.y, rValue.z);
        }
    }

}

