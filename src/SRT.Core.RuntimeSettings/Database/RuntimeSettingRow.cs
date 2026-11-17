namespace SRT.Core.RuntimeSettings.Database
{
    public sealed class RuntimeSettingRow
    {
        public string key { get; set; } = "";
        public string valueJson { get; set; } = "";
        public DateTime date_Update { get; set; }
        public string userType_AdminChange { get; set; } = "";
        public int id_AdminChange { get; set; }
        public string fullName_AdminChange { get; set; } = "";
    }
}
