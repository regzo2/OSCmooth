using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using OSCmooth.Types;

namespace OSCmooth
{
    public class OSCmoothBehavior : MonoBehaviour, IEditorOnly
    {
        [HideInInspector] public OSCmoothLayers setup;
        [HideInInspector] public VRCAvatarDescriptor avatarDescriptor;
        [HideInInspector] public CustomAnimLayer[] prevLayers;
    }
}