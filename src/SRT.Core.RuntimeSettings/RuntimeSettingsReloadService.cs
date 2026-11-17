using Microsoft.Extensions.Hosting;

namespace SRT.Core.RuntimeSettings
{
    internal sealed class RuntimeSettingsReloadService : BackgroundService
    {
        private readonly RuntimeSettingsRegistry _registry;
        private readonly IRuntimeSettings _settings;

        public RuntimeSettingsReloadService(RuntimeSettingsRegistry registry, IRuntimeSettings settings)
        {
            _registry = registry;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(ResolveSeconds()), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _registry.Provider?.Load();
            }
        }

        private int ResolveSeconds()
        {
            try
            {
                var value = _settings.Get<int>(RuntimeSettingsRegistry.ReloadSecondsKey);
                return value is >= 5 and <= 600 ? value : RuntimeSettingsRegistry.ReloadSecondsDefault;
            }
            catch (InvalidOperationException)
            {
                return RuntimeSettingsRegistry.ReloadSecondsDefault;
            }
        }
    }
}
