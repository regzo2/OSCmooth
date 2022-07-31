using System;

namespace OSCTools.OSCmooth.Types
{
    [Serializable]
    public class OSCmoothParameter
    {

        // for Animation creation purposes:
        public float localSmoothness;
        public float remoteSmoothness;
        public string paramName;

        // This setting renames any instance of the base parameter
        // in the animator with the Proxy parameter. This is intended
        // for total conversion between the base and Proxy parameters,
        // after conversion it should be expected to use the Proxy parameter 
        // in future animations and this will act as a 'one-time' use switch.
        // Ideally it will not flip any existing
        public bool flipInputOutput;

        // for EditorWindow purposes: This is intended to hide parameter settings.
        public bool isVisible;

        public OSCmoothParameter()
        {
            localSmoothness = 0.5f;
            remoteSmoothness = 0.9f;
            paramName = "New Parameter";
            isVisible = false;
            flipInputOutput = false;
        }
    }

    // For JSONUtil purposes
    [Serializable]
    public class OSCmoothLayer
    {
        public OSCmoothParameter[] parameters;

        public OSCmoothLayer(OSCmoothParameter[] parameters)
        {
            this.parameters = parameters;
        }
    }
}

