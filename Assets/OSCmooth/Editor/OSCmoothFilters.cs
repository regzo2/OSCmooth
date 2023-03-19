using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSCTools.OSCmooth
{
    public static class OSCmoothFilters
    {
        public static readonly string[] BlackList =
        {
            "OSCm/", "IsLocal", "Smooth", "Proxy", "Proxy/", "_Float", "_Normalizer", "_FTI", "OSCm_BlendSet", "BlendSet", "Blend", "Binary"
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
