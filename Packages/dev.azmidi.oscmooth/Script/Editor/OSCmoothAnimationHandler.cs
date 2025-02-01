﻿using System.Collections.Generic;
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
using static OSCmooth.OSCmNameManager;
using System.Collections.Immutable;
using System.Collections;
using VRC.SDK3.Avatars.Components;
using System.Reflection;
using VRC.SDKBase;
using System;
using Object = UnityEngine.Object;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

namespace OSCmooth.Editor.Animation
{
    public class OSCmoothAnimationHandler
    {
        private OSCmoothSetup _setup;
        private CustomAnimLayer[] _layers;
        private AnimatorController _animatorController;
        private string _animatorPath;
        private string _animatorGUID;
        private List<Object> _animatorAssets;
        private OSCmNameManager _nameParser;

        private List<OSCmoothParameter> _parameters;
        private string _exportDirectory;
        private bool _saveAssetsToFiles;
        private bool _useEncoding;
        private HashSet<string> _existingParameters;
        private Dictionary<string, string> _parameterRenameBatch = new Dictionary<string, string>();

        public OSCmoothAnimationHandler(OSCmoothSetup setup,
                                        CustomAnimLayer[] layers,
                                        bool saveAssetsToFiles = true)
        {
            _setup = setup;
            _layers = layers;
            _saveAssetsToFiles = saveAssetsToFiles;
        }

        public void RemoveAllOSCmoothFromController()
        {
            CleanAnimatorBlendTreeBloat("OSCm");
            RevertStateMachineParameters();
            RemoveExtendedParametersInController("OSCm");
            RemoveContainingLayersInController("OSCm");
        }

        public void RenameParameterBatch(string from, string to)
        {
            if(!_parameterRenameBatch.ContainsKey(from))
                _parameterRenameBatch.Add(from, to);
        }

        public void CreateEncoders(AnimatorState local, AnimatorState remote)
        {
            var _localBehaviors = new List<StateMachineBehaviour>();
            var _remoteBehaviors = new List<StateMachineBehaviour>();   
            foreach (var p in _setup.parameters.Where(pa => pa.binaryEncoding == OSCmoothParserType.Encoder && pa.binarySizeSelection > 0))
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Creating Encoder Layers", 0.0f);

                var _layer = CreateBinaryEncoderLayer(p.paramName, p.binarySizeSelection, p.binaryNegative);
                var _index = Array.FindIndex(_animatorController.layers, l => l.name == _layer.name);

                var _localBehavior = new VRCAnimatorLayerControl
                {
                    layer = _index,
                    blendDuration = 0f,
                    goalWeight = 1f,
                    playable = VRC_AnimatorLayerControl.BlendableLayer.FX
                };

                var _remoteBehavior = new VRCAnimatorLayerControl
                {
                    layer = _index,
                    blendDuration = 0f,
                    goalWeight = 0f,
                    playable = VRC_AnimatorLayerControl.BlendableLayer.FX
                };

                _localBehaviors.Add(_localBehavior);
                _remoteBehaviors.Add(_remoteBehavior);
                AssetDatabase.AddObjectToAsset(_localBehavior, _animatorPath);
                AssetDatabase.AddObjectToAsset(_remoteBehavior, _animatorPath);
            }
            local.behaviours = _localBehaviors.ToArray();
            remote.behaviours = _remoteBehaviors.ToArray();
            EditorUtility.ClearProgressBar();
        }

