using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using OSCTools.OSCmooth.Animation;
using OSCTools.OSCmooth.Types;
using OSCTools.OSCmooth.Util;
using System.IO;

namespace OSCTools.OSCmooth
{
    public class OSCmoothWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avDescriptor;
        private AnimatorController _animatorController;
        private int _layerSelect = 4;
        private OSCmoothLayer _parameterAsset;
        private bool _showParameters = true;
        private Vector2 paramMenuScroll;

        readonly private string[] _animatorSelection = new string[]
        {
            "Base","Additive","Gesture","Action","FX"
        };

        [MenuItem("Tools/OSCmooth")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<OSCmoothWindow>("OSCmooth");
            window.maxSize = new Vector2(512, 1024);
            window.minSize = new Vector2(368, 768);
            window.Show();
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

                EditorGUILayout.Space(10f);

                _parameterAsset = (OSCmoothLayer)EditorGUILayout.ObjectField
                (
                    new GUIContent
                    (
                        "Config",
                        "The VRC Avatar that will have the smoothing animation layers set up on. " +
                        "The Avatar must have a VRCAvatarDescriptor to show up in this field."
                    ),
                    _parameterAsset,
                    typeof(OSCmoothLayer),
                    false
                );

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (_parameterAsset == null)
                    _parameterAsset = ScriptableObject.CreateInstance<OSCmoothLayer>();

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Save Config",
                        "Saves Parameter configuration into a JSON readable text file."
                    ),
                    GUILayout.MaxWidth((float)Screen.width - 159f)
                ))
                {
                    if (AssetDatabase.GetAssetPath(_parameterAsset) == string.Empty)
                        AssetDatabase.CreateAsset(_parameterAsset, EditorUtility.SaveFilePanelInProject("Save OSCmooth Configuration", "OSCmoothConfig", "asset", ""));

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                EditorGUILayout.EndHorizontal();

                _animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(_avDescriptor.baseAnimationLayers[_layerSelect].animatorController));

                EditorGUILayout.Space();

                _showParameters = EditorGUILayout.Foldout(_showParameters, "Parameter Configuration");

                EditorGUI.indentLevel = 1;

                paramMenuScroll = EditorGUILayout.BeginScrollView(paramMenuScroll);
                if (_showParameters && _parameterAsset != null)
                {                    
                    foreach (OSCmoothParameter layer in _parameterAsset.parameters)
                    {
                        EditorGUI.indentLevel = 2;
                        layer.isVisible = EditorGUILayout.Foldout(layer.isVisible, layer.paramName);

                        EditorGUI.indentLevel = 4;
                        if (layer.isVisible)
                        {
                            EditorGUIUtility.labelWidth = 210;

                            layer.paramName = EditorGUILayout.TextField
                            (
                                new GUIContent
                                (
                                    "",
                                    "The specific float parameter that will have the smoothness layer have applied to."
                                ),
                                layer.paramName
                            );

                            EditorGUILayout.LabelField
                            (
                                new GUIContent
                                (
                                    "Smoothness",
                                    "How much of a percentage the previous float values influence the current one."
                                )
                            );

                            EditorGUI.indentLevel = 6;
                            EditorGUIUtility.labelWidth = 240;

                            layer.localSmoothness = EditorGUILayout.FloatField
                            (
                                new GUIContent
                                (
                                    "Local Smoothness",
                                    "How much % smoothness you (locally) will see when a parameter " +
                                    "changes value. Higher values represent more smoothness, and vice versa."
                                ),
                                layer.localSmoothness
                            );

                            layer.remoteSmoothness = EditorGUILayout.FloatField
                            (
                                new GUIContent
                                (
                                    "Remote Smoothness",
                                    "How much % smoothness remote users will see when a parameter " +
                                    "changes value. Higher values represent more smoothness, and vice versa."
                                ),
                                layer.remoteSmoothness
                            );

                            layer.convertToProxy = EditorGUILayout.Toggle
                            (
                                new GUIContent
                                (
                                    "Proxy Conversion",
                                    "Automatically convert existing animations to use the Proxy (output) float."
                                ),
                                layer.convertToProxy
                            );

                            layer.flipInputOutput = EditorGUILayout.Toggle
                            (
                                new GUIContent
                                (
                                    "Flip Input/Output",
                                    "Sets the Base parameter to be the output parameter from the smoother layer, and " +
                                    "sets the Proxy parameter as the input driver parameter. Useful for apps that can " +
                                    "drive the Proxy parameter like VRCFaceTracking binary parameters."
                                ),
                                layer.flipInputOutput
                            );

                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button
                            (
                                new GUIContent
                                (
                                    "Remove Parameter",
                                    "Removes specified parameter from smoothness creation."
                                ),
                                GUILayout.MaxWidth((float)Screen.width - 70f)
                            ))
                            {
                                _parameterAsset.parameters.Remove(layer);
                            }

                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.Space();
                        }
                    }
                }

                EditorUtility.SetDirty(_parameterAsset);

                EditorGUILayout.Space();

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Add New Parameter",
                        "Adds a new Parameter configuration."
                    )
                ))
                {
                    OSCmoothParameter param = new OSCmoothParameter();
                    _parameterAsset.parameters.Add(param);
                }


                EditorGUILayout.EndScrollView();

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Apply OSCmooth to Animator",
                        "Creates a new Layer in the selected Animator Controller that will apply smoothing " +
                        "to the listed configured parameters."
                    ),
                    GUILayout.MaxWidth(Screen.width * .9f)
                ))
                {
                    OSCmoothAnimationHandler animHandler = new OSCmoothAnimationHandler();

                    animHandler.animatorController = _animatorController;
                    animHandler.parameters = _parameterAsset.parameters;

                    animHandler.CreateSmoothAnimationLayer();
                }

                EditorGUILayout.Space(40);
            }
        }
    }
}