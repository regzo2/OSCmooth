using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using OSCTools.OSCmooth.Types;
using System.Collections.Generic;

namespace OSCTools.OSCmooth.Util
{
    public class AnimUtil
    {
        public static void CleanNestedStateChilds(ChildAnimatorState state)
        {
            if (state.state.motion == null)
                return;
            
            if (state.state.motion.GetType() == typeof(BlendTree))
            {
                AssetDatabase.RemoveObjectFromAsset(state.state.motion);
            }
        }
        public static AnimatorControllerLayer CreateAnimLayerInController(string layerName, AnimatorController animatorController)
        {
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = new AnimatorStateMachine
                {
                    hideFlags = HideFlags.HideInInspector
                },
                defaultWeight = 1f
            };

            int layerIndex = 0;

            if (animatorController.layers.Length == 0)
            {
                animatorController.AddLayer(layer);

                if (AssetDatabase.GetAssetPath(animatorController) != string.Empty)
                {
                    AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
                }

                return layer;
            }

            // Workaround for old blendtrees being stuck in the animator controller file
            foreach (AnimatorControllerLayer animLayer in animatorController.layers)
            {
                if (animLayer.name == layerName)
                {
                    // BlendTree bloat workaround
                    foreach(ChildAnimatorState state in animLayer.stateMachine.states)
                    {
                        CleanNestedStateChilds(state);
                        animLayer.stateMachine.RemoveState(state.state);
                    }

                    if (animLayer.stateMachine == null)
                    {
                        animLayer.stateMachine = new AnimatorStateMachine();
                        AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
                    }

                    layer = animLayer;

                    return layer;
                }
            }

            for (int i = animatorController.layers.Length - 1; i < layerIndex; i--)
            {
                if (animatorController.layers[i].name == layer.name)
                    animatorController.RemoveLayer(i);
            }

            // Store Layer into Animator Controller, as creating a Layer object is not serialized unless we store it inside an asset.
            if (AssetDatabase.GetAssetPath(animatorController) != string.Empty)
            {
                AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
            }

            animatorController.AddLayer(layer);

            return layer;
        }

        public static AnimationClip[] CreateFloatSmootherAnimation(string paramName, string smoothSuffix, string proxySuffix, float initThreshold = 0, float finalThreshold = 1, bool driveBase = false)
        {
            AnimationClip _animationClip1 = new AnimationClip();
            AnimationClip _animationClip2 = new AnimationClip();

            AnimationCurve _curve1 = new AnimationCurve(new Keyframe(0.0f, initThreshold));
            AnimationCurve _curve2 = new AnimationCurve(new Keyframe(0.0f, finalThreshold));

            _animationClip1.SetCurve("", typeof(Animator), driveBase ? paramName : paramName + proxySuffix, _curve1);
            _animationClip2.SetCurve("", typeof(Animator), driveBase ? paramName : paramName + proxySuffix, _curve2);

            if (!Directory.Exists("Assets/OSCmooth/Generated/Anims/"))
            {
                Directory.CreateDirectory("Assets/OSCmooth/Generated/Anims/");
            }

            string[] guid = (AssetDatabase.FindAssets(paramName + initThreshold + "Smoother.anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(_animationClip1, "Assets/OSCmooth/Generated/Anims/" + paramName + initThreshold + smoothSuffix + ".anim");
                AssetDatabase.SaveAssets();
            }

            else
            {
                _animationClip1 = (AnimationClip)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid[0]), typeof(AnimationClip));
            }

            guid = (AssetDatabase.FindAssets(paramName + finalThreshold + smoothSuffix + ".anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(_animationClip2, "Assets/OSCmooth/Generated/Anims/" + paramName + finalThreshold + smoothSuffix + ".anim");
                AssetDatabase.SaveAssets();
            }

            else
            {
                _animationClip2 = (AnimationClip)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid[0]), typeof(AnimationClip));
            }

            return new AnimationClip[] { _animationClip1, _animationClip2 };
        }

        public static BlendTree CreateSmoothingBlendTree(AnimatorController animatorController, AnimatorStateMachine stateMachine, float smoothness, string paramName, string smoothnessSuffix = "Smoother", string proxySuffix = "Proxy")
        {
            AnimatorControllerParameter smootherParam = ParameterUtil.CheckAndCreateParameter(paramName + smoothnessSuffix, animatorController, AnimatorControllerParameterType.Float, smoothness);
            ParameterUtil.CheckAndCreateParameter(paramName + proxySuffix, animatorController, AnimatorControllerParameterType.Float);
            ParameterUtil.CheckAndCreateParameter(paramName, animatorController, AnimatorControllerParameterType.Float);

            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInInspector,
                blendParameter = paramName + smoothnessSuffix,
                name = paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInInspector,
                blendParameter = paramName,
                name = "ProxyBlend",
                useAutomaticThresholds = false
            }; ;
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInInspector,
                blendParameter = paramName + proxySuffix,
                name = "TrueBlend",
                useAutomaticThresholds = false
            }; ;

            // Create smoothing anims
            AnimationClip[] driverAnims = AnimUtil.CreateFloatSmootherAnimation(paramName, smoothnessSuffix, proxySuffix, -1f);

            rootTree.AddChild(falseTree, 0);
            rootTree.AddChild(trueTree, 1);

            falseTree.AddChild(driverAnims[0], -1);
            falseTree.AddChild(driverAnims[1], 1);

            trueTree.AddChild(driverAnims[0], -1);
            trueTree.AddChild(driverAnims[1], 1);

            return rootTree;
        }
    }
}