        public void CreateLayers()
        {
            for (int i = 0; i < _layers.Length; i++)
			{
				if (_layers[i].animatorController == null)
					continue;

				string _baseAnimatorPath = AssetDatabase.GetAssetPath(_layers[i].animatorController);
				AnimLayerType _type = _layers[i].type;
				_parameters = _setup.parameters.Where(p => Extensions.Contains(p.layerMask, _type)).ToList();

				string _directory = $"Assets/OSCmooth/Temp/";
				if (!Directory.Exists(_directory))
				{
					Directory.CreateDirectory(_directory);
					Debug.Log("Directory created: " + _directory);
				}

				_animatorPath = $"{_directory}{_layers[i].animatorController.name}Proxy.controller";

				if (!AssetDatabase.CopyAsset(_baseAnimatorPath, _animatorPath)) continue;
				AssetDatabase.SaveAssets();

				_animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(_animatorPath);
				_animatorGUID = AssetDatabase.AssetPathToGUID(_animatorPath);
                _animatorAssets = AssetDatabase.LoadAllAssetsAtPath(_animatorPath)
                                           .Where(a => a != null && (a.GetType() == typeof(AnimatorState) || a.GetType() == typeof(BlendTree)))
                                           .ToList();
                _existingParameters = new HashSet<string>(_animatorController.parameters.Select((AnimatorControllerParameter p) => p.name));
                _exportDirectory = $"{_directory}/Animator_{_animatorGUID}/";
                _nameParser = new OSCmNameManager();

				CreateLayer();

				_layers[i].animatorController = _animatorController;
			}
        }

        public void CreateLayer()
        {
            AssetDatabase.StartAssetEditing();

            AnimatorControllerParameter _isLocal = _animatorController.CreateParameter(_existingParameters, "IsLocal", AnimatorControllerParameterType.Bool, true);

            AnimatorControllerLayer animLayer = CreateAnimLayerInController("_OSCmooth_Gen");
            var _localState = animLayer.stateMachine.AddState("OSCmooth_Local", new Vector3(30, 170, 0));
            var _remoteState = animLayer.stateMachine.AddState("OSCmooth_Remote", new Vector3(30, 170, 0));

            List<BlendTree> _locals = new List<BlendTree>();
            List<BlendTree> _remotes = new List<BlendTree>();
            if (_parameters.Any(p => p.binaryEncoding == OSCmoothParserType.Encoder))
                CreateEncoders(_localState, _remoteState);
            if (_parameters.Any(p => p.binarySizeSelection > 0))
                _remotes.Add(CreateBinaryDecoderLayer());

            var smoothLayer = CreateSmoothLayer();
            _locals.Add(smoothLayer.local);
            _remotes.Add(smoothLayer.remote);

            _locals.Add(CreateDirectDriveLayer());

            var _localChilds = new List<ChildMotion>();
            foreach (var tree in _locals)
            {
                _localChilds.Add(new ChildMotion
                {
                    directBlendParameter = _nameParser.BlendSet(),
                    motion = tree,
                    timeScale = 1
                });
            }
            var _remoteChilds = new List<ChildMotion>();
            foreach (var tree in _remotes)
            {
                _remoteChilds.Add(new ChildMotion
                {
                    directBlendParameter = _nameParser.BlendSet(),
                    motion = tree,
                    timeScale = 1
                });
            }

            var _remoteTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCmooth_Remote_Smoother",
                useAutomaticThresholds = false,
                children = _remoteChilds.ToArray()
            };

