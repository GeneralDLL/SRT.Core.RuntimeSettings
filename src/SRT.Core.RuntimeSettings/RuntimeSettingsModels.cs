namespace SRT.Core.RuntimeSettings
{
    public sealed class RuntimeSettingSearchRequest
    {
        public string? text { get; set; }
        public string? group { get; set; }
        public bool onlyOverridden { get; set; }
        public int? page { get; set; }
        public int? pageSize { get; set; }
        public string? sortBy { get; set; }
        public string? sortDir { get; set; }
    }

    public sealed class RuntimeSettingItem
    {
        public string key { get; set; } = "";
        public string group { get; set; } = "";
        public string title { get; set; } = "";
        public string description { get; set; } = "";
        public string type { get; set; } = "";
        public object? min { get; set; }
        public object? max { get; set; }
        public int? maxLength { get; set; }
        public int? maxItems { get; set; }
        public IReadOnlyList<string>? allowedValues { get; set; }
        public bool requiresRestart { get; set; }
        public string? defaultValueJson { get; set; }
        public string? valueJson { get; set; }
        public bool isOverridden { get; set; }
        public bool hasInvalidStoredValue { get; set; }
        public DateTime? date_Update { get; set; }
        public string? fullName_AdminChange { get; set; }
    }

    public sealed class RuntimeSettingSearchResult
    {
        public IReadOnlyList<RuntimeSettingItem> items { get; set; } = [];
        public int total { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }
        public IReadOnlyList<string> groups { get; set; } = [];
    }

    public sealed class RuntimeSettingSaveRequest
    {
        public string? key { get; set; }
        public string? valueJson { get; set; }
        public DateTime? expectedDate_Update { get; set; }
    }

    public sealed class RuntimeSettingResetRequest
    {
        public string? key { get; set; }
    }

    public sealed class RuntimeSettingHistoryRequest
    {
        public string? key { get; set; }
        public int? page { get; set; }
        public int? pageSize { get; set; }
    }

    public sealed class RuntimeSettingHistoryItem
    {
        public long id { get; set; }
        public string key { get; set; } = "";
        public string? oldValueJson { get; set; }
        public string? newValueJson { get; set; }
        public string action { get; set; } = "";
        public string userType { get; set; } = "";
        public int idUser { get; set; }
        public string fullNameUser { get; set; } = "";
        public DateTime date_Create { get; set; }
    }

    public sealed class RuntimeSettingHistoryResult
    {
        public IReadOnlyList<RuntimeSettingHistoryItem> items { get; set; } = [];
        public int total { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }
    }
}
