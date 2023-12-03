using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor.Animations;
using OSCmooth.Types;
using static OSCmooth.Filters;

namespace OSCmooth.Util
{
    public static class ParameterExtensions
    {
        public static bool CreateExpressionParameters(this List<OSCmoothParameter> oscmParameters, VRCAvatarDescriptor avatarDescriptor, string directory)
        {
            int _oscmCost = ParameterCost(oscmParameters);
            int _descCost = CalcAvailableSpace(avatarDescriptor.expressionParameters);
            if (_oscmCost > _descCost)
            {
                EditorUtility.DisplayDialog("OSCmooth", $"OSCmooth parameters take up too much Expression Parameter space on your avatar ({_oscmCost} used / {_descCost} available). Reduce the parameter usage of OSCmooth to upload.", "OK");
                return false;
            }

            List<string> _floatParameters = new List<string>();
            List<string> _binaryParameters = new List<string>();

            foreach (OSCmoothParameter parameter in oscmParameters)
            {
                if (parameter.binarySizeSelection != 0 && (!parameter.binaryNegative || parameter.binarySizeSelection < 7))
                {
                    for (int binarySize = 0; binarySize < parameter.binarySizeSelection; binarySize++)
                    {
                        _binaryParameters.Add($"{parameter.paramName}{1 << binarySize}");
                    }
                    if (parameter.binaryNegative)
                    {
                        _binaryParameters.Add(parameter.paramName + "Negative");
                    }
                }
                else
                {
                    _floatParameters.Add(parameter.paramName);
                }
            }
            List<VRCExpressionParameters.Parameter> _vrcParameters = avatarDescriptor.expressionParameters.parameters.ToList();
            foreach (string floatName in _floatParameters)
            {
                _vrcParameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = floatName,
                    valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0f,
                    saved = false,
                    networkSynced = true
                });
            }
            foreach (string boolName in _binaryParameters)
            {
                _vrcParameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = $"{oscmPrefix}{binaryPrefix}{boolName}",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0f,
                    saved = false,
                    networkSynced = true
                });
            }

            var _expressionsPath = AssetDatabase.GetAssetPath(avatarDescriptor.expressionParameters);
            var _proxyPath = $"{directory}{avatarDescriptor.expressionParameters.name}Proxy.asset"; 
            AssetDatabase.CopyAsset(_expressionsPath, _proxyPath);
            AssetDatabase.SaveAssets();
            var _proxyExpressions = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(_proxyPath);
            _proxyExpressions.parameters = _vrcParameters.ToArray();

            avatarDescriptor.expressionParameters = _proxyExpressions;
            AssetDatabase.SaveAssets();
            return true;
        }

        public static int ParameterCost(this List<OSCmoothParameter> oscmParameters)
        {
            int _oscmUsage = 0;
            oscmParameters.ForEach(delegate (OSCmoothParameter p)
            {
                _oscmUsage += (p.binarySizeSelection > 0) ? p.binarySizeSelection : VRCExpressionParameters.TypeCost(VRCExpressionParameters.ValueType.Float);
            });
            return _oscmUsage;
        }

        public static void RemoveOSCmParameters(this VRCAvatarDescriptor avatarDescriptor, List<OSCmoothParameter> oscmParameters)
        {
            List<VRCExpressionParameters.Parameter> vrcParameters = avatarDescriptor.expressionParameters.parameters.ToList();
            List<VRCExpressionParameters.Parameter> toRemove = new List<VRCExpressionParameters.Parameter>();
            foreach (OSCmoothParameter oscmParameter in oscmParameters)
            {
                foreach (VRCExpressionParameters.Parameter vrcParameter in vrcParameters)
                {
                    if (vrcParameter.name.Contains(oscmParameter.paramName))
                    {
                        toRemove.Add(vrcParameter);
                    }
                }
            }
            avatarDescriptor.expressionParameters.parameters = vrcParameters.Except(toRemove).ToArray();
        }

        public static int CalcAvailableSpace(this VRCExpressionParameters parameters)
        {
            return (int)Mathf.Max(new float[2]
            {
            0f,
            256 - parameters.CalcTotalCost()
            });
        }

        public static AnimatorControllerParameter CheckAndCreateParameter(this AnimatorController animatorController, string paramName, AnimatorControllerParameterType type, float defaultVal = 0f)
        {
            HashSet<string> existingParameterNames = new HashSet<string>(animatorController.parameters.Select((AnimatorControllerParameter p) => p.name));
            if (existingParameterNames.Contains(paramName))
            {
                return animatorController.parameters.First((AnimatorControllerParameter p) => p.name == paramName);
            }
            AnimatorControllerParameter parameter = new AnimatorControllerParameter
            {
                name = paramName,
                type = type,
                defaultFloat = defaultVal,
                defaultInt = (int)defaultVal,
                defaultBool = Convert.ToBoolean(defaultVal)
            };
            animatorController.AddParameter(parameter);
            return parameter;
        }
    }
}
