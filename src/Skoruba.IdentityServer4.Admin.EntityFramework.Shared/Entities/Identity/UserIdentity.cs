using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity
{
	public class UserIdentity : IdentityUser
	{
        public List<Password> PasswordsHistory { get; set; }
    }

    public class Password
    {
        public string PasswordHash { get; set; }
        public DateTime? ChangePasswordDate { get; set; }
    }
}