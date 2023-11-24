using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using Tools.OSCmooth.Types;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDKBase.Editor.BuildPipeline;
using Tools.OSCmooth.Animation;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Tools.OSCmooth
{
    public class OSCmoothBehavior : MonoBehaviour, IEditorOnly
    {
        [HideInInspector] public OSCmoothLayers setup;
        [HideInInspector] public VRCAvatarDescriptor avatarDescriptor;
        [HideInInspector] public CustomAnimLayer[] prevLayers;
    }

    public class OSCmoothPostprocessor : IVRCSDKPostprocessAvatarCallback 
    {
        public int callbackOrder => 1024;

        public void OnPostprocessAvatar()
        {
            var _oscmBehaviors = Object.FindObjectsOfType<OSCmoothBehavior>();
            foreach (var oscm in _oscmBehaviors)
                oscm.avatarDescriptor.baseAnimationLayers = oscm.prevLayers;
        }
    }

    public class OSCmoothPreprocessor : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => -1024;
        public bool OnBuildRequested(VRCSDKRequestedBuildType type)
        {
            if (type != VRCSDKRequestedBuildType.Avatar) return true;

            var _oscmBehaviors = Object.FindObjectsOfType<OSCmoothBehavior>();

            foreach (var oscm in _oscmBehaviors)
            {
                if (oscm == null)
                    return false;

                var _avatarDescriptor = oscm.avatarDescriptor;
                if (_avatarDescriptor == null)
                    return false;

                oscm.prevLayers = _avatarDescriptor.baseAnimationLayers;
                if (oscm.prevLayers == null)
                    return false;

                var _newLayers = new CustomAnimLayer[_avatarDescriptor.baseAnimationLayers.Length];
                for (int i = 0; i < _avatarDescriptor.baseAnimationLayers.Length; i++)
                {
                    _newLayers[i] = ApplyAnimationLayer(oscm, i);
                }
                _avatarDescriptor.baseAnimationLayers = _newLayers;
            }
            return true;
        }

        private CustomAnimLayer ApplyAnimationLayer(OSCmoothBehavior oscm, int i)
        {
            var avatarDescriptor = oscm.avatarDescriptor;
            var _animatorPath = AssetDatabase.GetAssetPath(avatarDescriptor.baseAnimationLayers[i].animatorController);

            string directory = "Assets/OSCmooth/Temp/";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"Directory created: {directory}");
            }

            string _proxyPath = $"{directory}{avatarDescriptor.baseAnimationLayers[i].type}Proxy.controller";
            bool success = AssetDatabase.CopyAsset(_animatorPath, _proxyPath);
            AssetDatabase.SaveAssets();

            if (success)
            {
                var _proxyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(_proxyPath);
                var _animatorGUID = AssetDatabase.AssetPathToGUID(_proxyPath);
                var _animationHandler = new OSCmoothAnimationHandler();

                _animationHandler._animatorController = _proxyController;
                _animationHandler._parameters = oscm.setup.layers[i].parameters;
                _animationHandler._animExportDirectory = "Assets/OSCmooth/Temp/Generated/Smooth/" + "Animator_" + _animatorGUID + "/";
                _animationHandler._binaryExportDirectory = "Assets/OSCmooth/Temp/Generated/Binary/" + "Animator_" + _animatorGUID + "/";
                //_animationHandler.RemoveAllOSCmoothFromController();
                _animationHandler.CreateLayers();

                return new CustomAnimLayer
                {
                    type = avatarDescriptor.baseAnimationLayers[i].type,
                    animatorController = _proxyController,
                    isEnabled = avatarDescriptor.baseAnimationLayers[i].isEnabled,
                    isDefault = avatarDescriptor.baseAnimationLayers[i].isDefault,
                    mask = avatarDescriptor.baseAnimationLayers[i].mask
                };
            }
            return avatarDescriptor.baseAnimationLayers[i];
        }
    }
}
#endif