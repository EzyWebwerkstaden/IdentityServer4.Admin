using System.Collections.Generic;
using System.Linq;

namespace Skoruba.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Grant
{
	public class PersistedGrantsDto
	{
		public PersistedGrantsDto()
		{
			PersistedGrants = new List<PersistedGrantDto>();
		}

	    public string SubjectId { get; set; }

		public int TotalCount { get; set; }

		public int PageSize { get; set; }

		public List<PersistedGrantDto> PersistedGrants { get; set; }

		public string GetAuditLogResourceId()
		{
			if (PersistedGrants == null)
				return null;

			return string.Join(",", PersistedGrants.Select(g => g.Key));
		}
	}
}