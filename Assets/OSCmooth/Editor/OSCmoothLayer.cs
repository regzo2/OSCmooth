using System;
using System.Collections.Generic;
using UnityEngine;

namespace OSCTools.OSCmooth.Types
{
    [Serializable]
    public class OSCmoothLayer : ScriptableObject
    {
        public List<OSCmoothParameter> parameters;
        public OSCmoothParameter configuration;

        public OSCmoothLayer()
        {
            parameters = new List<OSCmoothParameter>();
            configuration = new OSCmoothParameter();
        }
        public OSCmoothLayer(List<OSCmoothParameter> parameters, OSCmoothParameter configuration)
        {
            this.parameters = parameters;
            this.configuration = configuration;
        }
    }
}

