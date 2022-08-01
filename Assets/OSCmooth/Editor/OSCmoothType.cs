using System;
using System.Collections.Generic;
using UnityEngine;

namespace OSCTools.OSCmooth.Types
{
    [Serializable]
    public class OSCmoothParameter
    {

        // for Animation creation purposes:
        public float localSmoothness;
        public float remoteSmoothness;
        public string paramName;

        // This setting sets the output to controll the
        // base parameter. This is useful if an OSC app
        // doesn't need to directly control the base parameter,
        // such as VRCFaceTracking binary parameters.
        public bool flipInputOutput;

        // This will convert all instances of the base parameter with the 
        // Proxy version of the parameter. This could potentially be destructive
        // to the animator so be careful.
        public bool convertToProxy;

        // for Editor window visibility
        public bool isVisible;

        public OSCmoothParameter()
        {
            localSmoothness = 0.5f;
            remoteSmoothness = 0.9f;
            paramName = "NewParam";
            convertToProxy = true;
            flipInputOutput = false;
        }
    }

    [Serializable]
    public class OSCmoothLayer : ScriptableObject
    {
        public List<OSCmoothParameter> parameters;

        public OSCmoothLayer() 
        {
            parameters = new List<OSCmoothParameter>();
        }
        public OSCmoothLayer(List<OSCmoothParameter> parameters)
        {
            this.parameters = parameters;
        }
    }
}

