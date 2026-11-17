using Microsoft.Extensions.Configuration;

namespace SRT.Core.RuntimeSettings
{
    internal sealed class RuntimeSettingsStore : IRuntimeSettings
    {
        private readonly IConfiguration _configuration;
        private readonly RuntimeSettingsRegistry _registry;

        public RuntimeSettingsStore(IConfiguration configuration, RuntimeSettingsRegistry registry)
        {
            _configuration = configuration;
            _registry = registry;
        }

        public T Get<T>(string key)
        {
            var definition = _registry.Require(key);
            return RuntimeSettingsValues.Read<T>(_configuration, key, definition);
        }

        public bool IsOverridden(string key)
        {
            _registry.Require(key);
            return _registry.Provider?.IsOverridden(key) ?? false;
        }
    }

    internal sealed class OverlayRuntimeSettings : IRuntimeSettings
    {
        private readonly IRuntimeSettings _inner;
        private readonly RuntimeSettingDefinition _definition;
        private readonly IConfiguration _candidate;

        public OverlayRuntimeSettings(IRuntimeSettings inner, RuntimeSettingDefinition definition, string valueJson)
        {
            _inner = inner;
            _definition = definition;
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (RuntimeSettingsValues.ValidateShape(definition, valueJson, out var element) is null)
                RuntimeSettingsValues.Write(definition, element, data, paths);
            _candidate = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        }

        public T Get<T>(string key)
        {
            if (!key.Equals(_definition.Key, StringComparison.OrdinalIgnoreCase))
                return _inner.Get<T>(key);
            return RuntimeSettingsValues.Read<T>(_candidate, key, _definition);
        }

        public bool IsOverridden(string key)
            => key.Equals(_definition.Key, StringComparison.OrdinalIgnoreCase) || _inner.IsOverridden(key);
    }
}
