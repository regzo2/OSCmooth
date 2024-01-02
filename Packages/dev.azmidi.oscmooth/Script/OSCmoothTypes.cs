using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace OSCmooth.Types
{
    [Serializable]
    public class OSCmoothSetup
    {
        public List<OSCmoothParameter> parameters = new List<OSCmoothParameter>();
        public OSCmoothParameter configParam = new OSCmoothParameter();
        public bool useBinaryEncoding = true;
        public bool useBinaryDecoding = true;
    }

    [Serializable]
    public class OSCmoothParameter
    {
        // Default values
        public float localSmoothness = 0.1f;
        public float remoteSmoothness = 0.7f;
        public string paramName = "NewParam";
        public AnimLayerTypeMask layerMask;
        public bool convertToProxy = true;
        public int binarySizeSelection = 0;
        public bool binaryNegative = false;
        public bool binaryEncoding = false;
        public bool isVisible = false;
    }

    [Serializable]
    [Flags]
    public enum AnimLayerTypeMask
    {
        Base = 1,
        Deprecated0 = 2,
        Additive = 4,
        Gesture = 8,
        Action = 16,
        FX = 32,
        Sitting = 64,
        TPose = 128,
        IKPose = 256
    }

    public static class Extensions
    {
        public static AnimLayerTypeMask Mask(this AnimLayerType type) =>
            (AnimLayerTypeMask)(1 << (int)type);

        public static bool Contains(this AnimLayerTypeMask mask, AnimLayerType type) =>
            (mask & GetMaskForType(type)) != 0;

        private static AnimLayerTypeMask GetMaskForType(AnimLayerType type)
        {
            switch (type)
            {
                case AnimLayerType.Base:
                    return AnimLayerTypeMask.Base;
                case AnimLayerType.Deprecated0:
                    return AnimLayerTypeMask.Deprecated0;
                case AnimLayerType.Additive:
                    return AnimLayerTypeMask.Additive;
                case AnimLayerType.Gesture:
                    return AnimLayerTypeMask.Gesture;
                case AnimLayerType.Action:
                    return AnimLayerTypeMask.Action;
                case AnimLayerType.FX:
                    return AnimLayerTypeMask.FX;
                case AnimLayerType.Sitting:
                    return AnimLayerTypeMask.Sitting;
                case AnimLayerType.TPose:
                    return AnimLayerTypeMask.TPose;
                case AnimLayerType.IKPose:
                    return AnimLayerTypeMask.IKPose;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}

