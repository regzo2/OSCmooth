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
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Attributes;
using VRC.SDKBase.Editor.V3;
using VRC.Core;
using VRC.SDK3.Builder;

namespace OSCmooth.Editor
{
	/*
	public class OSCmoothPostprocessor : IVRCSDKPostprocessAvatarCallback, IVRCSDKOrderedCallback, IOrderedCallback
    {
        public int callbackOrder => 0;

        public void OnPostprocessAvatar()
        {
            OSCmoothBehavior[] _oscmBehaviors = Object.FindObjectsOfType<OSCmoothBehavior>()
													  .Where(o => o.hasPreprocessed)
													  .ToArray();

            foreach (OSCmoothBehavior oscm in _oscmBehaviors)
            {
                VRCAvatarDescriptor _avatarDescriptor = oscm.GetComponent<VRCAvatarDescriptor>();
                _avatarDescriptor.baseAnimationLayers = oscm.prevLayers;
                _avatarDescriptor.expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(oscm.prevParameterPath);
            }
        }
    }
	*/

	public class OSCmoothPreprocessor : IVRCSDKPreprocessAvatarCallback
	{
		public int callbackOrder => -1024;

		public bool OnPreprocessAvatar(GameObject avatar)
		{
			var _oscm = avatar.GetComponent<OSCmoothBehavior>();
			if (_oscm == null)
				return true;

			VRCAvatarDescriptor _avatarDescriptor = _oscm.GetComponent<VRCAvatarDescriptor>();
			_oscm.prevLayers = new CustomAnimLayer[_avatarDescriptor.baseAnimationLayers.Length];
			for (int j = 0; j < _avatarDescriptor.baseAnimationLayers.Length; j++)
				_oscm.prevLayers[j] = _avatarDescriptor.baseAnimationLayers[j];

			_oscm.prevParameterPath = AssetDatabase.GetAssetPath(_avatarDescriptor.expressionParameters);

			System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
			ApplyParameters(_avatarDescriptor, _oscm);
			ApplyAnimationLayers(_avatarDescriptor, _oscm);
			AssetDatabase.SaveAssets();
			watch.Stop();
			Debug.Log($"Time OSCmooth took to generate: {watch.ElapsedMilliseconds} ms");
			_oscm.hasPreprocessed = true;
			return true;
		}

		public static void ApplyAnimationLayers(VRCAvatarDescriptor avatarDescriptor, OSCmoothBehavior oscm)
		{
			CustomAnimLayer[] layers = avatarDescriptor.baseAnimationLayers;

			new OSCmoothAnimationHandler
			(
				oscm.setup,
				layers,
				false
			)
			.CreateLayers();

			avatarDescriptor.baseAnimationLayers = layers;
		}

		private static void ApplyParameters(VRCAvatarDescriptor avatarDescriptor, OSCmoothBehavior oscm)
		{
			string _directory = $"Assets/OSCmooth/Temp/{oscm.gameObject.name}/";
				if (!Directory.Exists(_directory))
				{
					Directory.CreateDirectory(_directory);
					Debug.Log("Directory created: " + _directory);
				}
			oscm.setup.CreateExpressionParameters(avatarDescriptor, _directory);
		}
	}
}

