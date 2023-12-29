using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using OSCmooth.Types;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OSCmooth.Util;
using UnityEditor.Animations;
using System.IO;
using static OSCmooth.Filters;
using System.Collections.Immutable;
using System.Collections;
using VRC.SDK3.Avatars.Components;
using System.Reflection;

namespace OSCmooth.Editor.Animation
{
    public class OSCmoothAnimationHandler
    {
        private AnimatorController _animatorController;
        private string _animatorPath;
        private string _animatorGUID;
        private List<Object> _animatorAssets;

        private List<OSCmoothParameter> _parameters;
        private string _smoothExportDirectory;
        private string _binaryExportDirectory;
        private bool _saveAssetsToFiles;
        private bool _useEncoding;
        private HashSet<string> _existingParameters;
        private Dictionary<string, string> parameterRenameBatch = new Dictionary<string, string>();

        public OSCmoothAnimationHandler(List<OSCmoothParameter> parameters,
                                        AnimatorController animatorController, 
                                        string smoothExportDirectory,
                                        string binaryExportDirectory,
                                        bool useEncoding = false,
                                        bool saveAssetsToFiles = true)
        {
            _animatorController = animatorController;
            _animatorPath = AssetDatabase.GetAssetPath(_animatorController);
            _animatorAssets = AssetDatabase.LoadAllAssetsAtPath(_animatorPath)
                                           .Where(a => a != null && (a.GetType() == typeof(AnimatorState) || a.GetType() == typeof(BlendTree)))
                                           .ToList();
            Debug.Log($"Loaded {_animatorAssets.Count} assets in animator");
            _animatorGUID = AssetDatabase.AssetPathToGUID(_animatorPath);
            _parameters = parameters;
            _smoothExportDirectory = smoothExportDirectory;
            _binaryExportDirectory = binaryExportDirectory;
            _saveAssetsToFiles = saveAssetsToFiles;
            _existingParameters = new HashSet<string>(animatorController.parameters.Select((AnimatorControllerParameter p) => p.name));
            _useEncoding = useEncoding;
         }

        public void RemoveAllOSCmoothFromController()
        {
            CleanAnimatorBlendTreeBloat("OSCm");
            RevertStateMachineParameters();
            RemoveExtendedParametersInController("OSCm");
            RemoveContainingLayersInController("OSCm");
        }

        public void CreateLayer()
        {
            AssetDatabase.StartAssetEditing();

            AnimatorControllerLayer animLayer = CreateAnimLayerInController("_OSCmooth_Gen");
            var state = animLayer.stateMachine.AddState("OSCmooth", new Vector3(30, 170, 0));

            var rootBlend = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCmooth_Root",
                useAutomaticThresholds = false
            };

            List<BlendTree> _trees = new List<BlendTree>();
            foreach (var p in _parameters.Where(pa => pa.binaryEncoding))
                CreateBinaryEncoderLayer(p.paramName, p.binarySizeSelection, p.binaryNegative);
            if (_parameters.Any(p => p.binarySizeSelection > 0))
                _trees.Add(CreateBinaryDecoderLayer());
            _trees.Add(CreateSmoothLayer());

            var childs = new List<ChildMotion>();
            foreach (var tree in _trees)
            {
                childs.Add(new ChildMotion
                {
                    directBlendParameter = $"{oscmPrefix}{blendSuffix}",
                    motion = tree,
                    timeScale = 1
                });
            }

            rootBlend.children = childs.ToArray();

            state.motion = rootBlend;

            BatchRenameParameters();

            AssetDatabase.AddObjectToAsset(rootBlend, _animatorPath);

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        public BlendTree CreateSmoothLayer()
        {
            _animatorController.CreateParameter(_existingParameters, "IsLocal", AnimatorControllerParameterType.Float, false, false);

            var rootBlend = new BlendTree()
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCm_Smoother_Root",
                useAutomaticThresholds = false,
                blendParameter = "IsLocal"
            };

            var nameLocalWD = "OSCm_Local";
            var nameRemoteWD = "OSCm_Remote";

            var basisLocalBlendTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = nameLocalWD,
                useAutomaticThresholds = false

            };

