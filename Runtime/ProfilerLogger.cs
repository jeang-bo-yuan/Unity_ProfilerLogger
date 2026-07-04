using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField, Tooltip("Specify which profiler counter's value need to be logged")]
        private List<TargetMetrics> targetMetrics = new List<TargetMetrics>
        {
            new TargetMetrics(){category = Category.Render, statName = "CPU Total Frame Time"},
            new TargetMetrics(){category = Category.Render, statName = "GPU Frame Time"},
        };

        private float _previousFrameEndSeconds = 0f;
        private float _accumFrameTimeSeconds = 0f;
        private float _accumFrameCount = 0f;
        private readonly List<ProfilerRecorder> _recorders = new List<ProfilerRecorder>();
        private StreamWriter _writer;
        
        /// <summary>
        /// On start up, initialize the recorders and the output stream
        /// </summary>
        private void Start()
        {
            // Initialize output stream
#if UNITY_EDITOR
            var path = outputPathEditor.ResolvePath();
#else
            var path = outputPathGameBuild.ResolvePath();
#endif
            Debug.Log($"[ProfilerLogger] Saved to {Path.GetFullPath(path)}");
            var dirName = Path.GetDirectoryName(path);
            if (dirName != null && !Directory.Exists(dirName)) Directory.CreateDirectory(dirName);

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
            
            // Initialize the recorders
            _writer.Write("Time, FPS");
            foreach (var tgt in targetMetrics)
            {
                try
                {
                    _recorders.Add(tgt.StartNewRecorder());
                    _writer.Write($", \"{tgt.statName}\"");
                }
                catch (ArgumentException e)
                {
                    Debug.LogException(e);
                }
            }
            _writer.Write("\n");

            StartCoroutine(UpdateCoroutine());
        }
        
        private IEnumerator UpdateCoroutine()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                
                _accumFrameTimeSeconds += (Time.realtimeSinceStartup - _previousFrameEndSeconds);
                _accumFrameCount += 1;
                
                if (_accumFrameTimeSeconds >= sampleIntervalSeconds)
                    WriteOneSample();
                
                _previousFrameEndSeconds = Time.realtimeSinceStartup;
            }
        }

        private void WriteOneSample()
        {
            var fps = _accumFrameCount / _accumFrameTimeSeconds;
            
            // sample and write
            _writer.Write($"{Time.realtimeSinceStartup}, {fps}");
            foreach (var recorder in _recorders)
            {
                try
                {
                    _writer.Write($", {recorder.GetSample(0).Value}");
                }
                catch (IndexOutOfRangeException)
                {
                    _writer.Write(", NaN");
                }
            }
            _writer.Write("\n");
            
            _accumFrameCount = _accumFrameTimeSeconds = 0f;
        }

        private void OnDestroy()
        {
            if (_writer == null) return;
            _writer.Close();
            _writer.Dispose();
            foreach (var recorder in _recorders) recorder.Dispose();
        }
    }
}