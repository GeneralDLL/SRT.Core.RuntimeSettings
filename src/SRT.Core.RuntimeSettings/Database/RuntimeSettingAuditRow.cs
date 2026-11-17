namespace SRT.Core.RuntimeSettings.Database
{
    public sealed class RuntimeSettingAuditRow
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
}
