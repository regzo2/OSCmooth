using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using OSCTools.OSCmooth.Util;
using OSCTools.OSCmooth.Types;

namespace OSCTools.OSCmooth.Animation
{
    public class OSCmoothAnimationHandler
    {
        public AnimatorController animatorController;
        public List<OSCmoothParameter> smoothLayer;

        public OSCmoothAnimationHandler() { }
        public OSCmoothAnimationHandler(List<OSCmoothParameter> smoothLayer, AnimatorController animatorController)
        {
            this.smoothLayer = smoothLayer;
            this.animatorController = animatorController;
        }
        public void CreateSmoothAnimationLayer()
        {

            for (int i = 0; i < animatorController.layers.Length; i++)
            {
                if (animatorController.layers[i].name == "_OSCmooth_Smoothing_Gen_")
                {
                    animatorController.RemoveLayer(i);
                }
            }

            AnimatorControllerLayer animLayer = AnimUtil.CreateAnimLayerInController("_OSCmooth_Smoothing_Gen_", animatorController);

            AnimatorState[] state = new AnimatorState[2];
            state[0] = animLayer.stateMachine.AddState("OSCmooth_Local", new Vector3(30, 170, 0));
            state[1] = animLayer.stateMachine.AddState("OSCmooth_Net", new Vector3(30, 170 + 60, 0));

            var toRemoteState = state[0].AddTransition(state[1]);
            var toLocalState = state[1].AddTransition(state[0]);
            toRemoteState.duration = 0;
            toLocalState.duration = 0;

            ParameterUtil.CheckAndCreateParameter("isLocal", animatorController, AnimatorControllerParameterType.Bool);

            toRemoteState.AddCondition(AnimatorConditionMode.IfNot, 0, "isLocal");
            toLocalState.AddCondition(AnimatorConditionMode.If, 0, "isLocal");

            var basisLocalBlendTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "Local",
                useAutomaticThresholds = false
                
            };

            var basisRemoteBlendTree = new BlendTree()
            {
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
                name = "Remote",
                useAutomaticThresholds = false
            };

            state[0].motion = basisLocalBlendTree;
            state[1].motion = basisRemoteBlendTree;
            AssetDatabase.AddObjectToAsset(basisLocalBlendTree, AssetDatabase.GetAssetPath(animLayer.stateMachine));
            AssetDatabase.AddObjectToAsset(basisRemoteBlendTree, AssetDatabase.GetAssetPath(animLayer.stateMachine));

            ParameterUtil.CheckAndCreateParameter("1Set", animatorController, AnimatorControllerParameterType.Float, 1);

            List<ChildMotion> localChildMotion = new List<ChildMotion>();
            List<ChildMotion> remoteChildMotion = new List<ChildMotion>();

            foreach (OSCmoothParameter smoothLayer in smoothLayer)
            {
                localChildMotion.Add(new ChildMotion 
                {
                    directBlendParameter = "1Set",
                    motion = AnimUtil.CreateSmoothingBlendTree(animatorController, animLayer.stateMachine, smoothLayer.localSmoothness, smoothLayer.paramName),
                    timeScale = 1
                });

                remoteChildMotion.Add(new ChildMotion
                {
                    directBlendParameter = "1Set",
                    motion = AnimUtil.CreateSmoothingBlendTree(animatorController, animLayer.stateMachine, smoothLayer.localSmoothness, smoothLayer.paramName),
                    timeScale = 1,
                });
            }

            basisLocalBlendTree.children = localChildMotion.ToArray();
            basisRemoteBlendTree.children = remoteChildMotion.ToArray();
        }
    }
}