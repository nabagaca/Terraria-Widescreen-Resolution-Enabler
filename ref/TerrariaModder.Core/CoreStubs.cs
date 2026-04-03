using System;
using System.Collections.Generic;
using System.Reflection;

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
        void Unload();
    }

    public interface IModLifecycle
    {
        void OnContentReady(ModContext context);
        void OnWorldLoad();
        void OnWorldUnload();
    }

    public class ModContext
    {
        public ILogger Logger { get; }
        public ModConfig Config { get; }
        public string ModFolder { get; }

        public ModContext(ILogger logger, string modFolder, ModConfig config = null)
        {
            Logger = logger;
            ModFolder = modFolder;
            Config = config;
        }

        public T GetConfig<T>() where T : ModConfig
        {
            return Config as T;
        }
    }
}

namespace TerrariaModder.Core.Config
{
    public enum ConfigScope
    {
        Client = 0,
        Server = 1
    }

    public abstract class ModConfig
    {
        public abstract int Version { get; }

        public string FilePath { get; internal set; }

        public virtual void Save() { }

        public virtual void Reload() { }

        public virtual void ResetToDefaults() { }

        public virtual bool HasChangesFromBaseline() => false;

        public virtual bool HasRestartRequiredChanges() => false;

        public virtual IReadOnlyList<ConfigPropertyMeta> GetPropertyMetadata() => Array.Empty<ConfigPropertyMeta>();
    }

    public class ConfigPropertyMeta
    {
        public PropertyInfo Property { get; internal set; }
        public string Label { get; internal set; }
        public string Description { get; internal set; }
        public bool RestartRequired { get; internal set; }
        public double? Min { get; internal set; }
        public double? Max { get; internal set; }
        public string[] Options { get; internal set; }
        public string[] FormerNames { get; internal set; }
        public ConfigScope Scope { get; internal set; }

        public object GetValue(ModConfig config)
        {
            return Property?.GetValue(config);
        }

        public void SetValue(ModConfig config, object value)
        {
            Property?.SetValue(config, value);
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ClientAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ServerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class LabelAttribute : Attribute
    {
        public string Text { get; }

        public LabelAttribute(string text)
        {
            Text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DescriptionAttribute : Attribute
    {
        public string Text { get; }

        public DescriptionAttribute(string text)
        {
            Text = text;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RangeAttribute : Attribute
    {
        public double Min { get; }
        public double Max { get; }

        public RangeAttribute(double min, double max)
        {
            Min = min;
            Max = max;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class FormerlySerializedAsAttribute : Attribute
    {
        public string OldName { get; }

        public FormerlySerializedAsAttribute(string oldName)
        {
            OldName = oldName;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class RestartRequiredAttribute : Attribute { }
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
