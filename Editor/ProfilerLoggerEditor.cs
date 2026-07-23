using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace JeangBoYuan.ProfilerLogger.Editor
{
    [CustomEditor(typeof(ProfilerLogger))]
    public class ProfilerLoggerEditor : UnityEditor.Editor
    {
        public VisualTreeAsset visualTree;
        public VisualTreeAsset oneTargetMetricsRow;
        
        // UI Element
        private VisualElement _showFpsToggle;
        private VisualElement _showFpsSetting;
        private VisualElement _exportTargets;
        private VisualElement _loadTargets;
        private ListView _targetMetricsList;
        
        // Target
        private ProfilerLogger _tgtComponent;

        private void OnEnable()
        {
            _tgtComponent = target as ProfilerLogger;
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            visualTree.CloneTree(root);
            
            // show fps setting
            _showFpsToggle = root.Q("showFpsToggle");
            _showFpsSetting = root.Q("showFpsSetting");
            _showFpsToggle.RegisterCallback<ChangeEvent<bool>>(OnShowFpsChanged);
            
            // export and load
            _exportTargets = root.Q("exportTargets");
            _exportTargets.RegisterCallback<ClickEvent>(OnExportTargets);
            _loadTargets = root.Q("loadTargets");
            _loadTargets.RegisterCallback<ClickEvent>(OnLoadTargets);
            
            // target metrics
            _targetMetricsList = root.Q<ListView>("targetMetricsList");
            _targetMetricsList.makeItem = () => oneTargetMetricsRow.Instantiate();
            _targetMetricsList.bindItem = BindOneTargetMetricsRow;
            
            return root;
        }

        private void OnShowFpsChanged(ChangeEvent<bool> evt)
        {
            _showFpsSetting.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnExportTargets(ClickEvent evt)
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Target Metrics", // title 
                ".",                 // directory
                $"{SceneManager.GetActiveScene().name}_target_metrics.csv", // default name 
                "csv"                // extension
                );

            if (path.Length == 0) return;

            var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            var writer = new StreamWriter(fs);
            
            // output header and each metrics
            writer.WriteLine("Disabled,Category,StatName");
            foreach (var tgt in _tgtComponent.targetMetrics)
            {
                writer.WriteLine($"{tgt.disabled},{tgt.category},{tgt.statName}");
            }

            writer.Flush();
            writer.Close();
            writer.Dispose();
        }

        private void OnLoadTargets(ClickEvent evt)
        {
            var path = EditorUtility.OpenFilePanel(
                "Load Target Metrics", // title
                ".",               // directory
                "csv");            // extension
            
            if (path.Length == 0) return;
            
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new StreamReader(fs);

            // Discard header
            reader.ReadLine();
            
            // Read each metrics
            var newMetrics = new List<TargetMetrics>();
            while (reader.ReadLine() is { } inLine)
            {
                // parse the line
                Regex regex = new("^(TRUE|FALSE),([^,]+),(.+)$", RegexOptions.IgnoreCase);
                var match = regex.Match(inLine);

                if (!match.Success
                    || !Enum.TryParse(match.Groups[2].ToString(), true, out Category inCategory))
                {
                    Debug.LogError($"The file ({path}) contains an invalid line: \"{inLine}\"");
                    return;
                }

                var inDisabled = string.Equals(match.Groups[1].ToString(), "TRUE",
                    StringComparison.InvariantCultureIgnoreCase);
                
                // Add to array
                newMetrics.Add(new TargetMetrics
                {
                    disabled = inDisabled,
                    category = inCategory,
                    statName = match.Groups[3].ToString()
                });
            }
            
            reader.Close();
            reader.Dispose();
            
            // Replace with loaded metrics
            Undo.RecordObject(_tgtComponent, "Load Target Metrics");
            _tgtComponent.targetMetrics = newMetrics;
        }

        private void BindOneTargetMetricsRow(VisualElement visual, int index)
        {
            // Get SerializedProperty of the corresponding element
            var prop = serializedObject.FindProperty("targetMetrics").GetArrayElementAtIndex(index);
            
            // bind each UI
            visual.Q<Toggle>("disabled").BindProperty(prop.FindPropertyRelative("disabled"));
            visual.Q<EnumField>("category").BindProperty(prop.FindPropertyRelative("category"));
            visual.Q<TextField>("statName").BindProperty(prop.FindPropertyRelative("statName"));
            
            // Set id label
            visual.Q<Label>("idLabel").text = $"#{index:00}";
        }
    }
}

