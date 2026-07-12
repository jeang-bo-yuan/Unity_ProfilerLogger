using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace JeangBoYuan.ProfilerLogger
{
    public class ProfilerLogger : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField, Tooltip("The output path to use when \"Play in Editor\"")]
        private PathStruct outputPathEditor = new PathStruct
        {
            prefix = PathPrefix.AssetsPath,
            path = "Profiler/ProfilerLogger.csv"
        };
#endif
        [SerializeField, Tooltip("The output path to use in built game")]
        private PathStruct outputPathGameBuild = new PathStruct
        {
            prefix = PathPrefix.PersistentDataPath,
            path = "Profiler/ProfilerLogger.csv"
        };
        
        [SerializeField, Tooltip("The minimum interval (in seconds) between each sample")]
        private float sampleIntervalSeconds = 1f;
        [SerializeField]
        private bool showFPSOnGUI = false;
        [SerializeField, Tooltip("The position to show FPS. The left-botton is (0, 0).")]
        private Vector2 showFPSPosition = new(100f, 100f); 
        
        [SerializeField, Tooltip("Specify which profiler counter's value need to be logged")]
        private List<TargetMetrics> targetMetrics = new List<TargetMetrics>
        {
            new TargetMetrics(){category = Category.Render, statName = "CPU Total Frame Time"},
            new TargetMetrics(){category = Category.Render, statName = "GPU Frame Time"},
            
            new TargetMetrics(){category = Category.Scripts, statName = "PlayerLoop"},
            new TargetMetrics(){category = Category.Scripts, statName = "RenderLoop"},
            
            // Important metrics for rendering: Waiting Command + Process Command + Present + VSync
            new TargetMetrics(){category = Category.Render, statName = "Gfx.WaitForGfxCommandsFromMainThread"},
            new TargetMetrics(){category = Category.Render, statName = "Gfx.ProcessCommands"},
            new TargetMetrics(){category = Category.Render, statName = "Gfx.PresentFrame"}, // wait GPU to render and present, include VSync
            new TargetMetrics(){category = Category.Render, statName = "Gfx.WaitForPresentOnGfxThread"}, // main thread is ready for next frame, but GPU has bottleneck
            new TargetMetrics(){category =  Category.Render, statName = "WaitForTargetFPS"}, // waiting for VSync
        };

        // Frame Time Accumulation
        private float _previousFrameEndSeconds = 0f;
        private float _accumFrameTimeSeconds = 0f;
        private float _accumFrameCount = 0f;
        // FPS
        private float _fps = 0f;
        private Rect _showFPSRect;
        private readonly GUIStyle _showFPSStyle = new (GUIStyle.none);
        // Recorders
        private readonly List<ProfilerRecorder> _recorders = new List<ProfilerRecorder>();
        private StreamWriter _writer;
        
        /// <summary>
        /// On start up, initialize the output stream and start the UpdateCoroutine
        /// </summary>
        private void Start()
        {
            // Initialize GUI settings
            _showFPSRect = new Rect(showFPSPosition.x, 0, Screen.width, Screen.height - showFPSPosition.y);
            _showFPSStyle.fontSize = 28;
            _showFPSStyle.alignment = TextAnchor.LowerLeft;
            _showFPSStyle.normal.textColor = Color.yellow;
            _showFPSStyle.font = null;
            
            // Initialize output stream
#if UNITY_EDITOR
            var path = outputPathEditor.ResolvePath();
#else
            var path = outputPathGameBuild.ResolvePath();
#endif
            Debug.Log($"[ProfilerLogger] Saved to {Path.GetFullPath(path)}");
            var dirName = Path.GetDirectoryName(path);
            if (dirName != null && dirName != "" && !Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

            try
            {
                _writer = new StreamWriter(path, false);
            }
            catch (IOException)
            {
                var newPath = path + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
                Debug.LogError($"[ProfilerLogger] {path} has been opened by another process, save to {newPath} instead");
                _writer = new StreamWriter(newPath, false);
            }

            StartCoroutine(UpdateCoroutine());
        }
        
        private IEnumerator UpdateCoroutine()
        {
            yield return new WaitForEndOfFrame();
            
            // Initialize the recorders at the end of frame
            _writer.Write("Time,FPS");
            foreach (var tgt in targetMetrics)
            {
                _recorders.Add(tgt.StartNewRecorder());
                _writer.Write($",\"{tgt.statName}\"");

                if (!_recorders[^1].Valid)
                {
                    Debug.LogWarning($"\"[ProfilerLogger] {tgt.statName}\" (in category {tgt.category}) might be an invalid Profiler Counter or an invalid Profiler Marker");
                }
            }
            _writer.Write("\n");
            
            // Loop for sample and record
            while (true)
            {
                _accumFrameTimeSeconds += (Time.realtimeSinceStartup - _previousFrameEndSeconds);
                _accumFrameCount += 1;
                
                if (_accumFrameTimeSeconds >= sampleIntervalSeconds)
                    WriteOneSample();
                
                _previousFrameEndSeconds = Time.realtimeSinceStartup;
                yield return new WaitForEndOfFrame(); // wait for next frame's end
            }
        }

        private void WriteOneSample()
        {
            _fps = _accumFrameCount / _accumFrameTimeSeconds;
            
            // sample and write
            _writer.Write($"{Time.realtimeSinceStartup},{_fps}");
            for (var i = 0; i < _recorders.Count; i++)
            {
                // Output the sampled data, output NaN if unavailable
                try
                {
                    _writer.Write(_recorders[i].Valid ? $",{_recorders[i].GetSample(0).Value}" : ",NaN");
                }
                catch (IndexOutOfRangeException)
                {
                    _writer.Write(",NaN");
                }
            }
            _writer.Write("\n");
            
            _accumFrameCount = _accumFrameTimeSeconds = 0f;
        }

        /// <summary>
        /// Show FPS on GUI
        /// </summary>
        private void OnGUI()
        {
            if (!showFPSOnGUI) return;
            
            GUI.Label(_showFPSRect, $"FPS: {_fps}", _showFPSStyle);
        }

        private void OnDestroy()
        {
            _writer?.Flush();
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;
            foreach (var recorder in _recorders) recorder.Dispose();
        }

        private void OnApplicationQuit()
        {
            _writer?.Flush();
            _writer?.Close();
            _writer?.Dispose();
            _writer = null;
            foreach (var recorder in _recorders) recorder.Dispose();
        }

        private void OnDisable()
        {
            _writer?.Flush();
        }
    }
}