            var basisRemoteBlendTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = nameRemoteWD,
                useAutomaticThresholds = false
            };

            // Creating a '1Set' parameter that holds a value of one at all times for the Direct BlendTree
            _animatorController.CreateParameter(_existingParameters, $"{oscmPrefix}{blendSuffix}", AnimatorControllerParameterType.Float, false, false, 1f);

            var localChildMotions = new List<ChildMotion>();
            var remoteChildMotions = new List<ChildMotion>();

            int i = 0;
            foreach (OSCmoothParameter p in _parameters)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Creating Smoothing Direct BlendTree", (float)i++/_parameters.Count);
                if (p.convertToProxy)
                {
                    parameterRenameBatch.Add(p.paramName, _useEncoding ? ParameterExtensions.Obfuscate($"{oscmPrefix}{proxyPrefix}{p.paramName}") : $"{oscmPrefix}{proxyPrefix}{p.paramName}");
                }

                var motionLocal = CreateParameterSmoothingBlendTree(p.localSmoothness, p.paramName, $"{oscmPrefix}{localPrefix}");
                var motionRemote = CreateParameterSmoothingBlendTree(p.remoteSmoothness, p.paramName, $"{oscmPrefix}{remotePrefix}");

                localChildMotions.Add(new ChildMotion
                {
                    directBlendParameter = $"{oscmPrefix}{blendSuffix}",
                    motion = motionLocal,
                    timeScale = 1
                });

                remoteChildMotions.Add(new ChildMotion
                {
                    directBlendParameter = $"{oscmPrefix}{blendSuffix}",
                    motion = motionRemote,
                    timeScale = 1,
                });
            }
            EditorUtility.ClearProgressBar();
            basisLocalBlendTree.children = localChildMotions.ToArray();
            basisRemoteBlendTree.children = remoteChildMotions.ToArray();

            rootBlend.AddChild(basisRemoteBlendTree, 0f);
            rootBlend.AddChild(basisLocalBlendTree, 1f);

            AssetDatabase.AddObjectToAsset(rootBlend, _animatorPath);
            AssetDatabase.AddObjectToAsset(basisRemoteBlendTree, _animatorPath);
            AssetDatabase.AddObjectToAsset(basisLocalBlendTree, _animatorPath);

            return rootBlend;
        }

        public BlendTree CreateBinaryDecoderLayer()
        {
            // Creating BlendTree objects to better customize them in the AC Editor         

            var binaryTreeRoot = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCm_Binary_Root",
                useAutomaticThresholds = false
            };

            // Creating a '1Set' parameter that holds a value of one at all times for the Direct BlendTree
            _animatorController.CreateParameter(_existingParameters, "OSCm/BlendSet", AnimatorControllerParameterType.Float, false, false, 1f);

            var childBinary = new List<ChildMotion>();

            // Go through each parameter and create each child to eventually stuff into the Direct BlendTrees. 
            int i = 0;
            foreach (OSCmoothParameter p in _parameters)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Creating Binary Parameter Direct BlendTree", (float)i++/_parameters.Count);
                if (p.binarySizeSelection == 0) continue;
                var decodeBinary = CreateBinaryDecoder(p.paramName, p.binarySizeSelection, p.binaryNegative);

                childBinary.Add(new ChildMotion
                {
                    directBlendParameter = $"{oscmPrefix}{blendSuffix}",
                    motion = decodeBinary,
                    timeScale = 1
                });
            }

            binaryTreeRoot.children = childBinary.ToArray();

            AssetDatabase.AddObjectToAsset(binaryTreeRoot, _animatorPath);

            return binaryTreeRoot;
        }

        public string NameNoSymbol(string name)
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
                        _animatorAssets.Remove(asset);
                    }
                }
            }
        }

        public void BatchRenameParameters()
        {
            foreach (Object asset in _animatorAssets)
            {
                if (asset == null) continue;
                switch (asset)
                {
                    case BlendTree blendTree:
                        if (parameterRenameBatch.TryGetValue(blendTree.blendParameter, out string _newBlend))
                            blendTree.blendParameter = _newBlend;

                        if (parameterRenameBatch.TryGetValue(blendTree.blendParameterY, out string _newBlendY))
                            blendTree.blendParameterY = _newBlendY;

                        var _children = blendTree.children;
                        for (int i = 0; i < blendTree.children.Length; i++)
                        {
                            if (parameterRenameBatch.TryGetValue(_children[i].directBlendParameter, out string _newDirectParameter))
                                _children[i].directBlendParameter = _newDirectParameter;
                        }
                        blendTree.children = _children;
                        break;

                    case AnimatorState animatorState:
                        if (parameterRenameBatch.TryGetValue(animatorState.timeParameter, out string _newTime))
                            animatorState.timeParameter = _newTime;

                        if (parameterRenameBatch.TryGetValue(animatorState.speedParameter, out string _newSpeed))
                            animatorState.speedParameter = _newSpeed;

                        if (parameterRenameBatch.TryGetValue(animatorState.cycleOffsetParameter, out string _newOffset))
                            animatorState.cycleOffsetParameter = _newOffset;

                        if (parameterRenameBatch.TryGetValue(animatorState.mirrorParameter,out string _newMirror))
                            animatorState.mirrorParameter = _newMirror;
                        break;
                }
            }
        }
        
        public List<string> GetAllStateMachineParameters()
        {
            List<string> stateParams = new List<string>();

            foreach (Object asset in _animatorAssets)
            {
                switch (asset)
                {
                    case BlendTree blendTree:
                        AddParameter(blendTree.blendParameter, stateParams);
                        AddParameter(blendTree.blendParameterY, stateParams);

                        for (int i = 0; i < blendTree.children.Length; i++)
                        {
                            AddParameter(blendTree.children[i].directBlendParameter, stateParams);
                        }

                        break;

                    case AnimatorState animatorState:
                        AddParameter(animatorState.timeParameter, stateParams);
                        AddParameter(animatorState.speedParameter, stateParams);
                        AddParameter(animatorState.cycleOffsetParameter, stateParams);
                        AddParameter(animatorState.mirrorParameter, stateParams);
                        break;
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
            foreach (var oscmParam in Filters.ParameterNames)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Removing Smoothing Direct BlendTree", (float)i++/oscmParam.Count());
                foreach (var stateParam in stateParams)
                {
                    if (stateParam.Contains(oscmParam))
                    {
                        parameterRenameBatch.Add(stateParam, stateParam.Replace(oscmParam, ""));
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

        public AnimatorControllerLayer CreateAnimLayerInController(string layerName, float defaultWeight = 1f)
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
                defaultWeight = defaultWeight
            };


            // Store Layer into Animator Controller, as creating a Layer object is not serialized unless we store it inside an asset.
            AssetDatabase.AddObjectToAsset(layer.stateMachine, _animatorPath);

            _animatorController.AddLayer(layer);

            return layer;
        }

        public void CreateBinaryEncoderLayer(string paramName, int binarySizeSelection, bool combinedParameter)
        {
            var _layer = CreateAnimLayerInController($"_OSCm_{paramName}_Encode", 1f);

            _animatorController.CreateParameter(_existingParameters, paramName, AnimatorControllerParameterType.Float, true, false);

            int binaryRes = (int)Mathf.Pow(2, binarySizeSelection);
            int binaryStates = binaryRes;

            for (int i = 0; i < binaryStates; i++)
            {
                var _state = _layer.stateMachine.AddState($"{paramName}{i}", new Vector3(400f, (i - (binaryStates/2)) * 40, 0f));
                _state.speed = 10000f;

                var _paramDriver = new VRCAvatarParameterDriver();

                var _transition = _layer.stateMachine.AddEntryTransition(_state);
                _transition.AddCondition(AnimatorConditionMode.Greater, ((float)i/(float)binaryStates) - 0.0001f, paramName);
                if (i != binaryStates - 1)
                    _transition.AddCondition(AnimatorConditionMode.Less, ((float)(i+1)/(float)binaryStates) + 0.0001f, paramName);

                var _exit = _state.AddExitTransition(true);
                _exit.exitTime = 0f;
                _exit.duration = 0f;

                if (combinedParameter)
                {
                    var _negativeTrans = _layer.stateMachine.AddEntryTransition(_state);
                _negativeTrans.AddCondition(AnimatorConditionMode.Less, ((float)-i/(float)binaryStates) + 0.0001f, paramName);
                if (i != binaryStates - 1)
                    _negativeTrans.AddCondition(AnimatorConditionMode.Greater, ((float)-(i+1)/(float)binaryStates) - 0.0001f, paramName);
                    var _parameter = new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                    {
                        source = paramName,
                        name = ParameterExtensions.Obfuscate($"{oscmPrefix}{binaryPrefix}{paramName}{binaryNegativeSuffix}"),
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy,
                        convertRange = true,
                        sourceMin = 0f,
                        sourceMax = -.0001f,
                        valueMin = 0f,
                        valueMax = 1f,
                        destMin = 0f,
                        destMax = 1f,
                    };
                    _paramDriver.parameters.Add(_parameter);
                }

                for (int j = 0; j < binarySizeSelection; j++) 
                {
                    Debug.Log($"i % (1 << j) : {(i % (1 << j))}");
                    _paramDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                    {
                        name = ParameterExtensions.Obfuscate($"{oscmPrefix}{binaryPrefix}{paramName}{1 << j}"),
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        value = ((i >> j) & 1) == 1 ? 1f : 0f
                    });
                }

                _state.behaviours = new StateMachineBehaviour[] { _paramDriver };
                AssetDatabase.AddObjectToAsset(_paramDriver, _animatorPath);
            }
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

            var _animationClipInit = FindOrCreateAnimationClip(initAssetPath, proxyPrefix + paramName, curvesInit);
            var _animationClipFinal = FindOrCreateAnimationClip(finalAssetPath, proxyPrefix + paramName, curvesFinal);

            return new AnimationClip[] { _animationClipInit, _animationClipFinal };
        }

        public BlendTree CreateParameterSmoothingBlendTree(float smoothness,
                                                           string paramName,
                                                           string prefix)
        {
            var proxyPrefix = "OSCm/Proxy/";
            var binaryProxyPrefix = "OSCm/Binary/Proxy/";
            var smoothSuffix = "Smoother";

            _animatorController.CreateParameter(_existingParameters, prefix + paramName + smoothSuffix, AnimatorControllerParameterType.Float, false, _useEncoding, smoothness);
            _animatorController.CreateParameter(_existingParameters, proxyPrefix + paramName, AnimatorControllerParameterType.Float, false, _useEncoding);
            _animatorController.CreateParameter(_existingParameters, binaryProxyPrefix + paramName, AnimatorControllerParameterType.Float, false, _useEncoding);

            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _useEncoding ? ParameterExtensions.Obfuscate(prefix + paramName + "Smoother") : prefix + paramName + "Smoother",
                name = "OSCm_" + paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _useEncoding ? ParameterExtensions.Obfuscate(binaryProxyPrefix + paramName) : binaryProxyPrefix + paramName,
                name = "OSCm_Input",
                useAutomaticThresholds = false
            };
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _useEncoding ? ParameterExtensions.Obfuscate(proxyPrefix + paramName) : proxyPrefix + paramName,
                name = "OSCm_Driver",
                useAutomaticThresholds = false
            };

            // Create smoothing anims
            AnimationClip[] driverAnims = CreateFloatSmootherAnimation(paramName, smoothSuffix, proxyPrefix, _smoothExportDirectory);

            rootTree.AddChild(falseTree, 0f);
            rootTree.AddChild(trueTree, 1f);

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

        public BlendTree CreateBinaryDecoder(string paramName, int binarySizeSelection, bool combinedParameter)
        {
            string _prefix = "OSCm/Binary/";

            var _rootParameter = "OSCm/BlendSet";
            if (combinedParameter)
            {
                _animatorController.CreateParameter(_existingParameters, _prefix + paramName + "Negative", AnimatorControllerParameterType.Float, true, _useEncoding);
                _rootParameter = _useEncoding ? ParameterExtensions.Obfuscate(_prefix + paramName + "Negative") : _prefix + paramName + "Negative";
            }

            BlendTree _decodeBinaryRoot = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _rootParameter,
                name = "OSCm_Binary_" + paramName + "_Root",
                useAutomaticThresholds = false
            };

            BlendTree _decodeBinaryPositiveTree = new BlendTree
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "Binary_" + paramName + "_Positive",
                useAutomaticThresholds = false
            };

            // Go through binary steps and create each child to eventually stuff into the Direct BlendTrees.
            List<ChildMotion> childBinaryPositiveDecode = new List<ChildMotion>();
            for (int i = 0; i < binarySizeSelection; i++)
            {
                var decodeBinaryPositive = CreateBinaryDecodeTree(paramName, _binaryExportDirectory, i, binarySizeSelection, false);

                childBinaryPositiveDecode.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = decodeBinaryPositive,
                    timeScale = 1
                });
            }
            _decodeBinaryPositiveTree.children = childBinaryPositiveDecode.ToArray();
            _decodeBinaryRoot.AddChild(_decodeBinaryPositiveTree, 0f);

            if (combinedParameter)
            {
                BlendTree _decodeBinaryNegativeTree = new BlendTree
                {
                    blendType = BlendTreeType.Direct,
                    hideFlags = HideFlags.HideInHierarchy,
                    name = "Binary_" + paramName + "_Negative",
                    useAutomaticThresholds = false
                };

                List<ChildMotion> childBinaryNegativeDecode = new List<ChildMotion>();
                for (int i = 0; i < binarySizeSelection; i++)
                {
                    var decodeBinaryNegative = CreateBinaryDecodeTree(paramName, _binaryExportDirectory, i, binarySizeSelection, true);

                    childBinaryNegativeDecode.Add(new ChildMotion
                    {
                        directBlendParameter = "OSCm/BlendSet",
                        motion = decodeBinaryNegative,
                        timeScale = 1
                    });
                }
                _decodeBinaryNegativeTree.children = childBinaryNegativeDecode.ToArray();
                AssetDatabase.AddObjectToAsset(_decodeBinaryNegativeTree, _animatorPath);
                _decodeBinaryRoot.AddChild(_decodeBinaryNegativeTree, 1f);
            }

            AssetDatabase.AddObjectToAsset(_decodeBinaryRoot, _animatorPath);
            return _decodeBinaryRoot;
        }

        public AnimationClip FindOrCreateAnimationClip(string directory, string paramName, AnimationCurve curve)
        {
            AnimationClip clip = null;
            if (!_saveAssetsToFiles)
            {
                clip = new AnimationClip();
                AssetDatabase.AddObjectToAsset(clip, _animatorPath);
            }
            else
            {
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(directory);

                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, directory);
                }
                else clip.ClearCurves();
            }

            clip.SetCurve("", typeof(Animator), _useEncoding ? ParameterExtensions.Obfuscate(paramName) : paramName, curve);

            return clip;
        }

        public AnimationClip[] CreateBinaryDecodeAnimation(string paramName, string directory, float weight, int step)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var trueNamePath = directory + NameNoSymbol(paramName) + "_True_" + step.ToString() + weight + _animatorGUID + ".anim";
            var falseNamePath = directory + NameNoSymbol(paramName) + "_False_" + step.ToString() + weight + _animatorGUID + ".anim";

            var _trueClip = FindOrCreateAnimationClip(trueNamePath, paramName, new AnimationCurve(new Keyframe(0.0f, 0.0f)));
            var _falseClip = FindOrCreateAnimationClip(falseNamePath, paramName, new AnimationCurve(new Keyframe(0.0f, weight)));

            return new AnimationClip[] { _trueClip, _falseClip };
        }

        public BlendTree CreateBinaryDecodeTree(string paramName, string directory, int binaryPow, int binarySize, bool negative)
        {
            string prefix = "OSCm/Binary/";
            var binaryProxyPrefix = "OSCm/Binary/Proxy/";
            float binaryPowValue = Mathf.Pow(2, binaryPow);
            _animatorController.CreateParameter(_existingParameters, prefix + paramName + binaryPowValue, AnimatorControllerParameterType.Float, true, _useEncoding);

            BlendTree decodeBinary = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _useEncoding ? ParameterExtensions.Obfuscate(prefix + paramName + (int)binaryPowValue) : prefix + paramName + (int)binaryPowValue,
                name = $"Binary_{paramName}_Decode_{(negative ? "Negative_" : "")}{binaryPowValue}",
                useAutomaticThresholds = false
            };

            // Create Decode anims and weight per binary
            AnimationClip[] decodeAnims = CreateBinaryDecodeAnimation(binaryProxyPrefix + paramName, 
                                                                      directory, (negative ? -1f : 1f) * binaryPowValue / (Mathf.Pow(2, binarySize) - 1f), 
                                                                      binaryPow);
            decodeBinary.AddChild(decodeAnims[0], 0f);
            decodeBinary.AddChild(decodeAnims[1], 1f);

            AssetDatabase.AddObjectToAsset(decodeBinary, _animatorPath);

            return decodeBinary;
        }
    }
}