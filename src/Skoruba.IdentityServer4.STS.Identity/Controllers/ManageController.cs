using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using EzyNet.Serilog.AuditLogs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Skoruba.IdentityServer4.Shared.Helpers;
using Skoruba.IdentityServer4.STS.Identity.Helpers;
using Skoruba.IdentityServer4.STS.Identity.Helpers.Localization;
using Skoruba.IdentityServer4.STS.Identity.ViewModels.Manage;

namespace Skoruba.IdentityServer4.STS.Identity.Controllers
{    
    [Authorize]
    public class ManageController<TUser, TKey> : Controller
        where TUser : IdentityUser<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        private readonly UserManager<TUser> _userManager;
        private readonly SignInManager<TUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ManageController<TUser, TKey>> _logger;
        private readonly IGenericControllerLocalizer<ManageController<TUser, TKey>> _localizer;
        private readonly UrlEncoder _urlEncoder;
        private readonly IAuditLogger _auditLogger;

        private const string RecoveryCodesKey = nameof(RecoveryCodesKey);
        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        [TempData]
        public string StatusMessage { get; set; }

        public ManageController(UserManager<TUser> userManager, SignInManager<TUser> signInManager, IEmailSender emailSender, ILogger<ManageController<TUser, TKey>> logger, IGenericControllerLocalizer<ManageController<TUser, TKey>> localizer, UrlEncoder urlEncoder, IAuditLogger auditLogger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _localizer = localizer;
            _urlEncoder = urlEncoder;
            _auditLogger = auditLogger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var model = await BuildManageIndexViewModelAsync(user);

            return View(model);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(IndexViewModel model)
        {
            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(Index), "unsuccessful - invalid model", model.Username);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(Index), "unsuccessful - does not exist", model.Username);
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var email = user.Email;
            if (model.Email != email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    AuditLogInfo(nameof(Index), "unsuccessful - error setting email", model.Email, "User.Email");
                    throw new ApplicationException(_localizer["ErrorSettingEmail", user.Id]);
                }
            }

            var phoneNumber = user.PhoneNumber;
            if (model.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    AuditLogInfo(nameof(Index), "unsuccessful - error setting phone", model.PhoneNumber, "User.PhoneNumber");
                    throw new ApplicationException(_localizer["ErrorSettingPhone", user.Id]);
                }
            }
            
            await UpdateUserClaimsAsync(model, user);

            StatusMessage = _localizer["ProfileUpdated"];
            AuditLogInfo(nameof(Index), "successful", model.Username);

            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendVerificationEmail(IndexViewModel model)
        {
            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(SendVerificationEmail), "unsuccessful - invalid model", model.Username);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(SendVerificationEmail), "unsuccessful - does not exist", model.Username);
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code }, HttpContext.Request.Scheme);

            await _emailSender.SendEmailAsync(model.Email, _localizer["ConfirmEmailTitle"], _localizer["ConfirmEmailBody", HtmlEncoder.Default.Encode(callbackUrl)]);

            StatusMessage = _localizer["VerificationSent"];
            AuditLogInfo(nameof(SendVerificationEmail), "successful", model.Username);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToAction(nameof(SetPassword));
            }

            var model = new ChangePasswordViewModel { StatusMessage = StatusMessage };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(ChangePassword), "unsuccessful - invalid model");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(ChangePassword), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                AddErrors(changePasswordResult);
                AuditLogInfo(nameof(ChangePassword), "unsuccessful");
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation(_localizer["PasswordChangedLog", user.UserName]);

            StatusMessage = _localizer["PasswordChanged"];
            AuditLogInfo(nameof(ChangePassword), "successful");

            return RedirectToAction(nameof(ChangePassword));
        }

        [HttpGet]
        public async Task<IActionResult> SetPassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);

            if (hasPassword)
            {
                return RedirectToAction(nameof(ChangePassword));
            }

            var model = new SetPasswordViewModel { StatusMessage = StatusMessage };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(SetPassword), "unsuccessful - invalid model");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(SetPassword), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var addPasswordResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
            if (!addPasswordResult.Succeeded)
            {
                AddErrors(addPasswordResult);
                AuditLogInfo(nameof(SetPassword), "unsuccessful");
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = _localizer["PasswordSet"];
            AuditLogInfo(nameof(SetPassword), "successful");

            return RedirectToAction(nameof(SetPassword));
        }

        [HttpGet]
        public async Task<IActionResult> PersonalData()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadPersonalData()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(DownloadPersonalData), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            _logger.LogInformation(_localizer["AskForPersonalDataLog"], _userManager.GetUserId(User));

            var personalDataProps = typeof(TUser).GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            var personalData = personalDataProps.ToDictionary(p => p.Name, p => p.GetValue(user)?.ToString() ?? "null");
            Response.Headers.Add("Content-Disposition", "attachment; filename=PersonalData.json");
            AuditLogInfo(nameof(DownloadPersonalData), "successful");
            
            return new FileContentResult(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(personalData)), "text/json");
        }

        [HttpGet]
        public async Task<IActionResult> DeletePersonalData()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var deletePersonalDataViewModel = new DeletePersonalDataViewModel
            {
                RequirePassword = await _userManager.HasPasswordAsync(user)
            };

            return View(deletePersonalDataViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePersonalData(DeletePersonalDataViewModel deletePersonalDataViewModel)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(DeletePersonalData), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            deletePersonalDataViewModel.RequirePassword = await _userManager.HasPasswordAsync(user);
            if (deletePersonalDataViewModel.RequirePassword)
            {
                if (!await _userManager.CheckPasswordAsync(user, deletePersonalDataViewModel.Password))
                {
                    ModelState.AddModelError(string.Empty, _localizer["PasswordNotCorrect"]);
                    AuditLogInfo(nameof(DeletePersonalData), "unsuccessful - password not correct");
                    
                    return View(deletePersonalDataViewModel);
                }
            }

            var result = await _userManager.DeleteAsync(user);
            var userId = await _userManager.GetUserIdAsync(user);
            if (!result.Succeeded)
            {
                AuditLogInfo(nameof(DeletePersonalData), "unsuccessful - error deleting user");
                throw new InvalidOperationException(_localizer["ErrorDeletingUser", user.Id]);
            }

            await _signInManager.SignOutAsync();

            _logger.LogInformation(_localizer["DeletePersonalData"], userId);
            AuditLogInfo(nameof(DeletePersonalData), "successful");

            return Redirect("~/");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLogin(RemoveLoginViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(RemoveLogin), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var result = await _userManager.RemoveLoginAsync(user, model.LoginProvider, model.ProviderKey);
            if (!result.Succeeded)
            {
                AuditLogInfo(nameof(RemoveLogin), "unsuccessful - error removing external login");
                throw new ApplicationException(_localizer["ErrorRemovingExternalLogin", user.Id]);
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = _localizer["ExternalLoginRemoved"];
            AuditLogInfo(nameof(RemoveLogin), "successful");

            return RedirectToAction(nameof(ExternalLogins));
        }

        [HttpGet]
        public async Task<IActionResult> LinkLoginCallback()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var info = await _signInManager.GetExternalLoginInfoAsync(user.Id.ToString());
            if (info == null)
            {
                throw new ApplicationException(_localizer["ErrorLoadingExternalLogin", user.Id]);
            }

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                AddErrors(result);
                return View("LinkLoginFailure");
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            StatusMessage = _localizer["ExternalLoginAdded"];

            return RedirectToAction(nameof(ExternalLogins));
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLogins()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var model = new ExternalLoginsViewModel
            {
                CurrentLogins = await _userManager.GetLoginsAsync(user)
            };

            model.OtherLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Where(auth => model.CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();

            model.ShowRemoveButton = await _userManager.HasPasswordAsync(user) || model.CurrentLogins.Count > 1;
            model.StatusMessage = StatusMessage;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkLogin(string provider)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // Request a redirect to the external login provider to link a login for the current user
            var redirectUrl = Url.Action(nameof(LinkLoginCallback));
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, _userManager.GetUserId(User));
            AuditLogInfo(nameof(LinkLogin), "successful");

            return new ChallengeResult(provider, properties);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateRecoveryCodes()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(GenerateRecoveryCodes), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            if (!user.TwoFactorEnabled)
            {
                AuditLogInfo(nameof(GenerateRecoveryCodes), "unsuccessful - error generate code without 2fa");
                AddError(_localizer["ErrorGenerateCodesWithout2FA"]);
                return View();
            }

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

            _logger.LogInformation(_localizer["UserGenerated2FACodes", user.Id]);
            AuditLogInfo(nameof(GenerateRecoveryCodes), "successful");
            var model = new ShowRecoveryCodesViewModel { RecoveryCodes = recoveryCodes.ToArray() };

            return View(nameof(ShowRecoveryCodes), model);
        }

        [HttpGet]
        public IActionResult ShowRecoveryCodes()
        {
            var recoveryCodes = (string[])TempData[RecoveryCodesKey];
            if (recoveryCodes == null)
            {
                return RedirectToAction(nameof(TwoFactorAuthentication));
            }

            var model = new ShowRecoveryCodesViewModel { RecoveryCodes = recoveryCodes };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TwoFactorAuthentication()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var model = new TwoFactorAuthenticationViewModel
            {
                HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null,
                Is2faEnabled = user.TwoFactorEnabled,
                RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user),
                IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user)
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ForgetTwoFactorClient()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(ForgetTwoFactorClient), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            await _signInManager.ForgetTwoFactorClientAsync();
            StatusMessage = _localizer["SuccessForgetBrowser2FA"];
            AuditLogInfo(nameof(ForgetTwoFactorClient), "successful");

            return RedirectToAction(nameof(TwoFactorAuthentication));
        }

        [HttpGet]
        public async Task<IActionResult> Disable2faWarning()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            if (!user.TwoFactorEnabled)
            {
                throw new ApplicationException(_localizer["ErrorDisable2FA", user.Id]);
            }

            return View(nameof(Disable2fa));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable2fa()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(Disable2fa), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var disable2faResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!disable2faResult.Succeeded)
            {
                AuditLogInfo(nameof(Disable2fa), "unsuccessful - error disable 2fa");
                throw new ApplicationException(_localizer["ErrorDisable2FA", user.Id]);
            }

            _logger.LogInformation(_localizer["SuccessDisabled2FA", user.Id]);
            AuditLogInfo(nameof(Disable2fa), "successful");

            return RedirectToAction(nameof(TwoFactorAuthentication));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(ResetAuthenticator), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);
            _logger.LogInformation(_localizer["SuccessResetAuthenticationKey", user.Id]);
            AuditLogInfo(nameof(ResetAuthenticator), "successful");

            return RedirectToAction(nameof(EnableAuthenticator));
        }

        [HttpGet]
        public IActionResult ResetAuthenticatorWarning()
        {
            return View(nameof(ResetAuthenticator));
        }

        [HttpGet]
        public async Task<IActionResult> EnableAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            var model = new EnableAuthenticatorViewModel();
            await LoadSharedKeyAndQrCodeUriAsync(user, model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                AuditLogInfo(nameof(EnableAuthenticator), "unsuccessful - does not exist");
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            if (!ModelState.IsValid)
            {
                AuditLogInfo(nameof(EnableAuthenticator), "unsuccessful - invalid model state");
                await LoadSharedKeyAndQrCodeUriAsync(user, model);
                return View(model);
            }

            var verificationCode = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

            var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!is2faTokenValid)
            {
                ModelState.AddModelError(_localizer["ErrorCode"], _localizer["InvalidVerificationCode"]);
                await LoadSharedKeyAndQrCodeUriAsync(user, model);
                AuditLogInfo(nameof(EnableAuthenticator), "unsuccessful - invalid verification code");
                
                return View(model);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var userId = await _userManager.GetUserIdAsync(user);

            _logger.LogInformation(_localizer["SuccessUserEnabled2FA"], userId);

            StatusMessage = _localizer["AuthenticatorVerified"];
            AuditLogInfo(nameof(EnableAuthenticator), "successful");

            if (await _userManager.CountRecoveryCodesAsync(user) == 0)
            {
                var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                TempData[RecoveryCodesKey] = recoveryCodes.ToArray();

                return RedirectToAction(nameof(ShowRecoveryCodes));
            }

            return RedirectToAction(nameof(TwoFactorAuthentication));
        }

        [HttpGet]
        public async Task<IActionResult> GenerateRecoveryCodesWarning()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(_localizer["UserNotFound", _userManager.GetUserId(User)]);
            }

            if (!user.TwoFactorEnabled)
            {
                throw new ApplicationException(_localizer["Error2FANotEnabled", user.Id]);
            }

            return View(nameof(GenerateRecoveryCodes));
        }

        private async Task LoadSharedKeyAndQrCodeUriAsync(TUser user, EnableAuthenticatorViewModel model)
        {
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            model.SharedKey = FormatKey(unformattedKey);
            model.AuthenticatorUri = GenerateQrCodeUri(user.Email, unformattedKey);
        }

        private async Task<IndexViewModel> BuildManageIndexViewModelAsync(TUser user)
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var profile = OpenIdClaimHelpers.ExtractProfileInfo(claims);

            var model = new IndexViewModel
            {
                Username = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsEmailConfirmed = user.EmailConfirmed,
                StatusMessage = StatusMessage,
                Name = profile.FullName,
                Website = profile.Website,
                Profile = profile.Profile,
                Country = profile.Country,
                Region = profile.Region,
                PostalCode = profile.PostalCode,
                Locality = profile.Locality,
                StreetAddress = profile.StreetAddress
            };
            return model;
        }

        private async Task UpdateUserClaimsAsync(IndexViewModel model, TUser user)
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var oldProfile = OpenIdClaimHelpers.ExtractProfileInfo(claims);
            var newProfile = new OpenIdProfile
            {
                Website = model.Website,
                StreetAddress = model.StreetAddress,
                Locality = model.Locality,
                PostalCode = model.PostalCode,
                Region = model.Region,
                Country = model.Country,
                FullName = model.Name,
                Profile = model.Profile
            };

            var claimsToRemove = OpenIdClaimHelpers.ExtractClaimsToRemove(oldProfile, newProfile);
            var claimsToAdd = OpenIdClaimHelpers.ExtractClaimsToAdd(oldProfile, newProfile);
            var claimsToReplace = OpenIdClaimHelpers.ExtractClaimsToReplace(claims, newProfile);

            await _userManager.RemoveClaimsAsync(user, claimsToRemove);
            await _userManager.AddClaimsAsync(user, claimsToAdd);

            foreach (var pair in claimsToReplace)
            {
                await _userManager.ReplaceClaimAsync(user, pair.Item1, pair.Item2);
            }
        }

        private string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            var currentPosition = 0;

            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition, 4)).Append(" ");
                currentPosition += 4;
            }

            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                AuthenticatorUriFormat,
                _urlEncoder.Encode("Skoruba.IdentityServer4.STS.Identity"),
                _urlEncoder.Encode(email),
                unformattedKey);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private void AddError(string description, string title = "")
        {
            ModelState.AddModelError(title, description);
        }

        private void AuditLogInfo(string action, string operationStatus, string resourceId = null, string resourceType = null)
        {
            resourceType ??= "User";
            resourceId ??= User?.Identity?.Name;
            _auditLogger.Info(this, action, operationStatus, resourceId, resourceType, User?.Identity?.Name);
        }
    }
}