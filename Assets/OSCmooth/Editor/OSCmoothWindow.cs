using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using OSCTools.OSCmooth.Animation;
using OSCTools.OSCmooth.Types;
using System.Linq;

namespace OSCTools.OSCmooth
{
    public class OSCmoothWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avDescriptor;
        private AnimatorController _animatorController;
        private List<string> layers = new List<string>();

        private int _layerSelect = 4;

        private OSCmoothLayer _parameterAsset;
        private OSCmoothParameter _basisConfigurationParameter;

        private bool _showParameters = true;
        private bool _showGlobalConfiguration = false;
        private Vector2 paramMenuScroll;

        readonly public string[] binarySizeOptions = new string[]
        {
            "OFF","2 (1 Bit)","4 (2 Bit)","8 (3 Bit)","16 (4 Bit)","32 (5 Bit)", "64 (6 Bit)", "128 (7 Bit)"
        };

        //readonly private string[] _humanoidLayers = { "Base", "Additive", "Gesture", "Action", "FX" };

        [MenuItem("Tools/OSCmooth")]
        public static void ShowWindow()
        {
            AssetDatabase.Refresh();
            var window = EditorWindow.GetWindow<OSCmoothWindow>("OSCmooth");
            window.maxSize = new Vector2(512, 1024);
            window.minSize = new Vector2(368, 480);
            window.Show();
        }

        private void OnGUI()
        {
            DrawGUI();
        }

        void DrawGUI()
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
                // Building animation layer selection
                layers.Clear();
                foreach (VRCAvatarDescriptor.CustomAnimLayer animLayer in _avDescriptor.baseAnimationLayers)
                    layers.Add(animLayer.type.ToString());

