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

        public static void CleanAnimatorBlendTreeBloat(AnimatorController animatorController, string filter)
        {
            Object[] animatorAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(animatorController)); 

            foreach (Object asset in animatorAssets)
            {
                if (asset?.GetType() == typeof(BlendTree))
                {
                    if (((BlendTree)asset).name.Contains(filter))
                    {
                        Object.DestroyImmediate(asset, true);
                    }
                }
            }
        }
        public static void RenameAllStateMachineInstancesOfBlendParameter(AnimatorController animatorController, string initParameter, string newParameter)
        {
            Object[] animatorAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(animatorController));

            foreach (Object asset in animatorAssets)
            {
                if (asset?.GetType() == typeof(BlendTree))
                {
                    if(((BlendTree)asset).blendParameter == initParameter)
                        ((BlendTree)asset).blendParameter = newParameter;

                    if (((BlendTree)asset).blendParameterY == initParameter)
                        ((BlendTree)asset).blendParameterY = newParameter;

                    for (int i = 0; i < ((BlendTree)asset).children.Length; i++)
                    {
                        if (((BlendTree)asset).children[i].directBlendParameter == initParameter)
                            ((BlendTree)asset).children[i].directBlendParameter = newParameter;
                    }

                    continue;
                }

                if (asset?.GetType() == typeof(AnimatorState))
                {
                    if (((AnimatorState)asset).timeParameter == initParameter)
                        ((AnimatorState)asset).timeParameter = newParameter;

                    if (((AnimatorState)asset).speedParameter == initParameter)
                        ((AnimatorState)asset).speedParameter = newParameter;

                    if (((AnimatorState)asset).cycleOffsetParameter == initParameter)
                        ((AnimatorState)asset).cycleOffsetParameter = newParameter;

                    if (((AnimatorState)asset).mirrorParameter == initParameter)
                        ((AnimatorState)asset).mirrorParameter = newParameter;
                }
            }
        }

        public static AnimatorControllerLayer CreateAnimLayerInController(string layerName, AnimatorController animatorController)
        {
            for (int i = 0; i < animatorController.layers.Length; i++)
            {
                if (animatorController.layers[i].name == layerName)
                {
                    animatorController.RemoveLayer(i);
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

            CleanAnimatorBlendTreeBloat(animatorController,  "OSCm_");

            // Store Layer into Animator Controller, as creating a Layer object is not serialized unless we store it inside an asset.
            if (AssetDatabase.GetAssetPath(animatorController) != string.Empty)
            {
                AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
            }

            animatorController.AddLayer(layer);

            return layer;
        }

        public static AnimationClip[] CreateFloatSmootherAnimation(string paramName, string smoothSuffix, string proxySuffix, float initThreshold = -1, float finalThreshold = 1, bool driveBase = false)
        {
            AnimationClip[] _animationClips = new AnimationClip[3];
            AnimationCurve[] _curves = new AnimationCurve[]
            {
                new AnimationCurve(new Keyframe(0.0f, initThreshold)),
                new AnimationCurve(new Keyframe(0.0f, 0f)),
                new AnimationCurve(new Keyframe(0.0f, finalThreshold))
            };

            _animationClips[0].SetCurve("", typeof(Animator), driveBase ? paramName : paramName + proxySuffix, _curves[0]);
            _animationClips[1].SetCurve("", typeof(Animator), driveBase ? paramName : paramName + proxySuffix, _curves[1]);
            _animationClips[2].SetCurve("", typeof(Animator), driveBase ? paramName : paramName + proxySuffix, _curves[1]);

            if (!Directory.Exists("Assets/OSCmooth/Generated/Anims/"))
            {
                Directory.CreateDirectory("Assets/OSCmooth/Generated/Anims/");
            }

            SaveAnimationAsset(_animationClips[0], paramName + initThreshold + "Smoother");
            SaveAnimationAsset(_animationClips[1], paramName + "0" + "Smoother");
            SaveAnimationAsset(_animationClips[2], paramName + finalThreshold + "Smoother");

            return _animationClips;
        }

        private static void SaveAnimationAsset(AnimationClip clip, string name)
        {
            if (!Directory.Exists("Assets/OSCmooth/Generated/Anims/"))
            {
                Directory.CreateDirectory("Assets/OSCmooth/Generated/Anims/");
            }

            string[] guid = (AssetDatabase.FindAssets(name + ".anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(clip, "Assets/OSCmooth/Generated/Anims/" + name + ".anim");
                AssetDatabase.SaveAssets();
            }

            else
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid[0]));
                AssetDatabase.CreateAsset(clip, "Assets/OSCmooth/Generated/Anims/" + name + ".anim");
                AssetDatabase.SaveAssets();
            }
        }

        public static BlendTree CreateSmoothingBlendTree(AnimatorController animatorController, AnimatorStateMachine stateMachine, float smoothness, string paramName, bool driveBase, float range, string smoothnessSuffix = "Smoother", string proxySuffix = "Proxy")
        {
            AnimatorControllerParameter smootherParam = ParameterUtil.CheckAndCreateParameter(paramName + smoothnessSuffix, animatorController, AnimatorControllerParameterType.Float, smoothness);
            ParameterUtil.CheckAndCreateParameter(paramName + proxySuffix, animatorController, AnimatorControllerParameterType.Float);
            ParameterUtil.CheckAndCreateParameter(paramName, animatorController, AnimatorControllerParameterType.Float);

            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = paramName + smoothnessSuffix,
                name = "OSCm_" + paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = driveBase ? paramName + proxySuffix : paramName,
                name = "OSCm_ProxyBlend",
                useAutomaticThresholds = false
            }; ;
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = driveBase ? paramName: paramName + proxySuffix,
                name = "OSCm_TrueBlend",
                useAutomaticThresholds = false
            }; ;

            // Create smoothing anims
            AnimationClip[] driverAnims = AnimUtil.CreateFloatSmootherAnimation(paramName, smoothnessSuffix, proxySuffix, -range, range, driveBase);

            rootTree.AddChild(falseTree, driveBase ? 1 : 0);
            rootTree.AddChild(trueTree, driveBase ? 0 : 1);

            falseTree.AddChild(driverAnims[0], -1);
            falseTree.AddChild(driverAnims[1], 0);
            falseTree.AddChild(driverAnims[2], 1);

            trueTree.AddChild(driverAnims[0], -1);
            trueTree.AddChild(driverAnims[1], 0);
            trueTree.AddChild(driverAnims[2], 1);

            AssetDatabase.AddObjectToAsset(rootTree, AssetDatabase.GetAssetPath(animatorController));
            AssetDatabase.AddObjectToAsset(falseTree, AssetDatabase.GetAssetPath(animatorController));
            AssetDatabase.AddObjectToAsset(trueTree, AssetDatabase.GetAssetPath(animatorController));

            return rootTree;
        }
    }
}