using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace JiangBoYuan.ProfilerLogger
{
    /// <summary>
    /// All the ProfilerCategory
    /// </summary>
    internal enum Category
    {
        Ai,
        Animation,
        Audio,
        FileIO,
        Gui,
        Input,
        Internal,
        Lighting,
        Loading,
        Memory,
        Network,
        Particles,
        Physics,
        Physics2D,
        Render,
        Scripts,
        Video,
        VirtualTexturing,
        Vr
    }
    
    /// <summary>
    /// Specify which metric (Profiler Counter or Profiler Marker) need to be logged
    /// </summary>
    [Serializable]
    internal struct TargetMetrics
    {
        public Category category;
        public string statName;

        /// <summary>
        /// Start a new recorder that record the target metrics
        /// </summary>
        /// <exception cref="ArgumentException">The target metrics is invalid</exception>
        public ProfilerRecorder StartNewRecorder()
        {
            var maybeCategory = typeof(ProfilerCategory).GetProperty(category.ToString())?.GetValue(null);

            if (maybeCategory is not ProfilerCategory c)
                throw new ArgumentException($"ProfilerCategory.{category} is invalid");
            
            var recorder = ProfilerRecorder.StartNew(c, statName);
            if (recorder.Valid) return recorder;
                
            throw new ArgumentException(
                $"\"{statName}\" (in category {category}) is neither a valid Profiler Counter nor a valid Profiler Marker");
        }
    }

    internal enum PathPrefix
    {
        PersistentDataPath,
        [Tooltip("The folder Assets/StreamingAssets/\n\nDon't use this prefix in built game")]
        StreamingAssetsPath,
        [Tooltip("The folder Assets/\n\nDon't use this prefix in built game")]
        AssetsPath,
        NoPrefix
    }
    
    /// <summary>
    /// Represent the path with prefix and suffix
    /// </summary>
    [Serializable]
    internal struct PathStruct
    {
        public PathPrefix prefix;
        public string path;

        /// <summary>
        /// Resolve the actual path this struct represents
        /// </summary>
        /// <exception cref="ArgumentException">if prefix is undefined</exception>
        public string ResolvePath()
        {
            var prefixString = prefix switch
            {
                PathPrefix.PersistentDataPath => Application.persistentDataPath,
                PathPrefix.StreamingAssetsPath => Application.streamingAssetsPath,
                PathPrefix.AssetsPath => Application.dataPath,
                PathPrefix.NoPrefix => "",
                _ => throw new ArgumentException()
            };

            return Path.Combine(prefixString, path);
        }
    }
}
