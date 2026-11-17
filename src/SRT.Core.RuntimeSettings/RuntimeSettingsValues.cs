using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace SRT.Core.RuntimeSettings
{
    internal static class RuntimeSettingsValues
    {
        public static string? ValidateShape(RuntimeSettingDefinition definition, string? valueJson, out JsonElement element)
        {
            element = default;
            if (string.IsNullOrWhiteSpace(valueJson))
                return "مقدار JSON نامعتبر است.";

            try
            {
                using var document = JsonDocument.Parse(valueJson);
                element = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return "مقدار JSON نامعتبر است.";
            }

            return definition.Type switch
            {
                RuntimeSettingType.Bool => element.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? null
                    : "نوع مقدار درست نیست.",
                RuntimeSettingType.Int => ValidateInt(definition, element),
                RuntimeSettingType.Decimal => ValidateDecimal(definition, element),
                RuntimeSettingType.String => ValidateString(definition, element),
                RuntimeSettingType.Enum => ValidateEnum(definition, element),
                RuntimeSettingType.StringList => ValidateStringList(definition, element),
                RuntimeSettingType.Json => ValidateJsonLength(definition, valueJson),
                _ => "نوع مقدار درست نیست."
            };
        }

        public static string? ValidateJsonCallback(RuntimeSettingDefinition definition, JsonElement element, IRuntimeSettings settings)
        {
            if (definition.ValidateJson is null)
                return null;
            var message = definition.ValidateJson(element, settings);
            return string.IsNullOrWhiteSpace(message) ? null : message;
        }

        public static void Write(
            RuntimeSettingDefinition definition,
            JsonElement element,
            IDictionary<string, string?> data,
            ISet<string> collectionPaths)
        {
            if (definition.Type is RuntimeSettingType.StringList or RuntimeSettingType.Json
                && element.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                collectionPaths.Add(definition.Key);
                Flatten(definition.Key, element, data);
                return;
            }

            data[definition.Key] = Scalar(element);
        }

        public static T Read<T>(IConfiguration configuration, string key, RuntimeSettingDefinition definition)
        {
            if (definition.Type is RuntimeSettingType.StringList or RuntimeSettingType.Json)
            {
                var bound = configuration.GetSection(key).Get<T>();
                if (bound is null)
                    return default!;
                return bound;
            }

            if (configuration[key] is null
                && definition.Key.Equals(RuntimeSettingsRegistry.ReloadSecondsKey, StringComparison.OrdinalIgnoreCase)
                && typeof(T) == typeof(int))
            {
                return (T)(object)RuntimeSettingsRegistry.ReloadSecondsDefault;
            }

            return configuration.GetValue<T>(key)!;
        }

        public static string BaselineJson(RuntimeSettingDefinition definition, IConfiguration baseline)
        {
            if (definition.Type is RuntimeSettingType.StringList)
                return JsonSerializer.Serialize(ReadList(baseline, definition.Key));

            if (definition.Type is RuntimeSettingType.Json)
            {
                var node = NodeFrom(baseline, baseline.GetSection(definition.Key));
                return node?.ToJsonString() ?? "null";
            }

            var raw = baseline[definition.Key];
            if (raw is null
                && definition.Key.Equals(RuntimeSettingsRegistry.ReloadSecondsKey, StringComparison.OrdinalIgnoreCase))
            {
                return RuntimeSettingsRegistry.ReloadSecondsDefault.ToString(CultureInfo.InvariantCulture);
            }

            if (raw is null)
                return "null";

            return definition.Type switch
            {
                RuntimeSettingType.Bool => bool.TryParse(raw, out var flag) && flag ? "true" : "false",
                RuntimeSettingType.Int or RuntimeSettingType.Decimal => raw,
                _ => JsonSerializer.Serialize(raw)
            };
        }

        public static string WireTimestamp(DateTime value)
        {
            var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
            return local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public static bool SameTimestamp(DateTime stored, DateTime expected)
        {
            var storedUtc = stored.Kind == DateTimeKind.Local
                ? stored.ToUniversalTime()
                : DateTime.SpecifyKind(stored, DateTimeKind.Utc);
            return WireTimestamp(storedUtc) == WireTimestamp(expected);
        }

        private static string? ValidateInt(RuntimeSettingDefinition definition, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var number))
                return "نوع مقدار درست نیست.";
            if (definition.IntMin is long min && number < min)
                return "مقدار خارج از بازه است.";
            if (definition.IntMax is long max && number > max)
                return "مقدار خارج از بازه است.";
            return null;
        }

        private static string? ValidateDecimal(RuntimeSettingDefinition definition, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetDecimal(out var number))
                return "نوع مقدار درست نیست.";
            if (definition.DecimalMin is decimal min && number < min)
                return "مقدار خارج از بازه است.";
            if (definition.DecimalMax is decimal max && number > max)
                return "مقدار خارج از بازه است.";
            return null;
        }

        private static string? ValidateString(RuntimeSettingDefinition definition, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
                return "نوع مقدار درست نیست.";
            var text = element.GetString() ?? "";
            if (definition.MaxLength is int max && text.Length > max)
                return "طول مقدار بیش از حد است.";
            return null;
        }

        private static string? ValidateEnum(RuntimeSettingDefinition definition, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
                return "نوع مقدار درست نیست.";
            var text = element.GetString() ?? "";
            if (definition.AllowedValues is null || !definition.AllowedValues.Contains(text, StringComparer.Ordinal))
                return "مقدار در فهرست مجاز نیست.";
            return null;
        }

        private static string? ValidateStringList(RuntimeSettingDefinition definition, JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
                return "نوع مقدار درست نیست.";

            var count = 0;
            foreach (var item in element.EnumerateArray())
            {
                count++;
                if (item.ValueKind != JsonValueKind.String)
                    return "نوع مقدار درست نیست.";
                var text = item.GetString() ?? "";
                if (definition.MaxItemLength is int itemMax && text.Length > itemMax)
                    return "طول عضو فهرست بیش از حد است.";
            }

            if (definition.MaxItems is int max && count > max)
                return "تعداد عضوهای فهرست بیش از حد است.";
            return null;
        }

        private static string? ValidateJsonLength(RuntimeSettingDefinition definition, string valueJson)
        {
            if (definition.MaxLength is int max && valueJson.Length > max)
                return "طول مقدار بیش از حد است.";
            return null;
        }

        private static void Flatten(string prefix, JsonElement element, IDictionary<string, string?> data)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var child = prefix + ":" + property.Name;
                        if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                            Flatten(child, property.Value, data);
                        else
                            data[child] = Scalar(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var child = prefix + ":" + index.ToString(CultureInfo.InvariantCulture);
                        if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                            Flatten(child, item, data);
                        else
                            data[child] = Scalar(item);
                        index++;
                    }
                    break;
                default:
                    data[prefix] = Scalar(element);
                    break;
            }
        }

        private static string Scalar(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Null => "",
            _ => element.GetRawText()
        };

        private static string[] ReadList(IConfiguration configuration, string key)
        {
            return configuration.GetSection(key).GetChildren()
                .Select(child => (Ok: int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index), Index: index, child.Value))
                .Where(item => item.Ok)
                .OrderBy(item => item.Index)
                .Select(item => item.Value ?? "")
                .ToArray();
        }

        private static JsonNode? NodeFrom(IConfiguration root, IConfigurationSection section)
        {
            var children = section.GetChildren().ToList();
            if (children.Count == 0)
                return LeafOrEmpty(root, section);

            if (children.All(child => int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                var array = new JsonArray();
                foreach (var child in children.OrderBy(child => int.Parse(child.Key, CultureInfo.InvariantCulture)))
                    array.Add(NodeFrom(root, child));
                return array;
            }

            var obj = new JsonObject();
            foreach (var child in children)
                obj[child.Key] = NodeFrom(root, child);
            return obj;
        }

        // {} and JSON null are both a present null. [] and "" are both an empty string, so "" stays a string.
        private static JsonNode? LeafOrEmpty(IConfiguration root, IConfigurationSection section)
        {
            if (section.Value is null)
                return KeyPresent(root, section) ? new JsonObject() : null;
            return TypedLeaf(section.Value);
        }

        private static bool KeyPresent(IConfiguration root, IConfigurationSection section)
        {
            if (root is not IConfigurationRoot configurationRoot)
                return false;
            foreach (var provider in configurationRoot.Providers.Reverse())
            {
                if (provider.TryGet(section.Path, out _))
                    return true;
            }
            return false;
        }

        private static JsonNode TypedLeaf(string text)
        {
            if (text.Equals("true", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(true);
            if (text.Equals("false", StringComparison.OrdinalIgnoreCase))
                return JsonValue.Create(false);
            if (TryCanonicalNumber(text, out var number))
                return number;
            return JsonValue.Create(text);
        }

        private static bool TryCanonicalNumber(string text, out JsonNode node)
        {
            node = null!;
            if (text.Length == 0)
                return false;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return false;
            if (!double.IsFinite(number))
                return false;
            var written = JsonValue.Create(number);
            if (written.ToJsonString() != text)
                return false;
            node = written;
            return true;
        }
    }
}
