using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using OSCTools.OSCmooth.Util;

namespace OSCTools.OSCmooth.Animation
{
    public class OSCmoothAnimationHandler
    {
        public AnimatorController animatorController;
        public List<OSCmoothLayer> smoothLayer;

        public OSCmoothAnimationHandler() { }
        public OSCmoothAnimationHandler(List<OSCmoothLayer> smoothLayer, AnimatorController animatorController)
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
            state[0].motion = basisLocalBlendTree;
            AssetDatabase.AddObjectToAsset(basisLocalBlendTree, AssetDatabase.GetAssetPath(animLayer.stateMachine));

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

            foreach (OSCmoothLayer smoothLayer in smoothLayer)
            {
                basisLocalBlendTree.AddChild(AnimUtil.CreateSmoothingBlendTree(animatorController, animLayer.stateMachine, smoothLayer.localSmoothness, smoothLayer.paramName));
                basisRemoteBlendTree.AddChild(AnimUtil.CreateSmoothingBlendTree(animatorController, animLayer.stateMachine, smoothLayer.remoteSmoothness, smoothLayer.paramName, "RemoteSmoother"));
            }

            for(int i = 0; i < basisLocalBlendTree.children.Length; i++)
            {
                basisLocalBlendTree.children[i].directBlendParameter = "1Set";
                basisRemoteBlendTree.children[i].directBlendParameter = "1Set";
            }
        }
    }
}

