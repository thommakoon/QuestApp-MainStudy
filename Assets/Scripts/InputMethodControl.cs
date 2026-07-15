
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace InputMethod
{
    public class Control
    {
        public enum CursorType { Head, Hand, Eye }
        public void SetPokeOnly()
        {
            //CoreServices.InputSystem.EyeGazeProvider.IsEyeTrackingEnabled = false;
            //PointerUtils.SetGazePointerBehavior(PointerBehavior.AlwaysOff);

            //PointerUtils.SetHandPokePointerBehavior(PointerBehavior.AlwaysOn, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            //PointerUtils.SetHandGrabPointerBehavior(PointerBehavior.AlwaysOff, Handedness.Any);
            //PointerUtils.SetHandRayPointerBehavior(PointerBehavior.AlwaysOff, Handedness.Any);
        }

        public void DisablePokeCursor()
        {
            //PointerUtils.SetHandPokePointerBehavior(PointerBehavior.AlwaysOff, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            //Debug.Log("Disable Poke Cursor");
        }
        public void EnablePokeCursor()
        {
            //PointerUtils.SetHandPokePointerBehavior(PointerBehavior.AlwaysOn, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
        }
       
        public void SetHandRayPointer()
        {

            //CoreServices.InputSystem.EyeGazeProvider.IsEyeTrackingEnabled = false;
            //PointerUtils.SetGazePointerBehavior(PointerBehavior.AlwaysOff);
            ////GameManager.instance.SendTCPMessage("SET HAND POINTER");

            ////PointerUtils.SetHandPokePointerBehavior(PointerBehavior.AlwaysOff, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            //PointerUtils.SetHandGrabPointerBehavior(PointerBehavior.AlwaysOff, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            //PointerUtils.SetHandRayPointerBehavior(PointerBehavior.AlwaysOn, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            ////GameManager.instance.SetCurrentCursor(CursorType.HAND);
        }
        public void SetEyePointer()
        {
            //Debug.Log("Set Eye Pointer");

            //CoreServices.InputSystem.EyeGazeProvider.IsEyeTrackingEnabled = true;
            //PointerUtils.SetGazePointerBehavior(PointerBehavior.AlwaysOn);

            //CoreServices.InputSystem.EyeGazeProvider.Enabled = true;
            ////GameManager.instance.SendTCPMessage("SET EYE POINTER");



            ////PointerUtils.SetHandPokePointerBehavior(PointerBehavior.AlwaysOff, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            //PointerUtils.SetHandGrabPointerBehavior(PointerBehavior.AlwaysOff, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            //PointerUtils.SetHandRayPointerBehavior(PointerBehavior.AlwaysOff, Microsoft.MixedReality.Toolkit.Utilities.Handedness.Any);
            ////GameManager.instance.SetCurrentCursor(CursorType.EYE);
            ////CheckEyeCalibration();
        }
    }
}