            var _localTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCmooth_Local_Smoother",
                useAutomaticThresholds = false,
                children = _localChilds.ToArray()
            };

            _localState.motion = _localTree;
            _remoteState.motion = _remoteTree;

            var _localEntry = animLayer.stateMachine.AddEntryTransition(_localState);
            var _remoteEntry = animLayer.stateMachine.AddEntryTransition(_remoteState);
            var _toLocal = _remoteState.AddTransition(_localState);
            var _toRemote = _localState.AddTransition(_remoteState);

            _toLocal.exitTime = 0f;
            _toLocal.duration = 0f;
            _toRemote.exitTime = 0f;
            _toRemote.duration = 0f;

            if (_isLocal.type == AnimatorControllerParameterType.Bool)
            {
                _localEntry.AddCondition(AnimatorConditionMode.If, 0.5f, "IsLocal");
                _toLocal.AddCondition(AnimatorConditionMode.If, 0.5f, "IsLocal");
                _remoteEntry.AddCondition(AnimatorConditionMode.IfNot, 0.5f, "IsLocal");
                _toRemote.AddCondition(AnimatorConditionMode.IfNot, 0.5f, "IsLocal");
            }
            else
            {
                _localEntry.AddCondition(AnimatorConditionMode.Greater, 0.5f, "IsLocal");
                _toLocal.AddCondition(AnimatorConditionMode.Greater, 0.5f, "IsLocal");
                _remoteEntry.AddCondition(AnimatorConditionMode.Less, 0.5f, "IsLocal");
                _toRemote.AddCondition(AnimatorConditionMode.Less, 0.5f, "IsLocal");
            }

            BatchRenameParameters();

            AssetDatabase.AddObjectToAsset(_localTree, _animatorPath);
            AssetDatabase.AddObjectToAsset(_remoteTree, _animatorPath);

            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        public BlendTree CreateDirectDriveLayer()
        {
            var _basis = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCm_Direct_Passthrough",
                useAutomaticThresholds = false
            };

            var _childs = new List<ChildMotion>();

            foreach (OSCmoothParameter p in _parameters.Where(oscmP => oscmP.binaryEncoding != OSCmoothParserType.None))
            {
                Debug.Log($"Creating Parameter {p.paramName} Direct Drive.");
                //_animatorController.CreateParameter(_existingParameters, p.paramName, AnimatorControllerParameterType.Float, true);
                //_animatorController.CreateParameter(_existingParameters, _nameParser.Proxy(p.paramName), AnimatorControllerParameterType.Float, true);

                EnsureParameterExists(p.paramName, AnimatorControllerParameterType.Float, true);
                EnsureParameterExists(_nameParser.Proxy(p.paramName), AnimatorControllerParameterType.Float, true);

                

                var _driveBlend = new BlendTree()
                {
                    blendType = BlendTreeType.Simple1D,
                    hideFlags = HideFlags.HideInHierarchy,
                    name = $"{p.paramName}_DirectDrive",
                    useAutomaticThresholds = false,
                    blendParameter = p.paramName,
                };

                var _driveChilds = new List<ChildMotion>();
                var _curveZero = new AnimationCurve();
                _curveZero.AddKey(0f, p.binaryNegative ? -1f : 0f);
                _driveChilds.Add(new ChildMotion
                {
                    motion = FindOrCreateAnimationClip(_exportDirectory, _nameParser.BinaryDriver(p.paramName), _curveZero, $"{p.paramName}_DirectDrive_0"),
                    threshold = p.binaryNegative ? -1f : 0f,
                    timeScale = 1f,
                });

                var _curveOne = new AnimationCurve();
                _curveOne.AddKey(0f, 1f);
                _driveChilds.Add(new ChildMotion
                {
                    motion = FindOrCreateAnimationClip(_exportDirectory, _nameParser.BinaryDriver(p.paramName), _curveOne,$"{p.paramName}_DirectDrive_1"),
                    threshold = 1f,
                    timeScale= 1f,
                });

                _driveBlend.children = _driveChilds.ToArray();

                _childs.Add(new ChildMotion 
                { 
                    motion = _driveBlend,
                    directBlendParameter = _nameParser.BlendSet(),
                });

                AssetDatabase.AddObjectToAsset(_driveBlend, _animatorPath);
            }

            _basis.children = _childs.ToArray();

            AssetDatabase.AddObjectToAsset(_basis, _animatorPath);

            return _basis;
        }

        public struct SmoothLayer
        {
            public BlendTree local;
            public BlendTree remote;
        }


        public SmoothLayer CreateSmoothLayer()
        {
            //_animatorController.CreateParameter(_existingParameters, "IsLocal", AnimatorControllerParameterType.Float, false);

            /*
            var rootBlend = new BlendTree()
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCm_Smoother_Root",
                useAutomaticThresholds = false,
                blendParameter = "IsLocal"
            };
            */
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
            //_animatorController.CreateParameter(_existingParameters, _nameParser.BlendSet(), AnimatorControllerParameterType.Float, false, 1f);
            EnsureParameterExists(_nameParser.BlendSet(), AnimatorControllerParameterType.Float, false, 1f);



            var localChildMotions = new List<ChildMotion>();
            var remoteChildMotions = new List<ChildMotion>();

            int i = 0;
            foreach (OSCmoothParameter p in _parameters)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Creating Smoothing Direct BlendTree", (float)i++/_parameters.Count);
                if (p.convertToProxy)
                {
                    RenameParameterBatch(p.paramName, _nameParser.Proxy(p.paramName));
                }

                var motionLocal = CreateParameterSmoothingBlendTree(p.localSmoothness, p, false);
                var motionRemote = CreateParameterSmoothingBlendTree(p.remoteSmoothness, p, true);

                localChildMotions.Add(new ChildMotion
                {
                    directBlendParameter = _nameParser.BlendSet(),
                    motion = motionLocal,
                    timeScale = 1
                });

                remoteChildMotions.Add(new ChildMotion
                {
                    directBlendParameter = _nameParser.BlendSet(),
                    motion = motionRemote,
                    timeScale = 1,
                });
            }
            EditorUtility.ClearProgressBar();
            basisLocalBlendTree.children = localChildMotions.ToArray();
            basisRemoteBlendTree.children = remoteChildMotions.ToArray();

            //rootBlend.AddChild(basisRemoteBlendTree, 0f);
            //rootBlend.AddChild(basisLocalBlendTree, 1f);

            //AssetDatabase.AddObjectToAsset(rootBlend, _animatorPath);
            AssetDatabase.AddObjectToAsset(basisRemoteBlendTree, _animatorPath);
            AssetDatabase.AddObjectToAsset(basisLocalBlendTree, _animatorPath);

            return new SmoothLayer 
            { 
                remote = basisRemoteBlendTree,
                local = basisLocalBlendTree,
            };
            //return rootBlend;
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
            //_animatorController.CreateParameter(_existingParameters, _nameParser.BlendSet(), AnimatorControllerParameterType.Float, false, 1f);
            EnsureParameterExists(_nameParser.BlendSet(), AnimatorControllerParameterType.Float, false, 1f);


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
                        if (_parameterRenameBatch.TryGetValue(blendTree.blendParameter, out string _newBlend))
                            blendTree.blendParameter = _newBlend;

                        if (_parameterRenameBatch.TryGetValue(blendTree.blendParameterY, out string _newBlendY))
                            blendTree.blendParameterY = _newBlendY;

                        var _children = blendTree.children;
                        for (int i = 0; i < blendTree.children.Length; i++)
                        {
                            if (_parameterRenameBatch.TryGetValue(_children[i].directBlendParameter, out string _newDirectParameter))
                                _children[i].directBlendParameter = _newDirectParameter;
                        }
                        blendTree.children = _children;
                        break;

                    case AnimatorState animatorState:
                        if (_parameterRenameBatch.TryGetValue(animatorState.timeParameter, out string _newTime))
                            animatorState.timeParameter = _newTime;

                        if (_parameterRenameBatch.TryGetValue(animatorState.speedParameter, out string _newSpeed))
                            animatorState.speedParameter = _newSpeed;

                        if (_parameterRenameBatch.TryGetValue(animatorState.cycleOffsetParameter, out string _newOffset))
                            animatorState.cycleOffsetParameter = _newOffset;

                        if (_parameterRenameBatch.TryGetValue(animatorState.mirrorParameter,out string _newMirror))
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
            foreach (var oscmParam in OSCmNameManager.ParameterNames)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Removing Smoothing Direct BlendTree", (float)i++/oscmParam.Count());
                foreach (var stateParam in stateParams)
                {
                    if (stateParam.Contains(oscmParam))
                    {
                        RenameParameterBatch(stateParam, stateParam.Replace(oscmParam, ""));
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

        public AnimatorControllerLayer CreateAnimLayerInController(string layerName, float defaultWeight = 1f, bool checkForExisting = true)
        {
            if (checkForExisting)
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

        public AnimatorControllerLayer CreateBinaryEncoderLayer(string paramName, int binarySizeSelection, bool combinedParameter)
        {
            var _layer = CreateAnimLayerInController($"_OSCm_{paramName}_Encode", 1f, false);

            //_animatorController.CreateParameter(_existingParameters, paramName, AnimatorControllerParameterType.Float, true);
            EnsureParameterExists(paramName, AnimatorControllerParameterType.Float, true);


            int binaryRes = (int)Mathf.Pow(2, binarySizeSelection);
            int binaryStates = binaryRes;

            for (int i = 0; i < binaryStates; i++)
            {
                var _state = _layer.stateMachine.AddState($"{paramName}{i}", new Vector3(400f, (i - (binaryStates/2)) * 40, 0f));
                _state.speed = 10000f;

                var _paramDriver = new VRCAvatarParameterDriver();
                _paramDriver.localOnly = true;

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
                        name = _nameParser.BinaryNegative(paramName, true),
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
                    _paramDriver.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter
                    {
                        name = _nameParser.Binary(paramName, 1 << j, true),
                        type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set,
                        value = ((i >> j) & 1) == 1 ? 1f : 0f
                    });
                }

                _state.behaviours = new StateMachineBehaviour[] { _paramDriver };
                AssetDatabase.AddObjectToAsset(_paramDriver, _animatorPath);
            }

            return _layer;
        }

        public AnimationClip[] CreateFloatSmootherAnimation(string paramName, 
                                                            string directory, 
                                                            float initThreshold = -1, 
                                                            float finalThreshold = 1)
        {
            string baseName = NameNoSymbol(paramName);
            string initAssetPath = directory + baseName + "-1" + "_" + _animatorGUID + ".anim";
            string finalAssetPath = directory + baseName + "1" + "_" + _animatorGUID + ".anim";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var curvesInit = new AnimationCurve(new Keyframe(0.0f, initThreshold));
            var curvesFinal = new AnimationCurve(new Keyframe(0.0f, finalThreshold));

            var _animationClipInit = FindOrCreateAnimationClip(initAssetPath, paramName, curvesInit, $"{paramName}_Smoother_{initThreshold}");
            var _animationClipFinal = FindOrCreateAnimationClip(finalAssetPath, paramName, curvesFinal, $"{paramName}_Smoother_{finalThreshold}");

            return new AnimationClip[] { _animationClipInit, _animationClipFinal };
        }

        public BlendTree CreateParameterSmoothingBlendTree(float smoothness,
                                                           OSCmoothParameter parameter,
                                                           bool remote)
        {
            var paramName = parameter.paramName;
            var _smoother = _nameParser.Smoother(paramName, remote);
            var _driverProxy = parameter.binarySizeSelection > 0 ? _nameParser.BinaryDriver(paramName) : paramName;
            var _proxy = _nameParser.Proxy(paramName);

            //_animatorController.CreateParameter(_existingParameters, _smoother, AnimatorControllerParameterType.Float, false, smoothness);
            //_animatorController.CreateParameter(_existingParameters, _proxy, AnimatorControllerParameterType.Float, false);
            //_animatorController.CreateParameter(_existingParameters, _driverProxy, AnimatorControllerParameterType.Float, false);
            EnsureParameterExists(_smoother, AnimatorControllerParameterType.Float, false, smoothness);
            EnsureParameterExists(_proxy, AnimatorControllerParameterType.Float, false);
            EnsureParameterExists(_driverProxy, AnimatorControllerParameterType.Float, false);


            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _smoother,
                name = "OSCm_" + paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _driverProxy,
                name = "OSCm_Input",
                useAutomaticThresholds = false
            };
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = _proxy,
                name = "OSCm_Driver",
                useAutomaticThresholds = false
            };

            // Create smoothing anims
            AnimationClip[] driverAnims = CreateFloatSmootherAnimation(_proxy, _exportDirectory);

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
            var _rootParameter = _nameParser.BlendSet();
            if (combinedParameter)
            {
                var _binaryNegative = _nameParser.BinaryNegative(paramName, false);
                EnsureParameterExists(_binaryNegative, AnimatorControllerParameterType.Float, true);
                _rootParameter = _binaryNegative;
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
                var decodeBinaryPositive = CreateBinaryDecodeTree(paramName, _exportDirectory, i, binarySizeSelection, false);

                childBinaryPositiveDecode.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = decodeBinaryPositive,
                    timeScale = 1
                });
            }
            _decodeBinaryPositiveTree.children = childBinaryPositiveDecode.ToArray();
            AssetDatabase.AddObjectToAsset(_decodeBinaryPositiveTree, _animatorPath);
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
                    var decodeBinaryNegative = CreateBinaryDecodeTree(paramName, _exportDirectory, i, binarySizeSelection, true);

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

        public AnimationClip FindOrCreateAnimationClip(string directory, string paramToDrive, AnimationCurve curve, string animName = "")
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
            clip.name = animName;

            Debug.Log("Choki - animName: " + animName);
            clip.SetCurve("", typeof(Animator), paramToDrive, curve);
            Debug.Log("Choki - Created or loaded animation clip with name: " + clip.name);
    
            return clip;
        }

        public AnimationClip[] CreateBinaryDecodeAnimation(string paramName, string directory, float weight, int step)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var trueNamePath = directory + NameNoSymbol(paramName) + "_True_" + step.ToString() + weight + _animatorGUID + ".anim";
            var falseNamePath = directory + NameNoSymbol(paramName) + "_False_" + step.ToString() + weight + _animatorGUID + ".anim";
            var trueAnimName = NameNoSymbol(paramName) + "_True_" + step.ToString() + weight;
            var falseAnimName = NameNoSymbol(paramName) + "_False_" + step.ToString() + weight;

            var _trueClip = FindOrCreateAnimationClip(trueNamePath, paramName, new AnimationCurve(new Keyframe(0.0f, 0.0f)),trueAnimName);
            var _falseClip = FindOrCreateAnimationClip(falseNamePath, paramName, new AnimationCurve(new Keyframe(0.0f, weight)),falseAnimName);

            return new AnimationClip[] { _trueClip, _falseClip };
        }

        public BlendTree CreateBinaryDecodeTree(string paramName, string directory, int binaryPow, int binarySize, bool negative)
        {
            float binaryPowValue = Mathf.Pow(2, binaryPow);
            var binaryParameter = _nameParser.Binary(paramName, (int)binaryPowValue, false);
            var binaryProxy = _nameParser.BinaryDriver(paramName);

            //_animatorController.CreateParameter(_existingParameters, binaryParameter, AnimatorControllerParameterType.Float, true);
            EnsureParameterExists(binaryParameter, AnimatorControllerParameterType.Float, true);
            

            BlendTree decodeBinary = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = binaryParameter,
                name = $"Binary_{paramName}_Decode_{(negative ? "Negative_" : "")}{binaryPowValue}",
                useAutomaticThresholds = false
            };

            // Create Decode anims and weight per binary
            AnimationClip[] decodeAnims = CreateBinaryDecodeAnimation(binaryProxy, 
                                                                      directory, (negative ? -1f : 1f) * binaryPowValue / (Mathf.Pow(2, binarySize) - 1f), 
                                                                      binaryPow);
            decodeBinary.AddChild(decodeAnims[0], 0f);
            decodeBinary.AddChild(decodeAnims[1], 1f);

            AssetDatabase.AddObjectToAsset(decodeBinary, _animatorPath);

            return decodeBinary;
        }

        private void EnsureParameterExists(string parameterName, AnimatorControllerParameterType type, bool defaultBool = false, float defaultFloat = 0f)
        {
            if (_existingParameters.Contains(parameterName))
            {
                Debug.Log($"Parameter '{parameterName}' already exists in the Animator Controller. Skipping creation.");
                return;
            }

            AnimatorControllerParameter parameter = new AnimatorControllerParameter
            {
                name = parameterName,
                type = type
            };

            if (type == AnimatorControllerParameterType.Float)
            {
                parameter.defaultFloat = defaultFloat;
            }
            else if (type == AnimatorControllerParameterType.Bool)
            {
                parameter.defaultBool = defaultBool;
            }

            _animatorController.AddParameter(parameter);
            _existingParameters.Add(parameterName); // Add to the existing parameters list
            Debug.Log($"Parameter '{parameterName}' added to Animator Controller.");
        }

    }
}

