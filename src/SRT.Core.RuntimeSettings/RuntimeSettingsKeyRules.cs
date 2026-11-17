namespace SRT.Core.RuntimeSettings
{
    /// <summary>
    /// Rejects keys that would let a panel change a password, token, or connection string.
    /// A segment that ends with token or key is rejected, so GatewayToken is rejected and Tokens:DailyCap is allowed.
    /// </summary>
    internal static class RuntimeSettingsKeyRules
    {
        public const int MaxKeyLength = 160;

        private static readonly string[] ReservedFirstSegments =
        [
            "ConnectionDB",
            "ConnectionStrings",
            "Service",
            "urlData",
            "ServiceAuth",
            "ServiceTokenCache",
            "AuthSettings",
            "SeedPasswords",
            "Logging",
            "Kestrel",
            "AllowedHosts"
        ];

        private static readonly string[] ForbiddenFragments =
        [
            "secret",
            "password",
            "passwd",
            "pepper",
            "credential",
            "connectionstring",
            "privatekey",
            "apikey",
            "certificate"
        ];

        private static readonly string[] ForbiddenSuffixes = ["pass", "pwd", "token", "key"];

        public static void EnsureAllowed(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("A runtime setting key cannot be empty.");

            if (key.Length > MaxKeyLength)
                throw new InvalidOperationException("A runtime setting key cannot be longer than 160 characters.");

            var parts = key.Split(':');
            if (parts.Any(part => part.Length == 0))
                throw new InvalidOperationException($"Runtime setting key '{key}' has an empty segment.");

            var first = parts[0];
            if (ReservedFirstSegments.Any(name => name.Equals(first, StringComparison.OrdinalIgnoreCase))
                || first.StartsWith("AUTH_", StringComparison.OrdinalIgnoreCase)
                || first.StartsWith("S2S_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Runtime setting key '{key}' is reserved.");
            }

            foreach (var part in parts)
            {
                if (ForbiddenFragments.Any(fragment => part.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    || ForbiddenSuffixes.Any(suffix => part.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException($"Runtime setting key '{key}' is reserved.");
                }
            }
        }
    }
}
