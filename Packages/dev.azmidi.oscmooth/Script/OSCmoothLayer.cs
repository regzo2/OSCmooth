using System;
using System.Collections.Generic;
using UnityEngine;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace Tools.OSCmooth.Types
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
}

