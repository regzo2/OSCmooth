using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using Tools.OSCmooth.Types;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;

#if UNITY_EDITOR
using Tools.OSCmooth.Util;
using UnityEditor.Animations;

namespace Tools.OSCmooth.Animation
{
    public class OSCmoothAnimationHandler
    {
        public AnimatorController _animatorController;
        public List<OSCmoothParameter> _parameters;
        public bool _writeDefaults;
        public string _animExportDirectory;
        public string _binaryExportDirectory;

        public void RemoveAllOSCmoothFromController()
        {
            AnimUtil _handler = new AnimUtil(_animatorController, _binaryExportDirectory, _animExportDirectory);
            _handler.CleanAnimatorBlendTreeBloat("OSCm");
            _handler.RevertStateMachineParameters();
            _handler.RemoveExtendedParametersInController("OSCm");
            _handler.RemoveContainingLayersInController("OSCm");
        }

        public void CreateLayers()
        {
            AssetDatabase.StartAssetEditing();
            CreateBinaryLayer();
            CreateSmoothAnimationLayer();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        public void CreateSmoothAnimationLayer()
        {

            // Creating new OSCmooth setup.
            AnimatorControllerLayer animLayer;
            AnimUtil _handler = new AnimUtil(_animatorController, _binaryExportDirectory, _animExportDirectory);

            animLayer = _handler.CreateAnimLayerInController("_OSCmooth_Smoothing_Gen");

            // Creating a Direct BlendTree that will hold all of the smooth driver animations. This is to effectively create a 'sublayer'
            // system within the Direct BlendTree to tidy up the animator base layers from bloating up visually.
            AnimatorState[] state = new AnimatorState[2];


            state[0] = animLayer.stateMachine.AddState("OSCmooth_Local", new Vector3(30, 170, 0));
            state[1] = animLayer.stateMachine.AddState("OSCmooth_Net", new Vector3(30, 170 + 60, 0));

            state[0].writeDefaultValues = true;
            state[1].writeDefaultValues = true;

            var toRemoteState = state[0].AddTransition(state[1]);
            toRemoteState.duration = 0;

            var toLocalState = state[1].AddTransition(state[0]);
            toLocalState.duration = 0;

            ParameterUtil.CheckAndCreateParameter("IsLocal", _animatorController, AnimatorControllerParameterType.Bool);

            toRemoteState.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLocal");
            toLocalState.AddCondition(AnimatorConditionMode.If, 0, "IsLocal");

            // Creating BlendTree objects to better customize them in the AC Editor
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

            // Stuffing the BlendTrees into their designated state. Also stuffing them so that they
            // retain serialization.
            state[0].motion = basisLocalBlendTree;
            state[1].motion = basisRemoteBlendTree;
            AssetDatabase.AddObjectToAsset(basisLocalBlendTree, AssetDatabase.GetAssetPath(animLayer.stateMachine));
            AssetDatabase.AddObjectToAsset(basisRemoteBlendTree, AssetDatabase.GetAssetPath(animLayer.stateMachine));

            // Creating a '1Set' parameter that holds a value of one at all times for the Direct BlendTree
            ParameterUtil.CheckAndCreateParameter("OSCm/BlendSet", _animatorController, AnimatorControllerParameterType.Float, 1f);

            var localChildMotions = new List<ChildMotion>();
            var remoteChildMotions = new List<ChildMotion>();

            int i = 0;
            foreach (OSCmoothParameter p in _parameters)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Creating Smoothing Direct BlendTree", (float)i++/_parameters.Count);
                if (p.convertToProxy)
                {
                    _handler.RenameAllStateMachineInstancesOfBlendParameter(p.paramName, "OSCm/Proxy/" + p.paramName);
                }

                var motionLocal = _handler.CreateSmoothingBlendTree(p.localSmoothness, p.paramName, "OSCm/Local/");
                var motionRemote = _handler.CreateSmoothingBlendTree(p.remoteSmoothness, p.paramName, "OSCm/Remote/");

                localChildMotions.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = motionLocal,
                    timeScale = 1
                });

                remoteChildMotions.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = motionRemote,
                    timeScale = 1,
                });
            }
            EditorUtility.ClearProgressBar();
            basisLocalBlendTree.children = localChildMotions.ToArray();
            basisRemoteBlendTree.children = remoteChildMotions.ToArray();
        }

        public void CreateBinaryLayer()
        {
            // Check to see if we need to create the animation layer.
            if (!_parameters.Any(p => p.binarySizeSelection > 0)) return;

            // Creating new Binary setup.
            AnimatorControllerLayer animLayer;
            AnimUtil _handler = new AnimUtil(_animatorController, _binaryExportDirectory, _animExportDirectory);

            animLayer = _handler.CreateAnimLayerInController("_OSCmooth_Binary_Gen");

            // Creating a Direct BlendTree that will hold all of the binary decode driver animations. This is to effectively create a 'sublayer'
            // system within the Direct BlendTree to tidy up the animator base layers from bloating up visually.
            AnimatorState[] state = new AnimatorState[1];

            state[0] = animLayer.stateMachine.AddState("Binary_Parameters_Blendtree", new Vector3(30, 170, 0));
            state[0].writeDefaultValues = true;

            // Creating BlendTree objects to better customize them in the AC Editor         

            var binaryTreeRoot = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "OSCm_Binary_Root",
                useAutomaticThresholds = false
            };

            // Stuffing the BlendTrees into their designated state. Also stuffing them so that they
            // retain serialization.
            state[0].motion = binaryTreeRoot;
            AssetDatabase.AddObjectToAsset(binaryTreeRoot, AssetDatabase.GetAssetPath(animLayer.stateMachine));

            // Creating a '1Set' parameter that holds a value of one at all times for the Direct BlendTree
            ParameterUtil.CheckAndCreateParameter("OSCm/BlendSet", _animatorController, AnimatorControllerParameterType.Float, 1f);

            var childBinary = new List<ChildMotion>();

            // Go through each parameter and create each child to eventually stuff into the Direct BlendTrees. 
            int i = 0;
            foreach (OSCmoothParameter p in _parameters)
            {
                EditorUtility.DisplayProgressBar("OSCmooth", "Creating Binary Parameter Direct BlendTree", (float)i++/_parameters.Count);
                if (p.binarySizeSelection == 0) continue;
                var decodeBinary = _handler.CreateBinaryBlendTree(p.paramName, p.binarySizeSelection, p.combinedParameter);

                childBinary.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = decodeBinary,
                    timeScale = 1
                });
            }

            binaryTreeRoot.children = childBinary.ToArray();
        }
    }
}
#endif