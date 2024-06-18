using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Dtos.Log
{
    public class AuditLogsDto
    {
        public AuditLogsDto()
        {
            Logs = new List<AuditLogDto>();
        }

        [Required]
        public DateTime? DeleteOlderThan { get; set; }

        public List<AuditLogDto> Logs { get; set; }

        public int TotalCount { get; set; }

        public int PageSize { get; set; }


        public string GetAuditLogResourceId()
        {
            if (Logs == null)
                return null;

            return string.Join(",", Logs.Select(l => l.Id));
        }
    }
}
