using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSCmooth
{
    public static class Filters
    {
        public static readonly string oscmPrefix = "OSCm/";
        public static readonly string proxyPrefix = "Proxy/";
        public static readonly string binaryPrefix = "Binary/";
        public static readonly string remotePrefix = "Remote/";
        public static readonly string localPrefix = "Local/";
        public static readonly string blendSuffix = "BlendSet";
        public static readonly string smootherSuffix = "Smoother";
        public static readonly string binaryNegativeSuffix = "Negative";
        public static readonly string[] BlackList =
        {
            "OSCm/", "IsLocal", "Smooth", "Proxy", "Proxy/", "_Float", "_Normalizer", "_FTI", "OSCm_BlendSet", "BlendSet", "Blend", "Binary/"
        };
        public static readonly string[] AllLayerNames =
        {
            "_OSCmooth_Smoothing_WD_Gen", "_OSCmooth_Smoothing_Gen"
        };
        public static readonly string[] ParameterExtensions =
        {
            "OSCm/Proxy/", "OSCm_Proxy"
        };
    }
}
