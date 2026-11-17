using System.Text.Json;

namespace SRT.Core.RuntimeSettings
{
    internal enum RuntimeSettingType
    {
        Bool,
        Int,
        Decimal,
        String,
        Enum,
        StringList,
        Json
    }

    internal sealed class RuntimeSettingDefinition
    {
        public required string Key { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required RuntimeSettingAudience Audience { get; init; }
        public required RuntimeSettingType Type { get; init; }
        public bool RequiresRestart { get; init; }
        public long? IntMin { get; init; }
        public long? IntMax { get; init; }
        public decimal? DecimalMin { get; init; }
        public decimal? DecimalMax { get; init; }
        public int? MaxLength { get; init; }
        public int? MaxItems { get; init; }
        public int? MaxItemLength { get; init; }
        public IReadOnlyList<string>? AllowedValues { get; init; }
        public Func<JsonElement, IRuntimeSettings, string?>? ValidateJson { get; init; }

        public string Group
        {
            get
            {
                var split = Key.IndexOf(':');
                return split < 0 ? Key : Key[..split];
            }
        }
    }
}
