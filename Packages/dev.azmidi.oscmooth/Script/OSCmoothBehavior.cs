using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using OSCmooth.Types;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace OSCmooth
{
    public class OSCmoothBehavior : MonoBehaviour, IEditorOnly
    {
        [HideInInspector] public OSCmoothSetup setup;
        [HideInInspector] public CustomAnimLayer[] prevLayers;
        [HideInInspector] public string prevParameterPath;
        [HideInInspector] public bool hasPreprocessed = false;
    }
}