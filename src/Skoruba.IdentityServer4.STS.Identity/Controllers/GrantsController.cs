﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

// Original file: https://github.com/IdentityServer/IdentityServer4.Quickstart.UI
// Modified by Jan Škoruba

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EzyNet.Serilog.AuditLogs;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skoruba.IdentityServer4.Shared.Helpers;
using Skoruba.IdentityServer4.STS.Identity.Helpers;
using Skoruba.IdentityServer4.STS.Identity.ViewModels.Grants;

namespace Skoruba.IdentityServer4.STS.Identity.Controllers
{
    /// <summary>
    /// This sample controller allows a user to revoke grants given to clients
    /// </summary>
    [SecurityHeaders]
    [Authorize]
    public class GrantsController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clients;
        private readonly IResourceStore _resources;
        private readonly IAuditLogger _auditLogger;

        public GrantsController(IIdentityServerInteractionService interaction,
            IClientStore clients,
            IResourceStore resources, 
            IAuditLogger auditLogger)
        {
            _interaction = interaction;
            _clients = clients;
            _resources = resources;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Show list of grants
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return View("Index", await BuildViewModelAsync());
        }

        /// <summary>
        /// Handle postback to revoke a client
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(string clientId)
        {
            await _interaction.RevokeUserConsentAsync(clientId);
            AuditLogInfo(nameof(Revoke), "successful", clientId);
            return RedirectToAction("Index");
        }

        private async Task<GrantsViewModel> BuildViewModelAsync()
        {
            var grants = await _interaction.GetAllUserConsentsAsync();

            var list = new List<GrantViewModel>();
            foreach (var grant in grants)
            {
                var client = await _clients.FindClientByIdAsync(grant.ClientId);
                if (client != null)
                {
                    var resources = await _resources.FindResourcesByScopeAsync(grant.Scopes);

                    var item = new GrantViewModel()
                    {
                        ClientId = client.ClientId,
                        ClientName = client.ClientName ?? client.ClientId,
                        ClientLogoUrl = client.LogoUri,
                        ClientUrl = client.ClientUri,
                        Created = grant.CreationTime,
                        Expires = grant.Expiration,
                        IdentityGrantNames = resources.IdentityResources.Select(x => x.DisplayName ?? x.Name).ToArray(),
                        ApiGrantNames = resources.ApiResources.Select(x => x.DisplayName ?? x.Name).ToArray()
                    };

                    list.Add(item);
                }
            }

            return new GrantsViewModel
            {
                Grants = list
            };
        }

        private void AuditLogInfo(string action, string operationStatus, string resourceId = null, string resourceType = null, string userId = null)
        {
            resourceType ??= "Grant";
            _auditLogger.Info(this, action, operationStatus, resourceId, resourceType, userId);
        }
    }
}