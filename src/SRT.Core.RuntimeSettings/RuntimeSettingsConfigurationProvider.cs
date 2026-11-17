using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings
{
    internal sealed class RuntimeSettingsConfigurationSource : IConfigurationSource
    {
        private readonly RuntimeSettingsRegistry _registry;
        private readonly ILogger _logger;

        public RuntimeSettingsConfigurationSource(RuntimeSettingsRegistry registry, ILogger logger)
        {
            _registry = registry;
            _logger = logger;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var provider = new RuntimeSettingsConfigurationProvider(_registry, _logger);
            _registry.Provider = provider;
            return provider;
        }
    }

    internal sealed class StoredSettingState
    {
        public required string ValueJson { get; init; }
        public required bool Applied { get; init; }
        public required bool Invalid { get; init; }
        public required DateTime UpdatedUtc { get; init; }
        public required string FullName { get; init; }
    }

    internal sealed class RuntimeSettingsConfigurationProvider : ConfigurationProvider
    {
        private readonly RuntimeSettingsRegistry _registry;
        private readonly ILogger _logger;
        private readonly object _gate = new();
        private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, StoredSettingState> _states = new(StringComparer.OrdinalIgnoreCase);

        public RuntimeSettingsConfigurationProvider(RuntimeSettingsRegistry registry, ILogger logger)
        {
            _registry = registry;
            _logger = logger;
        }

        public int OverriddenCount
        {
            get
            {
                lock (_gate)
                    return _states.Values.Count(state => state.Applied);
            }
        }

        public bool IsOverridden(string key)
        {
            lock (_gate)
                return _states.TryGetValue(key, out var state) && state.Applied;
        }

        public bool TryGetState(string key, out StoredSettingState state)
        {
            lock (_gate)
                return _states.TryGetValue(key, out state!);
        }

        public override void Load()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                LoadAsync(timeout.Token).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Runtime settings were not read within 3 seconds. Configuration defaults stay in effect.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Runtime settings could not be read ({ExceptionType}). Configuration defaults stay in effect.",
                    ex.GetType().Name);
            }
        }

        public override IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
        {
            lock (_gate)
            {
                var earlier = Suppress(parentPath) ? Array.Empty<string>() : earlierKeys;
                return base.GetChildKeys(earlier, parentPath).ToArray();
            }
        }

        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            List<RuntimeSettingRow> rows;
            await using (var db = _registry.CreateContext())
            {
                rows = await db.RuntimeSettings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Apply(rows);
        }

        private void Apply(List<RuntimeSettingRow> rows)
        {
            var next = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var states = new Dictionary<string, StoredSettingState>(StringComparer.OrdinalIgnoreCase);
            var definitions = _registry.All();
            var byKey = definitions.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (!byKey.TryGetValue(row.key, out var definition))
                    continue;

                var invalid = RuntimeSettingsValues.ValidateShape(definition, row.valueJson, out var element) is not null;
                states[definition.Key] = new StoredSettingState
                {
                    ValueJson = row.valueJson,
                    Applied = !invalid,
                    Invalid = invalid,
                    UpdatedUtc = row.date_Update,
                    FullName = row.fullName_AdminChange
                };

                if (invalid)
                {
                    WarnOnce(definition.Key);
                    continue;
                }

                ClearWarning(definition.Key);
                RuntimeSettingsValues.Write(definition, element, next, paths);
            }

            var changed = false;
            lock (_gate)
            {
                changed = !SameData(Data, next) || !_paths.SetEquals(paths);
                Data = next;
                _paths = paths;
                _states = states;
            }

            if (changed)
                OnReload();
        }

        private bool Suppress(string? parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
                return false;

            foreach (var path in _paths)
            {
                if (parentPath.Equals(path, StringComparison.OrdinalIgnoreCase)
                    || parentPath.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void WarnOnce(string key)
        {
            lock (_gate)
            {
                if (!_warned.Add(key))
                    return;
            }

            _logger.LogWarning("Stored runtime setting '{Key}' does not match its definition and was ignored.", key);
        }

        private void ClearWarning(string key)
        {
            lock (_gate)
                _warned.Remove(key);
        }

        private static bool SameData(IDictionary<string, string?> current, IDictionary<string, string?> next)
        {
            if (current.Count != next.Count)
                return false;

            foreach (var pair in next)
            {
                if (!current.TryGetValue(pair.Key, out var value) || !string.Equals(value, pair.Value, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }
}
