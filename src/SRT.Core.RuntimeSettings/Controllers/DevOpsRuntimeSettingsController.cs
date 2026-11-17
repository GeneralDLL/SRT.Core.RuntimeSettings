using Microsoft.AspNetCore.Mvc;
using SRT.Core.Authentication;

namespace SRT.Core.RuntimeSettings.Controllers
{
    [ApiController]
    [Route("devops/runtime-settings")]
    internal sealed class DevOpsRuntimeSettingsController : RuntimeSettingsControllerBase
    {
        private readonly RuntimeSettingsEditor _editor;

        public DevOpsRuntimeSettingsController(RuntimeSettingsEditor editor)
        {
            _editor = editor;
        }

        [HttpPost("Search")]
        [CheckAuthMiddleWare("DevOps", requiredAccess: RuntimeSettingsAccess.DevOpsView)]
        public Task<IActionResult> Search([FromBody] RuntimeSettingSearchRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.SearchAsync(RuntimeSettingAudience.DevOps, request, cancellationToken));

        [HttpPost("Save")]
        [CheckAuthMiddleWare("DevOps", requiredAccess: RuntimeSettingsAccess.DevOpsUpdate)]
        public Task<IActionResult> Save([FromBody] RuntimeSettingSaveRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.SaveAsync(RuntimeSettingAudience.DevOps, request, CurrentActor(), cancellationToken));

        [HttpPost("Reset")]
        [CheckAuthMiddleWare("DevOps", requiredAccess: RuntimeSettingsAccess.DevOpsUpdate)]
        public Task<IActionResult> Reset([FromBody] RuntimeSettingResetRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.ResetAsync(RuntimeSettingAudience.DevOps, request, CurrentActor(), cancellationToken));

        [HttpPost("History")]
        [CheckAuthMiddleWare("DevOps", requiredAccess: RuntimeSettingsAccess.DevOpsView)]
        public Task<IActionResult> History([FromBody] RuntimeSettingHistoryRequest? request, CancellationToken cancellationToken)
            => RuntimeSettingsControllerRunner.Run(this, () => _editor.HistoryAsync(RuntimeSettingAudience.DevOps, request, cancellationToken));
    }
}
