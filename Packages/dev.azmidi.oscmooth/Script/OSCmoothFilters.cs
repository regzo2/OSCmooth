using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSCmooth
{
    public class OSCmNameManager
    {
        public static readonly string oscmPrefix = "OSCm/";
        public static readonly string proxyPrefix = "Proxy/";
        public static readonly string binaryPrefix = "Binary/";
        public static readonly string remotePrefix = "Remote/";
        public static readonly string localPrefix = "Local/";
        public static readonly string blendSuffix = "BlendSet";
        public static readonly string smootherPrefix = "Smooth/";
        public static readonly string binaryNegativeSuffix = "Negative";
        public static readonly string[] BlackList =
        {
            "OSCm/", "IsLocal", "Smooth", "Proxy", "Proxy/", "_Float", "_Normalizer", "_FTI", "OSCm_BlendSet", "BlendSet", "Blend", "Binary/"
        };
        public static readonly string[] AllLayerNames =
        {
            "_OSCmooth_Smoothing_WD_Gen", "_OSCmooth_Smoothing_Gen"
        };
        public static readonly string[] ParameterNames =
        {
            "OSCm/Proxy/", "OSCm_Proxy"
        };
        public bool _legacy;
        public OSCmNameManager(bool legacy = false)
        {
            _legacy = legacy;
        }

        public static string Shuffle(string str)
    {
            var list = new SortedList<int,char>();
            var rand = new Random(0);
            foreach (var c in str)
                list.Add(rand.Next(), c);
            return new string(list.Values.ToArray());
    }

        public string Binary(string parameter, int step) =>
              _legacy
            ? $"{oscmPrefix}{binaryPrefix}{parameter}{step}"
            : $"{oscmPrefix}{binaryPrefix}{step}/{Shuffle(parameter)}";
        public string BinaryNegative(string parameter) =>
            _legacy
            ? $"{oscmPrefix}{binaryPrefix}{parameter}{binaryNegativeSuffix}"
            : $"{oscmPrefix}{binaryPrefix}{binaryNegativeSuffix}/{Shuffle(parameter)}";
        public string BinaryProxy(string parameter) =>
            $"{oscmPrefix}{binaryPrefix}{proxyPrefix}{parameter}";
        public string Proxy(string parameter) =>
            $"{oscmPrefix}{proxyPrefix}{parameter}";
        public string Smoother(string parameter, bool remote) =>
            $"{oscmPrefix}{(remote ? remotePrefix : localPrefix)}{smootherPrefix}{parameter}";
        public string BlendSet() => $"{oscmPrefix}{blendSuffix}";

    }
}
