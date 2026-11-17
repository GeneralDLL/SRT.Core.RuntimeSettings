namespace SRT.Core.RuntimeSettings
{
    /// <summary>
    /// Access strings for the runtime-settings controllers.
    /// Suffixes are View and Update because those are the role actions the platform recognizes.
    /// </summary>
    public static class RuntimeSettingsAccess
    {
        public const string DevOpsView = "devops.runtimesettings.View";
        public const string DevOpsUpdate = "devops.runtimesettings.Update";
        public const string AdminView = "settings.runtime.View";
        public const string AdminUpdate = "settings.runtime.Update";
    }
}