                // Making sure the selector never goes beyond the length of the selection
                if (_layerSelect > layers.Count)
                    _layerSelect = layers.Count - 1;

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
                    layers.ToArray()
                );

                EditorGUILayout.Space(10f);

                _parameterAsset = (OSCmoothLayer)EditorGUILayout.ObjectField
                (
                   new GUIContent
                   (
                       "Config",
                       "A preset configuration that stores Parameter Configuration data. " +
                       "This is intended for saving configurations for use later or sharing."
                   ),
                   _parameterAsset,
                   typeof(OSCmoothLayer),
                   false
                );

                if (_parameterAsset == null)
                    _parameterAsset = ScriptableObject.CreateInstance<OSCmoothLayer>();

                if (_basisConfigurationParameter == null)
                    _basisConfigurationParameter = new OSCmoothParameter();

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

                    EditorUtility.SetDirty(_parameterAsset);
                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.EndHorizontal();

                _animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(_avDescriptor.baseAnimationLayers[_layerSelect].animatorController));

                if (_animatorController == null)
                {
                    EditorGUILayout.HelpBox("This Playable Layer must have an Animator Controller in order for OSCmooth to function.", MessageType.Warning, true);
                    return;
                }

                EditorGUILayout.Space();
                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Use Playable Layer Parameters",
                        "Populates the parameter list with exising float parameters in the applied Playable Layer controller."
                    )
                ))
                {
                    _parameterAsset = ScriptableObject.CreateInstance<OSCmoothLayer>();

                    foreach (AnimatorControllerParameter parameter in _animatorController.parameters)
                    {
                        if (parameter.type == AnimatorControllerParameterType.Float && !OSCmoothFilters.BlackList.Any(parameter.name.Contains))
                        {
                            _parameterAsset.parameters.Add(new OSCmoothParameter
                            {
                                paramName = parameter.name,
                                localSmoothness = _basisConfigurationParameter.localSmoothness,
                                remoteSmoothness = _basisConfigurationParameter.remoteSmoothness,
                                convertToProxy = _basisConfigurationParameter.convertToProxy,
                                binarySizeSelection = _basisConfigurationParameter.binarySizeSelection,
                                combinedParameter = _basisConfigurationParameter.combinedParameter
                            });
                        }
                    }

                    EditorUtility.SetDirty(_parameterAsset);
                    AssetDatabase.SaveAssets();

                }
                EditorGUILayout.Space();

                _showGlobalConfiguration = EditorGUILayout.Foldout(_showGlobalConfiguration, "Default Parameter Values");
                if (_showGlobalConfiguration)
                {
                    DrawParameterConfiguration(_basisConfigurationParameter, false);
                }

                _parameterAsset.configuration = _basisConfigurationParameter;

                EditorGUI.indentLevel = 0;

                _showParameters = EditorGUILayout.Foldout(_showParameters, "Parameter Configuration");
                if (_parameterAsset.parameters != null && _parameterAsset.parameters.Count() > 0)
                {
                    if (GUILayout.Button("Remove All"))
                    {
                        Undo.RecordObject(_parameterAsset, "Remove All Parameters");
                        _parameterAsset.parameters = new List<OSCmoothParameter>();
                        return;
                    }
                }

                EditorGUI.indentLevel = 0;

                paramMenuScroll = EditorGUILayout.BeginScrollView(paramMenuScroll);
                if (_showParameters && _parameterAsset != null)
                {
                    foreach (OSCmoothParameter parameter in _parameterAsset.parameters)
                    {
                        if (parameter == null)
                            continue;

                        EditorGUI.indentLevel = 0;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(parameter.isVisible ? "v" : ">", GUILayout.Width(20)))
                            {
                                Undo.RecordObject(_parameterAsset, "Set Parameter Visible");
                                parameter.isVisible = !parameter.isVisible;
                            }

                            EditorGUI.BeginChangeCheck();
                            string paramName = parameter.paramName;
                            paramName = EditorGUILayout.TextField(paramName);
                            if (EditorGUI.EndChangeCheck() && parameter != null)
                            {
                                Undo.RecordObject(_parameterAsset, "Change Parameter Name");
                                parameter.paramName = paramName;
                                return;
                            }

                            GUI.color = Color.red;
                            if (GUILayout.Button("X", GUILayout.Width(40)))
                            {
                                Undo.RecordObject(_parameterAsset, "Remove Parameter");
                                _parameterAsset.parameters.Remove(parameter);
                                break;
                            }

                            GUI.color = Color.white;
                        }
                        EditorGUI.indentLevel = 2;
                        if (parameter.isVisible)
                        {
                            DrawParameterConfiguration(parameter);
                        }
                    }
                }

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
                    OSCmoothParameter param = new OSCmoothParameter
                    {
                        paramName = _basisConfigurationParameter.paramName,
                        localSmoothness = _basisConfigurationParameter.localSmoothness,
                        remoteSmoothness = _basisConfigurationParameter.remoteSmoothness,
                        convertToProxy = _basisConfigurationParameter.convertToProxy,
                        binarySizeSelection = _basisConfigurationParameter.binarySizeSelection,
                        combinedParameter = _basisConfigurationParameter.combinedParameter
                    };
                    _parameterAsset.parameters.Add(param);

                    EditorUtility.SetDirty(_parameterAsset);
                    AssetDatabase.SaveAssets();
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(20);

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Apply OSCmooth to Layer",
                        "Creates a new Layer in the selected Animator Controller that will apply smoothing " +
                        "to the listed configured parameters."
                    )
                ))
                {
                    string animatorGUID;
                    long id;

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_animatorController, out animatorGUID, out id);

                    OSCmoothAnimationHandler._animatorController = _animatorController;
                    OSCmoothAnimationHandler._parameters = _parameterAsset.parameters;
                    OSCmoothAnimationHandler._animExportDirectory = "Assets/OSCmooth/Generated/Smooth/" + "Animator_" + animatorGUID + "/";
                    OSCmoothAnimationHandler._binaryExportDirectory = "Assets/OSCmooth/Generated/Binary/" + "Animator_" + animatorGUID + "/";

                    Undo.RecordObject(OSCmoothAnimationHandler._animatorController, "Apply OSCmooth to Layer");



                    OSCmoothAnimationHandler.RemoveAllOSCmoothFromController();
                    OSCmoothAnimationHandler.CreateLayers();
                }

                EditorGUILayout.Space(20);

                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Revert OSCmooth from Layer",
                        "Removes all OSCmooth content from the Animator, and reverts all State Machine parameters back to their original naming." +
                        "\n\nWARNING: Will recycle all associated animations to the selected Playable Layer as well, proceed with caution."
                    )
                ))
                {
                    string animatorGUID;
                    long id;

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_animatorController, out animatorGUID, out id);

                    OSCmoothAnimationHandler._animatorController = _animatorController;
                    OSCmoothAnimationHandler._parameters = _parameterAsset.parameters;

                    Undo.RecordObject(OSCmoothAnimationHandler._animatorController, "Revert OSCmooth in Layer");
                    OSCmoothAnimationHandler.RemoveAllOSCmoothFromController();

                    FileUtil.DeleteFileOrDirectory("Assets/OSCmooth/Generated/Smooth/" + "Animator_" + animatorGUID);
                    FileUtil.DeleteFileOrDirectory("Assets/OSCmooth/Generated/Smooth/" + "Animator_" + animatorGUID + ".meta");
                    FileUtil.DeleteFileOrDirectory("Assets/OSCmooth/Generated/Binary/" + "Animator_" + animatorGUID);
                    FileUtil.DeleteFileOrDirectory("Assets/OSCmooth/Generated/Binary/" + "Animator_" + animatorGUID + ".meta");


                    AssetDatabase.Refresh();
                }

                EditorGUILayout.Space(20);
            }
        }

        public void DrawParameterConfiguration(OSCmoothParameter parameter, bool removable = true)
        {
            EditorGUIUtility.labelWidth = 210;

            EditorGUILayout.LabelField
            (
                new GUIContent
                (
                    "Smoothness",
                    "How much of a percentage the previous float values influence the current one."
                )
            );

            EditorGUI.indentLevel = 3;
            EditorGUIUtility.labelWidth = 220;

            float localSmoothness = parameter.localSmoothness;
            float remoteSmoothness = parameter.remoteSmoothness;
            bool convertToProxy = parameter.convertToProxy;
            int binarySizeSelection = parameter.binarySizeSelection;
            bool combinedParameter = parameter.combinedParameter;

            EditorGUI.BeginChangeCheck();
            {
                localSmoothness = EditorGUILayout.FloatField
                (
                    new GUIContent
                    (
                        "Local Smoothness",
                        "How much % smoothness you (locally) will see when a parameter " +
                        "changes value. Higher values represent more smoothness, and vice versa."
                    ),
                    localSmoothness
                );

                remoteSmoothness = EditorGUILayout.FloatField
                (
                    new GUIContent
                    (
                        "Remote Smoothness",
                        "How much % smoothness remote users will see when a parameter " +
                        "changes value. Higher values represent more smoothness, and vice versa."
                    ),
                    remoteSmoothness
                );

                convertToProxy = EditorGUILayout.Toggle
                (
                    new GUIContent
                    (
                        "Proxy Conversion",
                        "Automatically convert existing animations to use the Proxy (output) float."
                    ),
                    convertToProxy
                );

                binarySizeSelection = EditorGUILayout.Popup
                (
                    new GUIContent
                    (
                        "Binary Resolution",
                        "How many steps a Binary Parameter can make. Higher values are more accurate, " +
                        "while lower values are more economic for parameter space. Recommended to use a " +
                        "Resolution of 16 or less for more space savings."
                    ),
                    binarySizeSelection,
                    binarySizeOptions
                );


                combinedParameter = EditorGUILayout.Toggle
                (
                    new GUIContent
                    (
                        "Combined Parameter (+1 Bit)",
                        "Does this parameter go from positive to negative? " +
                        "This option will add an extra bool to keep track of the " +
                        "positive/negative of the parameter."
                    ),
                    combinedParameter
                );



            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_parameterAsset, "Change Parameter Values");
                parameter.localSmoothness = localSmoothness;
                parameter.remoteSmoothness = remoteSmoothness;
                parameter.convertToProxy = convertToProxy;
                parameter.binarySizeSelection = binarySizeSelection;
                parameter.combinedParameter = combinedParameter;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            /*
            if (removable)
            {
                if (GUILayout.Button
                (
                    new GUIContent
                    (
                        "Remove Parameter",
                        "Removes specified parameter from smoothness creation."
                    ),
                    GUILayout.MaxWidth((float)Screen.width - 248f)
                ))
                {
                    Undo.RecordObject(_parameterAsset, "Remove Parameter");
                    _parameterAsset.parameters.Remove(parameter);
                }
            }
            */

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }
}