using System.Linq;
using Marketplace.SaaS.Accelerator.Services.Models;
using Marketplace.SaaS.Accelerator.Services.Services;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Marketplace.SaaS.Accelerator.CustomerSite.Controllers;

/// <summary>
///  Sets a BaseController.
/// </summary>
/// <seealso cref="Microsoft.AspNetCore.Mvc.Controller" />
public class BaseController : Controller
{

    private readonly IAppVersionService _appVersionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseController"/> class.
    /// </summary>
    public BaseController(IAppVersionService appVersionService)
    {
        _appVersionService = appVersionService;
        // LOCAL DEV MODE: Comment out CheckAuthentication to avoid redirect loops
        // this.CheckAuthentication();
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        ViewData["AppVersion"] = _appVersionService.Version;
    }

    /// <summary>
    /// Gets Current Logged in User Email Address.
    /// </summary>
    /// <value>
    /// The current user email address.
    /// </value>
    public string CurrentUserEmailAddress
    {
        get
        {
            // LOCAL DEV MODE: Provide fallback mock user email
            return HttpContext?.User?.Claims?.FirstOrDefault(s => s.Type == ClaimConstants.CLAIM_EMAILADDRESS)?.Value
                   ?? "local-dev@example.com";
        }
    }

    /// <summary>
    /// Gets Current Logged in User Name.
    /// </summary>
    /// <value>
    /// The name of the current user.
    /// </value>
    public string CurrentUserName
    {
        get
        {
            if (this.HttpContext != null && this.HttpContext.User.Claims.Count() > 0)
            {
                var shortNameClaim = this.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimConstants.CLAIM_SHORT_NAME);
                if (shortNameClaim != null)
                {
                    return shortNameClaim.Value;
                }
                else
                {
                    var fullNameClaim = this.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimConstants.CLAIM_NAME);
                    if (fullNameClaim != null)
                    {
                        return fullNameClaim.Value;
                    }
                }
            }

            // LOCAL DEV MODE: Provide fallback mock user name
            return "Local Dev User";
        }
    }

    /// <summary>
    /// Get Current Logged in User Email Address.
    /// </summary>
    /// <returns> Current Logged User Email.</returns>
    public PartnerDetailViewModel GetCurrentUserDetail()
    {
        if (HttpContext?.User?.Identity?.IsAuthenticated == true)
        {
            PartnerDetailViewModel partnerDetail = new PartnerDetailViewModel();
            partnerDetail.FullName = this.CurrentUserName;
            partnerDetail.EmailAddress = this.CurrentUserEmailAddress;
            return partnerDetail;
        }

        return new PartnerDetailViewModel();
    }

    /// <summary>
    /// Checks the authentication.
    /// </summary>
    /// <returns>
    /// Check authentication.
    /// </returns>
    public IActionResult CheckAuthentication()
    {
        if (this.HttpContext == null || !this.HttpContext.User.Identity.IsAuthenticated)
        {
            return this.Challenge(new AuthenticationProperties { RedirectUri = "/" }, OpenIdConnectDefaults.AuthenticationScheme);
        }
        else
        {
            return this.RedirectToAction("Index", "Home", new { });
        }
    }
}