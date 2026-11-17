using System.Text.Json;

namespace SRT.Core.RuntimeSettings
{
    /// <summary>
    /// Collects setting definitions for one service. Calls accumulate on the same registry.
    /// A duplicate, empty, or reserved key throws <see cref="InvalidOperationException"/> so the service does not start.
    /// </summary>
    public sealed class RuntimeSettingsBuilder
    {
        private readonly RuntimeSettingsRegistry _registry;

        internal RuntimeSettingsBuilder(RuntimeSettingsRegistry registry)
        {
            _registry = registry;
        }

        public RuntimeSettingsBuilder Bool(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            bool requiresRestart = false)
            => Add(key, title, description, audience, RuntimeSettingType.Bool, requiresRestart);

        public RuntimeSettingsBuilder Int(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            long min,
            long max,
            bool requiresRestart = false)
        {
            if (min > max)
                throw new InvalidOperationException($"Runtime setting '{key}' has a minimum greater than its maximum.");

            return Add(key, title, description, audience, RuntimeSettingType.Int, requiresRestart, intMin: min, intMax: max);
        }

        public RuntimeSettingsBuilder Decimal(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            decimal min,
            decimal max,
            bool requiresRestart = false)
        {
            if (min > max)
                throw new InvalidOperationException($"Runtime setting '{key}' has a minimum greater than its maximum.");

            return Add(key, title, description, audience, RuntimeSettingType.Decimal, requiresRestart, decimalMin: min, decimalMax: max);
        }

        public RuntimeSettingsBuilder String(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            int maxLength,
            bool requiresRestart = false)
        {
            if (maxLength < 1)
                throw new InvalidOperationException($"Runtime setting '{key}' must allow at least one character.");

            return Add(key, title, description, audience, RuntimeSettingType.String, requiresRestart, maxLength: maxLength);
        }

        public RuntimeSettingsBuilder Enum(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            IReadOnlyList<string> allowedValues,
            bool requiresRestart = false)
        {
            ArgumentNullException.ThrowIfNull(allowedValues);
            if (allowedValues.Count == 0 || allowedValues.Any(value => string.IsNullOrEmpty(value)))
                throw new InvalidOperationException($"Runtime setting '{key}' needs at least one allowed value.");

            return Add(
                key,
                title,
                description,
                audience,
                RuntimeSettingType.Enum,
                requiresRestart,
                allowedValues: allowedValues.ToArray());
        }

        public RuntimeSettingsBuilder StringList(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            int maxItems,
            int maxItemLength,
            bool requiresRestart = false)
        {
            if (maxItems < 1 || maxItemLength < 1)
                throw new InvalidOperationException($"Runtime setting '{key}' must allow at least one list item.");

            return Add(
                key,
                title,
                description,
                audience,
                RuntimeSettingType.StringList,
                requiresRestart,
                maxItems: maxItems,
                maxItemLength: maxItemLength);
        }

        public RuntimeSettingsBuilder Json(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            int maxLength,
            Func<JsonElement, IRuntimeSettings, string?>? validate = null,
            bool requiresRestart = false)
        {
            if (maxLength < 1)
                throw new InvalidOperationException($"Runtime setting '{key}' must allow at least one character.");

            return Add(
                key,
                title,
                description,
                audience,
                RuntimeSettingType.Json,
                requiresRestart,
                maxLength: maxLength,
                validateJson: validate);
        }

        /// <summary>Cross-key check. A returned message rejects Save with that message.</summary>
        public RuntimeSettingsBuilder Rule(Func<IRuntimeSettings, string?> validate)
        {
            ArgumentNullException.ThrowIfNull(validate);
            _registry.AddRule(validate);
            return this;
        }

        private RuntimeSettingsBuilder Add(
            string key,
            string title,
            string description,
            RuntimeSettingAudience audience,
            RuntimeSettingType type,
            bool requiresRestart,
            long? intMin = null,
            long? intMax = null,
            decimal? decimalMin = null,
            decimal? decimalMax = null,
            int? maxLength = null,
            int? maxItems = null,
            int? maxItemLength = null,
            IReadOnlyList<string>? allowedValues = null,
            Func<JsonElement, IRuntimeSettings, string?>? validateJson = null)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(description);
            if (!System.Enum.IsDefined(audience))
                throw new InvalidOperationException($"Runtime setting '{key}' has an unknown audience.");

            _registry.Add(new RuntimeSettingDefinition
            {
                Key = key,
                Title = title,
                Description = description,
                Audience = audience,
                Type = type,
                RequiresRestart = requiresRestart,
                IntMin = intMin,
                IntMax = intMax,
                DecimalMin = decimalMin,
                DecimalMax = decimalMax,
                MaxLength = maxLength,
                MaxItems = maxItems,
                MaxItemLength = maxItemLength,
                AllowedValues = allowedValues,
                ValidateJson = validateJson
            });
            return this;
        }
    }
}
