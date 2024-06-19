using System.Threading.Tasks;
using EzyNet.Serilog.AuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Dtos.Log;
using Skoruba.IdentityServer4.Admin.BusinessLogic.Services.Interfaces;
using Skoruba.IdentityServer4.Admin.Configuration.Constants;
using Skoruba.IdentityServer4.Shared.Helpers;

namespace Skoruba.IdentityServer4.Admin.Controllers
{
    [Authorize(Policy = AuthorizationConsts.AdministrationPolicy)]
    public class LogController : BaseController
    {
        private readonly ILogService _logService;
        private readonly IAuditLogService _auditLogService;
        private readonly IAuditLogger _auditLogger;

        public LogController(ILogService logService,
            ILogger<ConfigurationController> logger,
            IAuditLogService auditLogService, 
            IAuditLogger auditLogger) : base(logger)
        {
            _logService = logService;
            _auditLogService = auditLogService;
            _auditLogger = auditLogger;
        }

        [HttpGet]
        public async Task<IActionResult> ErrorsLog(int? page, string search)
        {
            ViewBag.Search = search;
            var logs = await _logService.GetLogsAsync(search, page ?? 1);

            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> AuditLog([FromQuery]AuditLogFilterDto filters)
        {
            ViewBag.SubjectIdentifier = filters.SubjectIdentifier;
            ViewBag.SubjectName = filters.SubjectName;
            ViewBag.Event = filters.Event;
            ViewBag.Source = filters.Source;
            ViewBag.Category = filters.Category;

            var logs = await _auditLogService.GetAsync(filters);

            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLogs(LogsDto log)
        {
            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(DeleteLogs), "unsuccessful - invalid model state");
                return View(nameof(ErrorsLog), log);
            }
            
            await _logService.DeleteLogsOlderThanAsync(log.DeleteOlderThan.Value);
            AuditLogInfo(nameof(DeleteLogs), "successful");

            return RedirectToAction(nameof(ErrorsLog));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAuditLogs(AuditLogsDto log)
        {
            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(DeleteAuditLogs), "unsuccessful - invalid model state", log.GetAuditLogResourceId());
                return View(nameof(AuditLog), log);
            }

            await _auditLogService.DeleteLogsOlderThanAsync(log.DeleteOlderThan.Value);
            AuditLogInfo(nameof(DeleteAuditLogs), "successful", log.GetAuditLogResourceId());

            return RedirectToAction(nameof(AuditLog));
        }

        private void AuditLogInfo(string action, string operationStatus, string resourceId = null, string resourceType = null)
        {
            resourceType ??= "Log";
            _auditLogger.Info(this, action, operationStatus, resourceId, resourceType);
        }
    }
}