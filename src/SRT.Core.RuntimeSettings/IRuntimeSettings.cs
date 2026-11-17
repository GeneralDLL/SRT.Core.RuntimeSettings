namespace SRT.Core.RuntimeSettings
{
    /// <summary>
    /// Effective runtime settings for this process. A valid database row wins; otherwise the value comes from every other configuration source.
    /// </summary>
    public interface IRuntimeSettings
    {
        /// <summary>Returns the effective value. Throws if <paramref name="key"/> was not registered.</summary>
        T Get<T>(string key);

        /// <summary>True when a valid stored row is currently applied for <paramref name="key"/>.</summary>
        bool IsOverridden(string key);
    }
}
