using Microsoft.Extensions.Configuration;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings
{
    internal sealed class RuntimeSettingsRegistry
    {
        public const string ReloadSecondsKey = "RuntimeSettings:ReloadSeconds";
        public const int ReloadSecondsDefault = 30;

        private readonly object _gate = new();
        private readonly Dictionary<string, RuntimeSettingDefinition> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Func<IRuntimeSettings, string?>> _rules = new();

        public Func<RuntimeSettingsDbContext> ContextFactory { get; set; } = static () =>
            throw new InvalidOperationException("Runtime settings database factory is not set.");

        public IConfigurationRoot? Baseline { get; set; }
        public RuntimeSettingsConfigurationProvider? Provider { get; set; }

        public int Count
        {
            get
            {
                lock (_gate)
                    return _items.Count;
            }
        }

        public void Add(RuntimeSettingDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            RuntimeSettingsKeyRules.EnsureAllowed(definition.Key);
            lock (_gate)
            {
                if (!_items.TryAdd(definition.Key, definition))
                    throw new InvalidOperationException($"Runtime setting '{definition.Key}' is already registered.");
            }
        }

        public void AddRule(Func<IRuntimeSettings, string?> validate)
        {
            ArgumentNullException.ThrowIfNull(validate);
            lock (_gate)
                _rules.Add(validate);
        }

        public IReadOnlyList<RuntimeSettingDefinition> All()
        {
            lock (_gate)
                return _items.Values.ToArray();
        }

        public IReadOnlyList<Func<IRuntimeSettings, string?>> Rules()
        {
            lock (_gate)
                return _rules.ToArray();
        }

        public bool TryGet(string key, out RuntimeSettingDefinition definition)
        {
            lock (_gate)
                return _items.TryGetValue(key, out definition!);
        }

        public RuntimeSettingDefinition Require(string key)
        {
            if (!TryGet(key, out var definition))
                throw new InvalidOperationException($"Runtime setting '{key}' is not registered.");
            return definition;
        }

        public RuntimeSettingsDbContext CreateContext() => ContextFactory();
    }
}
