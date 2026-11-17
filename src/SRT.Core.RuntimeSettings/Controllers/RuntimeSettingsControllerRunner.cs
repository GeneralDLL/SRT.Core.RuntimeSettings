using Microsoft.AspNetCore.Mvc;
using SRT.Core.SYS_Controllers;

namespace SRT.Core.RuntimeSettings.Controllers
{
    internal abstract class RuntimeSettingsControllerBase : SRTBaseControllerSystem
    {
        protected RuntimeSettingActor CurrentActor()
        {
            var user = dmUser;
            return new RuntimeSettingActor(Cut(user?.userType, 20), user?.id ?? 0, Cut(user?.GetFullName(), 200));
        }

        private static string Cut(string? value, int max)
        {
            value ??= "";
            return value.Length <= max ? value : value[..max];
        }
    }

    internal static class RuntimeSettingsControllerRunner
    {
        public static async Task<IActionResult> Run(SRTBaseControllerSystem controller, Func<Task<EditorOutcome>> action)
        {
            try
            {
                var outcome = await action().ConfigureAwait(false);
                return outcome.Status switch
                {
                    200 => controller.Response_OK(data: outcome.Body),
                    400 => controller.Response_Error_BadRequest(outcome.Message),
                    404 => controller.Response_Error_NotFoundInputError(outcome.Message),
                    409 => controller.Response_Error_ExistedDataBefore(outcome.Message),
                    _ => controller.Response_Error_BadRequest(outcome.Message)
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await controller.Response_Error_ControlInternalServerError("خواندن تنظیم انجام نشد.", exception).ConfigureAwait(false);
            }
        }
    }
}
