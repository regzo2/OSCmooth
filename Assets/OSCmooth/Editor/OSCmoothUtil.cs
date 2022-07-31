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
        public static AnimatorControllerLayer CreateAnimLayerInController(string paramName, AnimatorController animatorController)
        {
            // Creating a layer object since the default weight can not be assigned after creation.
            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = paramName,
                stateMachine = new AnimatorStateMachine
                {
                    hideFlags = HideFlags.HideInHierarchy
                },
                defaultWeight = 1f
            };

            // Store Layer into Animator Controller, as creating a Layer object is not serialized unless we store it inside an asset.
            if (AssetDatabase.GetAssetPath(animatorController) != string.Empty)
            {
                AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animatorController));
            }

            animatorController.AddLayer(layer);

            return layer;
        }

        public static AnimationClip[] CreateFloatSmootherAnimation(string paramName, string smoothSuffix, float initThreshold = 0, float finalThreshold = 1)
        {
            AnimationClip _animationClip1 = new AnimationClip();
            AnimationClip _animationClip2 = new AnimationClip();

            AnimationCurve _curve1 = new AnimationCurve(new Keyframe(0.0f, initThreshold));
            AnimationCurve _curve2 = new AnimationCurve(new Keyframe(0.0f, finalThreshold));

            _animationClip1.SetCurve("", typeof(Animator), paramName, _curve1);
            _animationClip2.SetCurve("", typeof(Animator), paramName, _curve2);

            if (!Directory.Exists("Assets/VRCFaceTracking/Generated/Anims/"))
            {
                Directory.CreateDirectory("Assets/VRCFaceTracking/Generated/Anims/");
            }

            string[] guid = (AssetDatabase.FindAssets(paramName + initThreshold + "Smoother.anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(_animationClip1, "Assets/VRCFaceTracking/Generated/Anims/" + paramName + initThreshold + smoothSuffix + ".anim");
                AssetDatabase.SaveAssets();
            }

            else
            {
                _animationClip1 = (AnimationClip)AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid[0]), typeof(AnimationClip));
            }

            guid = (AssetDatabase.FindAssets(paramName + finalThreshold + smoothSuffix + ".anim"));

            if (guid.Length == 0)
            {
                AssetDatabase.CreateAsset(_animationClip2, "Assets/VRCFaceTracking/Generated/Anims/" + paramName + finalThreshold + smoothSuffix + ".anim");
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
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = paramName + smoothnessSuffix,
                name = paramName + " Root",
                useAutomaticThresholds = false
            };
            BlendTree falseTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = paramName,
                name = "ProxyBlend",
                useAutomaticThresholds = false
            }; ;
            BlendTree trueTree = new BlendTree
            {
                blendType = BlendTreeType.Simple1D,
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = paramName + proxySuffix,
                name = "TrueBlend",
                useAutomaticThresholds = false
            }; ;

            // Create smoothing anims
            AnimationClip[] driverAnims = AnimUtil.CreateFloatSmootherAnimation(paramName + proxySuffix, paramName + smoothnessSuffix, -1f);

            rootTree.AddChild(falseTree, 0);
            rootTree.AddChild(trueTree, 1);

            falseTree.AddChild(driverAnims[0], -1);
            falseTree.AddChild(driverAnims[1], 1);

            trueTree.AddChild(driverAnims[0], -1);
            trueTree.AddChild(driverAnims[1], 1);

            AssetDatabase.AddObjectToAsset(rootTree, AssetDatabase.GetAssetPath(stateMachine));
            AssetDatabase.AddObjectToAsset(falseTree, AssetDatabase.GetAssetPath(stateMachine));
            AssetDatabase.AddObjectToAsset(trueTree, AssetDatabase.GetAssetPath(stateMachine));

            return rootTree;
        }
    }

    public class OSCmoothJSONUtil
    {

        public static void SaveListToJSONFile(List<OSCmoothParameter> parameters, string filePath = "Assets/OSCmooth/Editor/Resources/OSCmoothConfig.txt")
        {
            OSCmoothLayer smoothLayer = new OSCmoothLayer(parameters.ToArray());

            string json = JsonUtility.ToJson(smoothLayer);

            if (!File.Exists(filePath))
            {
                File.Create(filePath);
            }

            StreamWriter writer = new StreamWriter(filePath);
            writer.Write(json);
            writer.Close();

            Debug.Log("Saved JSON data to " + filePath);
        }
        public static List<OSCmoothParameter> LoadListfromJSONAsset(string json)
        {
            OSCmoothLayer smoothLayer = JsonUtility.FromJson<OSCmoothLayer>(json);

            List<OSCmoothParameter> parameters = new List<OSCmoothParameter>(smoothLayer.parameters);
            return parameters;
        }
    }
}
