using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Jobs;
using OSCTools.OSCmooth.Util;
using OSCTools.OSCmooth.Types;
using System.Linq;

namespace OSCTools.OSCmooth.Animation
{
    public static class OSCmoothAnimationHandler
    {
        public static AnimatorController _animatorController;
        public static List<OSCmoothParameter> _parameters;
        public static bool _writeDefaults;
        public static string _animExportDirectory;
        public static string _binaryExportDirectory;

        public static void RemoveAllOSCmoothFromController()
        {
            AnimUtil.RevertStateMachineParameters(_animatorController);
            AnimUtil.RemoveExtendedParametersInController("OSCm", _animatorController);
            AnimUtil.RemoveContainingLayersInController("OSCm", _animatorController);
        }

        public static void RemoveAllBinaryFromController()
        {

        }

        public static void CreateSmoothAnimationLayer()
        {

            // Creating new OSCmooth setup.
            AnimatorControllerLayer animLayer;

            animLayer = AnimUtil.CreateAnimLayerInController("_OSCmooth_Smoothing_Gen", _animatorController);

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


            List<ChildMotion> localChildMotion = new List<ChildMotion>();
            List<ChildMotion> remoteChildMotion = new List<ChildMotion>();

            // Go through each parameter and create each child to eventually stuff into the Direct BlendTrees. 
            foreach (OSCmoothParameter p in _parameters)
            {
                if (p.convertToProxy)
                {
                    AnimUtil.RenameAllStateMachineInstancesOfBlendParameter(_animatorController, p.paramName, "OSCm/Proxy/" + p.paramName);
                }

                var motionLocal = AnimUtil.CreateSmoothingBlendTree(_animatorController, animLayer.stateMachine, p.localSmoothness, p.paramName, p.flipInputOutput, 1f, _animExportDirectory, "OSCm/Local/", "SmootherWD", "OSCm/Proxy/", "Proxy");
                var motionRemote = AnimUtil.CreateSmoothingBlendTree(_animatorController, animLayer.stateMachine, p.remoteSmoothness, p.paramName, p.flipInputOutput, 1f, _animExportDirectory, "OSCm/Remote/", "SmootherRemoteWD", "OSCm/Proxy/", "Proxy");

                localChildMotion.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = motionLocal,
                    timeScale = 1
                });

                remoteChildMotion.Add(new ChildMotion
                {
                    directBlendParameter = "OSCm/BlendSet",
                    motion = motionRemote,
                    timeScale = 1,
                });
            }

            basisLocalBlendTree.children = localChildMotion.ToArray();
            basisRemoteBlendTree.children = remoteChildMotion.ToArray();
        }

        public static void CreateBinaryLayer()
        {

            // Creating new Binary setup.
            AnimatorControllerLayer animLayer;

            animLayer = AnimUtil.CreateAnimLayerInController("_OSCmooth_Binary_Gen", _animatorController);

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
                name = "Binary_Root",
                useAutomaticThresholds = false

            };

            // Stuffing the BlendTrees into their designated state. Also stuffing them so that they
            // retain serialization.
            state[0].motion = binaryTreeRoot;
            AssetDatabase.AddObjectToAsset(binaryTreeRoot, AssetDatabase.GetAssetPath(animLayer.stateMachine));

            // Creating a '1Set' parameter that holds a value of one at all times for the Direct BlendTree

            ParameterUtil.CheckAndCreateParameter("OSCm/BlendSet", _animatorController, AnimatorControllerParameterType.Float, 1f);


            List<ChildMotion> childBinary = new List<ChildMotion>();

            // Go through each parameter and create each child to eventually stuff into the Direct BlendTrees. 
            foreach (OSCmoothParameter p in _parameters)
            {

                var decodeBinary = AnimUtil.CreateBinaryBlendTree(_animatorController, animLayer.stateMachine, p.paramName, _binaryExportDirectory, p.binarySizeSelection, p.combinedParameter);

                if (p.binarySizeSelection != 0)
                {
                    childBinary.Add(new ChildMotion
                    {
                        directBlendParameter = "OSCm/BlendSet",
                        motion = decodeBinary,
                        timeScale = 1
                    });
                }


            }

            binaryTreeRoot.children = childBinary.ToArray();
        }

    }
}