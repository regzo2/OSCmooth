using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using OSCTools.OSCmooth.Types;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OSCTools.OSCmooth.Util
{
    public class AnimUtil
    {
        public static void CleanAnimatorBlendTreeBloat(AnimatorController animatorController, string filter)
        {
            Object[] animatorAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(animatorController)); 

            foreach (Object asset in animatorAssets)
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

        public static List<string> GetAllStateMachineParameters(AnimatorController animatorController)
        {
            List<string> stateParams = new List<string>();

            Object[] animatorAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(animatorController));

            foreach (Object asset in animatorAssets)
            {
                if (asset?.GetType() == typeof(BlendTree))
                {
                    if (!stateParams.Contains(((BlendTree)asset).blendParameter))
                        stateParams.Add(((BlendTree)asset).blendParameter);

                    if (!stateParams.Contains(((BlendTree)asset).blendParameterY))
                        stateParams.Add(((BlendTree)asset).blendParameterY);

                    for (int i = 0; i < ((BlendTree)asset).children.Length; i++)
                    {
                        if (!stateParams.Contains(((BlendTree)asset).children[i].directBlendParameter))
                            stateParams.Add(((BlendTree)asset).children[i].directBlendParameter);
                    }

                    continue;
                }

                if (asset?.GetType() == typeof(AnimatorState))
                {
                    if (!stateParams.Contains(((AnimatorState)asset).timeParameter))
                        stateParams.Add(((AnimatorState)asset).timeParameter);

                    if (!stateParams.Contains(((AnimatorState)asset).speedParameter))
                        stateParams.Add(((AnimatorState)asset).speedParameter);

                    if (!stateParams.Contains(((AnimatorState)asset).cycleOffsetParameter))
                        stateParams.Add(((AnimatorState)asset).cycleOffsetParameter);

                    if (!stateParams.Contains(((AnimatorState)asset).mirrorParameter))
                        stateParams.Add(((AnimatorState)asset).mirrorParameter);
                }
            }

            return stateParams;
        }

        public static void RevertStateMachineParameters(AnimatorController animatorController)
        {
            string[] stateParams = GetAllStateMachineParameters(animatorController).ToArray();

            foreach (var oscmParam in OSCmoothFilters.ParameterExtensions)
            {
                foreach (var stateParam in stateParams) 
                {
                    if (stateParam.Contains(oscmParam))
                    {
                        RenameAllStateMachineInstancesOfBlendParameter(animatorController, stateParam, stateParam.Replace(oscmParam, ""));
                    }
                }
            }
        }

        public static void RemoveExtendedParametersInController(string name, AnimatorController animatorController)
        {
            for (int i = 0; i < animatorController.parameters.Length;)
            {
                if (animatorController.parameters[i].name.Contains(name))
                {
                    animatorController.RemoveParameter(i);
                    continue;
                }
                i++;
            }          
        }

        public static void RemoveContainingLayersInController(string name, AnimatorController animatorController)
        {
            for (int i = 0; i < animatorController.layers.Length;)
            {
                if (animatorController.layers[i].name.Contains(name))
                {
                    animatorController.RemoveLayer(i);
                    continue;
                }
                i++;
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

        public static void RemoveAssociatedAnimatorFolder(AnimatorController animatorController)
        {
            string animatorGUID;
            long id;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(animatorController, out animatorGUID, out id);
        }

        public static AnimationClip[] CreateFloatSmootherAnimation(AnimatorController animatorController, string paramName, string smoothSuffix, string proxyPrefix, string directory, float initThreshold = -1, float finalThreshold = 1, bool driveBase = false)
        {
            string animatorGUID;
            long id;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(animatorController, out animatorGUID, out id);
            
            AnimationClip _animationClipInit = new AnimationClip();
            AnimationClip _animationClipFinal = new AnimationClip();

            AnimationCurve _curvesInit = new AnimationCurve(new Keyframe(0.0f, initThreshold));
            AnimationCurve _curvesFinal = new AnimationCurve(new Keyframe(0.0f, finalThreshold));

            _animationClipInit.SetCurve("", typeof(Animator), driveBase ? paramName : proxyPrefix + paramName, _curvesInit);
            _animationClipFinal.SetCurve("", typeof(Animator), driveBase ? paramName : proxyPrefix + paramName, _curvesFinal);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string[] guid = (AssetDatabase.FindAssets(paramName + "-1" + "Smoother.anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(_animationClipInit, directory + paramName + "-1" + smoothSuffix + "_" + animatorGUID + ".anim");
                AssetDatabase.SaveAssets();
            }

            else
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid[0]));
                AssetDatabase.CreateAsset(_animationClipInit, directory + paramName + "-1" + smoothSuffix + "_" + animatorGUID + ".anim");
                AssetDatabase.SaveAssets();
            }

            guid = (AssetDatabase.FindAssets(paramName + "1" + smoothSuffix + ".anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(_animationClipFinal, directory + paramName + "1" + smoothSuffix + "_" + animatorGUID + ".anim");
                AssetDatabase.SaveAssets();
            }

            else
            {

                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid[0]));
                AssetDatabase.CreateAsset(_animationClipFinal, "Assets/OSCmooth/Generated/Anims/" + paramName + "1" + smoothSuffix + "_" + animatorGUID + ".anim");
                AssetDatabase.SaveAssets();
            }

            return new AnimationClip[]{ _animationClipInit, _animationClipFinal };
        }

        private static void SaveAnimationAsset(ref AnimationClip clip, string name, string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetDatabase.CreateAsset(clip, directory + name + ".anim");
            AssetDatabase.SaveAssets();
        }

        public static BlendTree CreateSmoothingBlendTree(AnimatorController animatorController, AnimatorStateMachine stateMachine, float smoothness, string paramName, bool driveBase, float range, string directory, string smoothnessPrefix = "OSCm/", string smoothnessSuffix = "Smoother", string proxyPrefix = "OSCm/", string proxySuffix = "Proxy")
        {
            AnimatorControllerParameter smootherParam = ParameterUtil.CheckAndCreateParameter(smoothnessPrefix + paramName + "Smoother", animatorController, AnimatorControllerParameterType.Float, smoothness);
            ParameterUtil.CheckAndCreateParameter(proxyPrefix + paramName, animatorController, AnimatorControllerParameterType.Float);
            ParameterUtil.CheckAndCreateParameter(paramName, animatorController, AnimatorControllerParameterType.Float);

            // Creating 3 blend trees to create the feedback loop
            BlendTree rootTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = smoothnessPrefix + paramName + "Smoother",
                name = "OSCm_" + paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = driveBase ? proxyPrefix + paramName : paramName,
                name = "OSCm_Input",
                useAutomaticThresholds = false
            };
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = driveBase ? paramName: proxyPrefix + paramName,
                name = "OSCm_Driver",
                useAutomaticThresholds = false
            };

            // Create smoothing anims
            AnimationClip[] driverAnims = AnimUtil.CreateFloatSmootherAnimation(animatorController, paramName, smoothnessSuffix, proxyPrefix, directory, -range, range, driveBase);

            rootTree.AddChild(falseTree, driveBase ? 1f : 0f);
            rootTree.AddChild(trueTree, driveBase ? 0f : 1f);

            falseTree.AddChild(driverAnims[0], -1f);
            falseTree.AddChild(driverAnims[1], 1f);

            trueTree.AddChild(driverAnims[0], -1f);
            trueTree.AddChild(driverAnims[1], 1f);

            AssetDatabase.AddObjectToAsset(rootTree, AssetDatabase.GetAssetPath(animatorController));
            AssetDatabase.AddObjectToAsset(falseTree, AssetDatabase.GetAssetPath(animatorController));
            AssetDatabase.AddObjectToAsset(trueTree, AssetDatabase.GetAssetPath(animatorController));

            AssetDatabase.SaveAssets();

            return rootTree;
        }
    }
}