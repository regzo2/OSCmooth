using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tools.OSCmooth.Types
{
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