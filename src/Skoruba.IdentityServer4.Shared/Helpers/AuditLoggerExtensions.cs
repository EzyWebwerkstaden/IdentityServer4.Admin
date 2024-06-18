using System;
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

    public static class StringExtensions
    {
        public static string Without(this string source, string toReplace)
        {
            if (string.IsNullOrEmpty(toReplace))
                return source;

            return source.Replace(toReplace, string.Empty);
        }
        
        public static string WithoutStartingWith(this string source, string startingWith)
        {
            if (string.IsNullOrEmpty(startingWith))
                return source;

            var indexOfStart = source.IndexOf(startingWith, StringComparison.Ordinal);
            if (indexOfStart == -1)
                return source;
            
            return source.Substring(0, indexOfStart);
        }
    }
}