using Microsoft.AspNetCore.Mvc;
using SRT.Core.Authentication;

namespace SRT.Core.RuntimeSettings.Controllers
{
    [ApiController]
    [Route("superadmin/runtime-settings")]
    internal sealed class AdminRuntimeSettingsController : RuntimeSettingsControllerBase
    {
        private readonly RuntimeSettingsEditor _editor;

        public AdminRuntimeSettingsController(RuntimeSettingsEditor editor)
        {
            _editor = editor;
        }

        [HttpPost("Search")]
        [CheckAuthMiddleWare("Admin", requiredAccess: RuntimeSettingsAccess.AdminView)]
        public Task<IActionResult> Search([FromBody] RuntimeSettingSearchRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.SearchAsync(RuntimeSettingAudience.Admin, request, cancellationToken));

        [HttpPost("Save")]
        [CheckAuthMiddleWare("Admin", requiredAccess: RuntimeSettingsAccess.AdminUpdate)]
        public Task<IActionResult> Save([FromBody] RuntimeSettingSaveRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.SaveAsync(RuntimeSettingAudience.Admin, request, CurrentActor(), cancellationToken));

        [HttpPost("Reset")]
        [CheckAuthMiddleWare("Admin", requiredAccess: RuntimeSettingsAccess.AdminUpdate)]
        public Task<IActionResult> Reset([FromBody] RuntimeSettingResetRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.ResetAsync(RuntimeSettingAudience.Admin, request, CurrentActor(), cancellationToken));

        [HttpPost("History")]
        [CheckAuthMiddleWare("Admin", requiredAccess: RuntimeSettingsAccess.AdminView)]
        public Task<IActionResult> History([FromBody] RuntimeSettingHistoryRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.HistoryAsync(RuntimeSettingAudience.Admin, request, cancellationToken));
    }
}
