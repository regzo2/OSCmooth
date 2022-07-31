using OSCTools.OSCmooth.Animation;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace OSCTools.OSCmooth
{
    public class OSCmoothLayer
    {
        // for Animation creation purposes:
        public float localSmoothness;
        public float remoteSmoothness;
        public string paramName;

        // This setting renames any instance of the base parameter
        // in the animator with the Proxy parameter. This is intended
        // for total conversion between the base and Proxy parameters,
        // after conversion it should be expected to use the Proxy parameter 
        // in future animations and this will act as a 'one-time' use switch.
        // Ideally it will not flip any existing
        public bool flipInputOutput;

        // for EditorWindow purposes: This is intended to hide parameter settings.
        public bool isVisible;

        public OSCmoothLayer() 
        {
            localSmoothness = 0.5f;
            remoteSmoothness = 0.9f;
            paramName = "NewParameter";
            isVisible = true;
            flipInputOutput = false;
        }
    }

    public class OSCmoothWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avDescriptor;
        private AnimatorController _animatorController;
        List<OSCmoothLayer> _smoothLayer;
        private int _layerSelect = 4;
        private string _baseParamName;
        private bool _showParameters;

        readonly private string[] _animatorSelection = new string[]
        {
            "Base","Additive","Gesture","Action","FX"
        };

        [MenuItem("Tools/OSCmooth")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<OSCmoothWindow>("OSCmooth");
        }

        private void OnGUI()
        {
            _avDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField
            (
                new GUIContent
                (
                    "Avatar",
                    "The VRC Avatar that will have the smoothing animation layers set up on. " +
                    "The Avatar must have a VRCAvatarDescriptor to show up in this field."
                ),
                _avDescriptor,
                typeof(VRCAvatarDescriptor),
                true
            );

            if (_avDescriptor != null)
            {
                _layerSelect = EditorGUILayout.Popup
                (
                    new GUIContent
                    (
                        "Layer",
                        "This selects what VRChat Playable Layer you would like to set up " +
                        "the following Binary Animation Layer into. A layer must be populated " +
                        "in order for the tool to properly set up an Animation Layer."
                    ),
                    _layerSelect,
                    _animatorSelection
                );

                _animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(_avDescriptor.baseAnimationLayers[_layerSelect].animatorController));

                GUIStyle subMenuFoldoutStyle = EditorStyles.foldout;
                subMenuFoldoutStyle.margin.left = -18;

                _showParameters = EditorGUILayout.Foldout(_showParameters, "Parameter Configuration");

                subMenuFoldoutStyle.margin.left = 18;
                GUIStyle subMenuTextStyle = EditorStyles.textField;
                subMenuTextStyle.margin.left = 25;
                GUIStyle subMenuFloatStyle = EditorStyles.textField;
                subMenuFloatStyle.margin.left = 25;

                if (_showParameters)
                {
                    foreach (OSCmoothLayer layer in _smoothLayer)
                    {
                        layer.isVisible = EditorGUILayout.Foldout(layer.isVisible, layer.paramName);

                        if (layer.isVisible)
                        {
                            layer.paramName = EditorGUILayout.TextField
                            (
                                new GUIContent
                                (
                                    "",
                                    "The specific float parameter that will have the smoothness layer have applied to."
                                ),
                                layer.paramName,
                                subMenuTextStyle
                            );

                            EditorGUILayout.BeginHorizontal();
                            layer.localSmoothness = EditorGUILayout.FloatField
                            (
                                new GUIContent
                                (
                                    "Local Smoothness %",
                                    "How much % smoothness you (locally) will see when a parameter" +
                                    "changes value. Higher values represent more smoothness, and vice versa."
                                ),
                                layer.localSmoothness,
                                subMenuFloatStyle
                            );

                            layer.remoteSmoothness = EditorGUILayout.FloatField
                            (
                                new GUIContent
                                (
                                    "Remote Smoothness %",
                                    "How much % smoothness remote users will see when a parameter" +
                                    "changes value. Higher values represent more smoothness, and vice versa."
                                ),
                                layer.remoteSmoothness,
                                subMenuFloatStyle
                            );

                            EditorGUILayout.EndHorizontal();

                            layer.switchProxy = EditorGUILayout.Toggle
                            (
                                new GUIContent
                                (
                                    "Proxy Switch",
                                    "This setting will automatically switch out the provided base parameter in exising animations  " +
                                    "out with the generated Proxy parameter from this tool. This is useful if you're looking to " +
                                    "convert your existing animation setup initially.\n\nWARNING: This setting is " +
                                    "potentially very destructive to the animator, It would be recommended to back-" +
                                    "up the Animator before using this setting, as a precaution, or manually swap out " +
                                    "the animations to use the Proxy float."
                                ),
                                layer.switchProxy
                            );

                            EditorGUILayout.Space();

                            if (GUILayout.Button
                            (
                                new GUIContent
                                (
                                    "Delete Layer",
                                    "Removes specified layer from the smoothness creation layers."
                                )
                            ))
                            {
                                _smoothLayer.Remove(layer);
                            }

                            EditorGUILayout.Space();
                        }
                    }
                }

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Add New Parameter",
                        "Adds a new Parameter configuration."
                    )
                ))
                {
                    _smoothLayer.Add(new OSCmoothLayer());
                }

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Apply OSCmooth to Animator",
                        "Creates a new Layer in the selected Animator Controller that will apply smoothing " +
                        "to the listed configured parameters."
                    )
                ))
                {
                    OSCmoothAnimationHandler animHandler = new OSCmoothAnimationHandler();

                    animHandler.animatorController = _animatorController;
                    animHandler.smoothLayer = _smoothLayer;

                    animHandler.CreateSmoothAnimationLayer();
                }
            }
        }
    }
}