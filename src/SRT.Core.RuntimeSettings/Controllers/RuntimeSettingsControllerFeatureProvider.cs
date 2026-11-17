using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SRT.Core.RuntimeSettings.Controllers
{
    internal sealed class RuntimeSettingsControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            Add(feature, typeof(DevOpsRuntimeSettingsController));
            Add(feature, typeof(AdminRuntimeSettingsController));
        }

        private static void Add(ControllerFeature feature, Type controller)
        {
            var info = controller.GetTypeInfo();
            if (!feature.Controllers.Contains(info))
                feature.Controllers.Add(info);
        }
    }
}
