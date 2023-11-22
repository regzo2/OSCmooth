using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using OSCTools.OSCmooth.Types;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OSCTools.OSCmooth.Util
{
    public class AnimUtil
    {
        public AnimatorController _animatorController;
        private Object[] _animatorAssets;
        private string _animatorGUID;
        private string _animatorPath;
        private string _binaryPath;
        private string _smoothPath;

        public AnimUtil (AnimatorController animatorController, string binaryPath, string smoothnessPath)
        {
            _binaryPath = binaryPath;
            _smoothPath = smoothnessPath;
            _animatorController = animatorController;
            _animatorPath = AssetDatabase.GetAssetPath(_animatorController);
            _animatorAssets = AssetDatabase.LoadAllAssetsAtPath(_animatorPath);
            _animatorGUID = AssetDatabase.AssetPathToGUID(_animatorPath);
        }
        public static string NameNoSymbol(string name)
        {
            string nameNoSym = "";

            for (int j = 0; j < name.Length; j++)
            {
                if (name[j] != '/')
                {
                    nameNoSym += name[j];
                }

            }
            return nameNoSym;
        }

        public void CleanAnimatorBlendTreeBloat(string filter)
        {
            foreach (Object asset in _animatorAssets)
            {
                if (asset?.GetType() == typeof(BlendTree) && asset != null)
                {
                    if (((BlendTree)asset).name.Contains(filter))
                    {
                        Object.DestroyImmediate(asset, true);
                    }
                }
            }
        }

        public void RenameAllStateMachineInstancesOfBlendParameter(string initParameter, string newParameter)
        {
            foreach (Object asset in _animatorAssets)
            {
                switch (asset)
                {
                    case BlendTree blendTree:
                        if (blendTree.blendParameter == initParameter)
                            blendTree.blendParameter = newParameter;

                        if (blendTree.blendParameterY == initParameter)
                            blendTree.blendParameterY = newParameter;

                        for (int i = 0; i < blendTree.children.Length; i++)
                        {
                            var child = blendTree.children[i];
                            if (child.directBlendParameter == initParameter)
                                child.directBlendParameter = newParameter;
                        }
                        break;

                    case AnimatorState animatorState:
                        if (animatorState.timeParameter == initParameter)
                            animatorState.timeParameter = newParameter;

                        if (animatorState.speedParameter == initParameter)
                            animatorState.speedParameter = newParameter;

                        if (animatorState.cycleOffsetParameter == initParameter)
                            animatorState.cycleOffsetParameter = newParameter;

                        if (animatorState.mirrorParameter == initParameter)
                            animatorState.mirrorParameter = newParameter;
                        break;
                }
            }
        }
        
        public List<string> GetAllStateMachineParameters()
        {
            List<string> stateParams = new List<string>();

            foreach (Object asset in _animatorAssets)
            {
                if (asset is BlendTree blendTree)
                {
                    AddParameter(blendTree.blendParameter, stateParams);
                    AddParameter(blendTree.blendParameterY, stateParams);

                    for (int i = 0; i < blendTree.children.Length; i++)
                    {
                        AddParameter(blendTree.children[i].directBlendParameter, stateParams);
                    }

                    continue;
                }

                if (asset is AnimatorState animatorState)
                {
                    AddParameter(animatorState.timeParameter, stateParams);
                    AddParameter(animatorState.speedParameter, stateParams);
                    AddParameter(animatorState.cycleOffsetParameter, stateParams);
                    AddParameter(animatorState.mirrorParameter, stateParams);
                }
            }

            return stateParams;
        }

        private static void AddParameter(string parameter, List<string> stateParams)
        {
            if (!string.IsNullOrEmpty(parameter)) 
                stateParams.Add(parameter);
        }

        public void RevertStateMachineParameters()
        {
            string[] stateParams = GetAllStateMachineParameters().ToArray();
            int i = 0;
            foreach (var oscmParam in OSCmoothFilters.ParameterExtensions)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Removing Smoothing Direct BlendTree", (float)i++/oscmParam.Count());
                foreach (var stateParam in stateParams)
                {
                    if (stateParam.Contains(oscmParam))
                    {
                        RenameAllStateMachineInstancesOfBlendParameter(stateParam, stateParam.Replace(oscmParam, ""));
                    }
                }
            }
            EditorUtility.ClearProgressBar();
        }

        public void RemoveExtendedParametersInController(string name)
        {
            for (int i = 0; i < _animatorController.parameters.Length;)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Removing Extra Parameters.", (float)i/_animatorController.parameters.Length);
                if (_animatorController.parameters[i].name.Contains(name))
                {
                    _animatorController.RemoveParameter(i);
                    continue;
                }
                i++;
            }
            EditorUtility.ClearProgressBar();
        }

        public void RemoveContainingLayersInController(string name)
        {
            for (int i = 0; i < _animatorController.layers.Length;)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Removing Animation Layers.", (float)i/_animatorController.layers.Length);
                if (_animatorController.layers[i].name.Contains(name))
                {
                    _animatorController.RemoveLayer(i);
                    continue;
                }
                i++;
            }
            EditorUtility.ClearProgressBar();
        }

        public AnimatorControllerLayer CreateAnimLayerInController(string layerName)
        {
            for (int i = 0; i < _animatorController.layers.Length; i++)
            {
                if (_animatorController.layers[i].name == layerName)
                {
                    _animatorController.RemoveLayer(i);
                }
            }

            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = new AnimatorStateMachine
                {
                    name = layerName,
                    hideFlags = HideFlags.HideInHierarchy
                },
                defaultWeight = 1f
            };


            // Store Layer into Animator Controller, as creating a Layer object is not serialized unless we store it inside an asset.
            if (_animatorPath != string.Empty)
            {
                AssetDatabase.AddObjectToAsset(layer.stateMachine, _animatorPath);
            }

            _animatorController.AddLayer(layer);

            return layer;
        }

        public AnimationClip[] CreateFloatSmootherAnimation(string paramName, 
                                                            string smoothSuffix, 
                                                            string proxyPrefix, 
                                                            string directory, 
                                                            float initThreshold = -1, 
                                                            float finalThreshold = 1)
        {
            string baseName = NameNoSymbol(paramName);
            string initAssetPath = directory + baseName + "-1" + smoothSuffix + "_" + _animatorGUID + ".anim";
            string finalAssetPath = directory + baseName + "1" + smoothSuffix + "_" + _animatorGUID + ".anim";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var curvesInit = new AnimationCurve(new Keyframe(0.0f, initThreshold));
            var curvesFinal = new AnimationCurve(new Keyframe(0.0f, finalThreshold));

            var _animationClipInit = FindOrCreateAnimationClip(initAssetPath, paramName, curvesInit);
            var _animationClipFinal = FindOrCreateAnimationClip(finalAssetPath, paramName, curvesFinal);

            _animationClipInit.SetCurve("", typeof(Animator), proxyPrefix + paramName, curvesInit);
            _animationClipFinal.SetCurve("", typeof(Animator), proxyPrefix + paramName, curvesFinal);
            
            return new AnimationClip[] { _animationClipInit, _animationClipFinal };
        }

        public BlendTree CreateSmoothingBlendTree(float smoothness,
                                                  string paramName,
                                                  string prefix)
        {
            var proxyPrefix = "OSCm/Proxy/";
            var smoothSuffix = "Smoother";

            ParameterUtil.CheckAndCreateParameter(prefix + paramName + smoothSuffix, _animatorController, AnimatorControllerParameterType.Float, smoothness);
            ParameterUtil.CheckAndCreateParameter(proxyPrefix + paramName, _animatorController, AnimatorControllerParameterType.Float);
            ParameterUtil.CheckAndCreateParameter(paramName, _animatorController, AnimatorControllerParameterType.Float);

            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = prefix + paramName + "Smoother",
                name = "OSCm_" + paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = paramName,
                name = "OSCm_Input",
                useAutomaticThresholds = false
            };
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = proxyPrefix + paramName,
                name = "OSCm_Driver",
                useAutomaticThresholds = false
            };

            // Create smoothing anims
            AnimationClip[] driverAnims = CreateFloatSmootherAnimation(paramName, smoothSuffix, proxyPrefix, _smoothPath);

            rootTree.AddChild(falseTree, 1f);
            rootTree.AddChild(trueTree, 0f);

            falseTree.AddChild(driverAnims[0], -1f);
            falseTree.AddChild(driverAnims[1], 1f);

            trueTree.AddChild(driverAnims[0], -1f);
            trueTree.AddChild(driverAnims[1], 1f);

            var controllerPath = _animatorPath;

            AssetDatabase.AddObjectToAsset(rootTree, controllerPath);
            AssetDatabase.AddObjectToAsset(falseTree, controllerPath);
            AssetDatabase.AddObjectToAsset(trueTree, controllerPath);

            return rootTree;
        }

        public BlendTree CreateBinaryBlendTree(string paramName, int binarySizeSelection, bool combinedParameter)
        {
            // Create each binary step decode layer. Expression Parameters are bools and are implicitly cast as floats in the animator.
            // This creates one monolithic animation layer with all of the binary conversion logic.

            var blendRootPara = "OSCm/BlendSet";
            if (combinedParameter)
            {
                ParameterUtil.CheckAndCreateParameter(paramName + "Negative", _animatorController, AnimatorControllerParameterType.Float);
                blendRootPara = paramName + "Negative";
            }

            BlendTree decodeBinaryRoot = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = blendRootPara,
                name = "Binary_" + paramName + "_Root",
                useAutomaticThresholds = false
            };

            // Go through binary steps and create each child to eventually stuff into the Direct BlendTrees.
            for (int sign = combinedParameter ? -1 : 1; sign <= 1; sign += 2)
            {
                BlendTree decodeBinaryChildTree = new BlendTree
                {
                    blendType = BlendTreeType.Direct,
                    hideFlags = HideFlags.HideInHierarchy,
                    name = "Binary_" + paramName + "_Positive",
                    useAutomaticThresholds = false
                };

                List<ChildMotion> childBinaryDecode = new List<ChildMotion>();

                for (int i = 0; i < binarySizeSelection; i++)
                {
                    var decodeBinaryPositive = CreateBinaryDecode(paramName, _binaryPath, i, binarySizeSelection, sign >= 0);

                    childBinaryDecode.Add(new ChildMotion
                    {
                        directBlendParameter = "OSCm/BlendSet",
                        motion = decodeBinaryPositive,
                        timeScale = 1
                    });
                }
                decodeBinaryChildTree.children = childBinaryDecode.ToArray();
                decodeBinaryRoot.AddChild(decodeBinaryChildTree, combinedParameter ? 1f : 0f);
                AssetDatabase.AddObjectToAsset(decodeBinaryChildTree, _animatorPath);
            }

            AssetDatabase.AddObjectToAsset(decodeBinaryRoot, _animatorPath);

            return decodeBinaryRoot;
        }

        public AnimationClip FindOrCreateAnimationClip(string directory, string paramName, AnimationCurve curve)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(directory);

            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, directory);
            }
            else clip.ClearCurves();

            clip.SetCurve("", typeof(Animator), paramName, curve);

            return clip;
        }

        public AnimationClip[] CreateBinaryAnimation(string paramName, string directory, float weight, int step)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var trueNamePath = directory + NameNoSymbol(paramName) + "_True_" + step.ToString() + weight + _animatorGUID + ".anim";
            var falseNamePath = directory + NameNoSymbol(paramName) + "_False_" + step.ToString() + weight + _animatorGUID + ".anim";

            var _trueClip = FindOrCreateAnimationClip(trueNamePath, paramName, new AnimationCurve(new Keyframe(0.0f, 0.0f)));
            var _falseClip = FindOrCreateAnimationClip(falseNamePath, paramName, new AnimationCurve(new Keyframe(0.0f, weight)));

            return new AnimationClip[] { _trueClip, _falseClip };
        }

        public BlendTree CreateBinaryDecode(string paramName, string directory, int binaryPow, int binarySize, bool negative)
        {
            int binaryPowValue = (int)Mathf.Pow(2, binaryPow);
            ParameterUtil.CheckAndCreateParameter(paramName + binaryPowValue, _animatorController, AnimatorControllerParameterType.Float);

            BlendTree decodeBinary = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = paramName + binaryPowValue,
                name = "Binary_" + paramName + "_Decode_" + binaryPowValue,
                useAutomaticThresholds = false
            };

            // Create Decode anims and weight per binary
            AnimationClip[] decodeAnims = CreateBinaryAnimation(paramName, directory, (negative ? -0.5f : 0.5f) * binaryPowValue / ((int)Mathf.Pow(2, binarySize) - 1f), binaryPow);
            decodeBinary.AddChild(decodeAnims[0], 0f);
            decodeBinary.AddChild(decodeAnims[1], 1f);

            AssetDatabase.AddObjectToAsset(decodeBinary, _animatorPath);

            return decodeBinary;
        }
    }
}