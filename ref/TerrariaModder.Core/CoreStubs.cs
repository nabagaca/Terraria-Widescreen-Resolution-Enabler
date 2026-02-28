using System;

namespace TerrariaModder.Core
{
    using TerrariaModder.Core.Config;
    using TerrariaModder.Core.Logging;

    public interface IMod
    {
        string Id { get; }
        string Name { get; }
        string Version { get; }
        void Initialize(ModContext context);
        void OnWorldLoad();
        void OnWorldUnload();
        void Unload();
    }

    public class ModContext
    {
        public ILogger Logger { get; set; }
        public IModConfig Config { get; set; }
        public string ModFolder { get; set; }
    }
}

namespace TerrariaModder.Core.Config
{
    public interface IModConfig
    {
        T Get<T>(string key);
        T Get<T>(string key, T defaultValue);
        void Set<T>(string key, T value);
        void Save();
    }
}

namespace TerrariaModder.Core.Logging
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public interface ILogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(string message, Exception ex);
        LogLevel MinLevel { get; set; }
        string ModId { get; }
    }
}

namespace TerrariaModder.Core.Events
{
    public static class FrameEvents
    {
        public static event Action OnPostUpdate;

        public static void RaisePostUpdate()
        {
            OnPostUpdate?.Invoke();
        }
    }
}
