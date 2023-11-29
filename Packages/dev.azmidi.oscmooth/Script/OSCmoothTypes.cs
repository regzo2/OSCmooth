using System;
using System.Collections.Generic;
using UnityEngine;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace OSCmooth.Types
{
    [Serializable]
    public class OSCmoothLayers : ScriptableObject
    {
        public List<OSCmoothLayer> layers = new List<OSCmoothLayer>();
        public OSCmoothParameter configParam = new OSCmoothParameter();
    }

    [Serializable]
    public class OSCmoothLayer
    {
        public List<OSCmoothParameter> parameters = new List<OSCmoothParameter>();
        public CustomAnimLayer associate;

        public OSCmoothLayer(CustomAnimLayer associate)
        {
            this.associate = associate;
        }
    }

    [Serializable]
    public class OSCmoothParameter
    {
        // Default values
        public float localSmoothness = 0.1f;
        public float remoteSmoothness = 0.7f;
        public string paramName = "NewParam";
        public bool convertToProxy = true;
        public int binarySizeSelection = 0;
        public bool combinedParameter = false;
        public bool isVisible = false;
    }
}

