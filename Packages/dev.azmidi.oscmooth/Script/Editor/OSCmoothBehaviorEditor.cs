using System.Collections.Generic;
using System.Linq;
using OSCmooth.Types;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace OSCmooth.Editor
{
    [CustomEditor(typeof(OSCmoothBehavior))]
    public class OSCmoothBehaviorEditor : UnityEditor.Editor
    {
        private OSCmoothBehavior _behavior;

        private bool[] _layerVisible = new bool[10];
        private bool _showGlobalConfiguration = false;
        private Vector2 paramMenuScroll;

        readonly public string[] binarySizeOptions = new string[]
        {
            "OFF",
            "2 (1 Bit)",
            "4 (2 Bit)",
            "8 (3 Bit)",
            "16 (4 Bit)",
            "32 (5 Bit)",
            "64 (6 Bit)", 
            "128 (7 Bit)"
        };

        void OnEnable ()
        {
            _behavior = (OSCmoothBehavior)target;

            if (_behavior == null)
                return;

            if (_behavior.setup == null)
                _behavior.setup = CreateInstance<OSCmoothLayers>();

            if (_behavior.avatarDescriptor == null)
                _behavior.avatarDescriptor = _behavior.GetComponent<VRCAvatarDescriptor>();

            if (_behavior.setup.layers == null || _behavior.setup.layers.Count != _behavior.avatarDescriptor.baseAnimationLayers.Length)
            {
                _behavior.setup.layers = _behavior.avatarDescriptor.baseAnimationLayers
                    .Select(layer => new OSCmoothLayer(layer))
                    .ToList();
            }
        }

        public override void OnInspectorGUI()
        {
            DrawAnimationLayerSelection(_behavior.avatarDescriptor);
            DrawConfigurationSection(_behavior);
            DrawGlobalConfigurationSection(_behavior.setup);
        }

        private void DrawAnimationLayerSelection(VRCAvatarDescriptor avatarDescriptor)
        {
            EditorGUILayout.Space(10f);

            _behavior.setup = (OSCmoothLayers)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Config",
                    "A preset configuration that stores Parameter Configuration data. " +
                    "This is intended for saving configurations for use later or sharing."),
                _behavior.setup,
                typeof(OSCmoothLayers),
                false);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                new GUIContent(
                    "Save Config",
                    "Saves Parameter configuration to an asset."),
                GUILayout.MaxWidth((float)Screen.width - 159f)))
            {
                if (AssetDatabase.GetAssetPath(_behavior.setup) == string.Empty)
                    AssetDatabase.CreateAsset(_behavior.setup, EditorUtility.SaveFilePanelInProject("Save OSCmooth Configuration", "OSCmoothConfig", "asset", ""));

                EditorUtility.SetDirty(_behavior.setup);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(
                new GUIContent(
                    "Use Playable Layer Parameters",
                    "Populates the parameter list with existing float parameters in all available."
                )))
            {
                for (int i = 0; i < avatarDescriptor.baseAnimationLayers.Length; i++)
                {
                    var animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                        AssetDatabase.GetAssetPath(avatarDescriptor.baseAnimationLayers[i].animatorController));

                    if (animatorController == null)
                        continue;

                    var parameters = animatorController.parameters;

                    _behavior.setup.layers[i].parameters.Clear();

                    foreach (var parameter in parameters)
                    {
                        if (_behavior.setup.layers.Count > i)
                        {
                            if (_behavior.setup.layers[i].parameters == null)
                                _behavior.setup.layers[i].parameters = new List<OSCmoothParameter>();
                            var _configParam = _behavior.setup.configParam;
                            _behavior.setup.layers[i].parameters.Add(new OSCmoothParameter
                            {
                                paramName = parameter.name,
                                binarySizeSelection = _configParam.binarySizeSelection,
                                combinedParameter = _configParam.combinedParameter,
                                localSmoothness = _configParam.localSmoothness,
                                convertToProxy = _configParam.convertToProxy,
                                remoteSmoothness = _configParam.remoteSmoothness,
                                isVisible = false,
                            });

                        }
                        else
                        {
                            Debug.LogError($"Invalid setup for adding parameter at index {i}. Check your initialization logic.");
                        }
                    }
                }
                EditorUtility.SetDirty(_behavior.setup);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space();
        }

        private void DrawConfigurationSection(OSCmoothBehavior behavior)
        {
            _showGlobalConfiguration = EditorGUILayout.Foldout(_showGlobalConfiguration, "Default Parameter Values");
            if (_showGlobalConfiguration)
            {
                DrawParameterConfiguration(_behavior.setup.configParam, false);
            }
        }

        private void DrawGlobalConfigurationSection(OSCmoothLayers setup)
        {
            paramMenuScroll = EditorGUILayout.BeginScrollView(paramMenuScroll);
            if (setup != null)
            {
                for (int i = 0; i < setup.layers.Count; i++)
                {
                    _layerVisible[i] = EditorGUILayout.Foldout(_layerVisible[i], $"{setup.layers[i].associate.type} Parameters");
                    if (true)
                    {
                        EditorGUI.indentLevel = 1; // Adjust the indent level as needed
                        DrawParameterList(setup.layers[i].parameters, setup);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawParameterList(List<OSCmoothParameter> parameters, OSCmoothLayers setup)
        {
            if (parameters == null)
                parameters = new List<OSCmoothParameter>();

            Debug.Log($"ParameterCount: {parameters.Count}");

            for (int j = 0; j < parameters.Count; j++)
            {
                EditorGUI.indentLevel = 0;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(parameters[j].isVisible ? "v" : ">", GUILayout.Width(20)))
                    {
                        Undo.RecordObject(setup, "Set Parameter Visible");
                        parameters[j].isVisible = !parameters[j].isVisible;
                    }

                    EditorGUI.BeginChangeCheck();
                    string paramName = parameters[j].paramName;
                    paramName = EditorGUILayout.TextField(paramName);

                    if (EditorGUI.EndChangeCheck() && parameters[j] != null)
                    {
                        Undo.RecordObject(setup, "Change Parameter Name");
                        parameters[j].paramName = paramName;
                        continue;
                    }

                    GUI.color = Color.red;

                    if (GUILayout.Button("X", GUILayout.Width(40)))
                    {
                        Undo.RecordObject(setup, "Remove Parameter");
                        parameters.Remove(parameters[j]);
                        break;
                    }

                    GUI.color = Color.white;
                }

                EditorGUI.indentLevel = 2;
                if (parameters[j].isVisible)
                    DrawParameterConfiguration(parameters[j]);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(
                new GUIContent(
                    "Add New Parameter",
                    "Adds a new Parameter configuration."
                )))
            {
                if (parameters == null)
                    parameters = new List<OSCmoothParameter>();
                var _configParam = _behavior.setup.configParam;
                OSCmoothParameter param = new OSCmoothParameter
                {
                    paramName = _configParam.paramName,
                    localSmoothness = _configParam.localSmoothness,
                    remoteSmoothness = _configParam.remoteSmoothness,
                    convertToProxy = _configParam.convertToProxy,
                    binarySizeSelection = _configParam.binarySizeSelection,
                    combinedParameter = _configParam.combinedParameter
                };

                parameters.Add(param);
                EditorUtility.SetDirty(setup);
                AssetDatabase.SaveAssets();
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
                parameter.localSmoothness = localSmoothness;
                parameter.remoteSmoothness = remoteSmoothness;
                parameter.convertToProxy = convertToProxy;
                parameter.binarySizeSelection = binarySizeSelection;
                parameter.combinedParameter = combinedParameter;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }
}
