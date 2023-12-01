using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using System.IO;
using UnityEditor.Animations;
using UnityEditor.Build;
using VRC.SDK3.Avatars.Components;
using OSCmooth.Editor.Animation;
using OSCmooth.Util;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Linq;
using OSCmooth.Types;
using System.Collections.Generic;

namespace OSCmooth.Editor
{
    public class OSCmoothPostprocessor : IVRCSDKPostprocessAvatarCallback, IVRCSDKOrderedCallback, IOrderedCallback
    {
        public int callbackOrder => 0;

        public void OnPostprocessAvatar()
        {
            OSCmoothBehavior[] _oscmBehaviors = Object.FindObjectsOfType<OSCmoothBehavior>();
            foreach (OSCmoothBehavior oscm in _oscmBehaviors)
            {
                VRCAvatarDescriptor _avatarDescriptor = oscm.GetComponent<VRCAvatarDescriptor>();
                _avatarDescriptor.baseAnimationLayers = oscm.prevLayers;
                _avatarDescriptor.expressionParameters.parameters = oscm.prevParameters;
            }
        }
    }

	public class OSCmoothPreprocessor : IVRCSDKBuildRequestedCallback, IVRCSDKOrderedCallback, IOrderedCallback
	{
		public int callbackOrder => 0;

		public bool OnBuildRequested(VRCSDKRequestedBuildType type)
		{
			if ((int)type > 0)
			{
				return true;
			}
			OSCmoothBehavior[] _oscmBehaviors = Object.FindObjectsOfType<OSCmoothBehavior>();
			OSCmoothBehavior[] array = _oscmBehaviors;
			foreach (OSCmoothBehavior oscm in array)
			{
				if (oscm == null)
				{
					return false;
				}
				VRCAvatarDescriptor _avatarDescriptor = oscm.GetComponent<VRCAvatarDescriptor>();
				oscm.prevLayers = (CustomAnimLayer[])(object)new CustomAnimLayer[_avatarDescriptor.baseAnimationLayers.Length];
				for (int j = 0; j < _avatarDescriptor.baseAnimationLayers.Length; j++)
				{
					oscm.prevLayers[j] = _avatarDescriptor.baseAnimationLayers[j];
				}
				oscm.prevParameters = (VRCExpressionParameters.Parameter[])(object)new VRCExpressionParameters.Parameter[_avatarDescriptor.expressionParameters.parameters.Length];
				for (int i = 0; i < _avatarDescriptor.expressionParameters.parameters.Length; i++)
				{
					oscm.prevParameters[i] = _avatarDescriptor.expressionParameters.parameters[i];
				}
				if (!oscm.setup.parameters.AppendToExpressionParameters(_avatarDescriptor))
				{
					return false;
				}
				ApplyAnimationLayers(_avatarDescriptor, oscm);
			}
			AssetDatabase.SaveAssets();
			return true;
		}

		public static void ApplyAnimationLayers(VRCAvatarDescriptor avatarDescriptor, OSCmoothBehavior behavior)
		{
			CustomAnimLayer[] layers = avatarDescriptor.baseAnimationLayers;
			for (int i = 0; i < layers.Length; i++)
			{
				if (layers[i].animatorController == null)
					continue;

				string _animatorPath = AssetDatabase.GetAssetPath(layers[i].animatorController);
				AnimLayerType _type = layers[i].type;
				List<OSCmoothParameter> _layerParameters = behavior.setup.parameters
					.Where(p => Extensions.Contains(p.layerMask, _type)).ToList();

				string _directory = "Assets/OSCmooth/Temp/";
				if (!Directory.Exists(_directory))
				{
					Directory.CreateDirectory(_directory);
					Debug.Log("Directory created: " + _directory);
				}

				string _proxyPath = $"{_directory}{layers[i].type}Proxy.controller";
				if (!AssetDatabase.CopyAsset(_animatorPath, _proxyPath)) continue;

				AssetDatabase.SaveAssets();
				AnimatorController _layerProxyController = AssetDatabase.LoadAssetAtPath<AnimatorController>(_proxyPath);
				string _proxyGUID = AssetDatabase.AssetPathToGUID(_proxyPath);
				new OSCmoothAnimationHandler
				(
					_layerParameters,
					_layerProxyController,
					_directory + "Generated/Smooth/Animator_" + _proxyGUID + "/",
					_directory + "Generated/Binary/Animator_" + _proxyGUID + "/")
				.CreateLayers();

				layers[i].animatorController = _layerProxyController;
			}
			avatarDescriptor.baseAnimationLayers = layers;
		}
	}
}

