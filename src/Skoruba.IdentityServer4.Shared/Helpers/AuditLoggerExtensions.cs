using EzyNet.Common.Extensions;
using EzyNet.Serilog.AuditLogs;
using Microsoft.AspNetCore.Mvc;

namespace Skoruba.IdentityServer4.Shared.Helpers
{
    public static class AuditLoggerExtensions
    {
        public static void Info(this IAuditLogger auditLogger,
            Controller controller,
            string action,
            string operationStatus,
            string resourceId,
            string resourceType,
            string userId = null)
        {
            if (userId == null)
                userId = controller.User?.Identity?.Name;

            string controllerName = controller.GetType().Name.Without("Controller").WithoutStartingWith("`");
            var @params = new AuditLogParams($"{controllerName}_{action}", operationStatus, resourceType, resourceId, userId);
            auditLogger.Information(@params, "{action} for: {@resourceId}: {@operationStatus}", action, resourceId, operationStatus);
        }
    }
}