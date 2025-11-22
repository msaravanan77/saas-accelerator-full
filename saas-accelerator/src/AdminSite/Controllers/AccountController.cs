using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.AdminSite.Controllers;

/// <summary>
/// Account Controller.
/// </summary>
/// <seealso cref="Microsoft.AspNetCore.Mvc.Controller" />
public class AccountController : Controller
{
    /// <summary>
    /// The SignIn..
    /// </summary>
    /// <param name="returnUrl">The returnUrl<see cref="string" />.</param>
    /// <returns>
    /// The <see cref="IActionResult" />.
    /// </returns>
    public IActionResult SignIn(string returnUrl)
    {
        return this.Challenge(new AuthenticationProperties { RedirectUri = "/" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// The SignOut.
    /// </summary>
    /// <returns>
    /// The <see cref="IActionResult" />.
    /// </returns>
    public new SignOutResult SignOut()
    {
        return this.SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "Home/Index/",
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// The SignIn.
    /// </summary>
    /// <param name="returnUrl">The returnUrl<see cref="string" />.</param>
    /// <returns>
    /// The <see cref="IActionResult" />.
    /// </returns>
    public IActionResult AccessDenied(string returnUrl)
    {
        return this.View("Error");
    }

    /// <summary>
    /// The SignedOut.
    /// </summary>
    /// <returns>
    /// The <see cref="IActionResult" />.
    /// </returns>
    public IActionResult SignedOut() => this.View();

    /// <summary>
    /// LOCAL DEV MODE: Mock login that creates an authenticated session
    /// without requiring Azure AD. This method creates a cookie-based
    /// session with predefined admin user claims for local testing.
    /// </summary>
    /// <param name="returnUrl">The return URL after login.</param>
    /// <returns>Redirect to the return URL or home page.</returns>
    [AllowAnonymous]
    public async Task<IActionResult> MockLogin(string returnUrl = null)
    {
        // Create mock admin user claims that match what Azure AD would provide
        var claims = new List<Claim>
        {
            new Claim(ClaimConstants.CLAIM_EMAILADDRESS, "admin-dev@example.com"),
            new Claim(ClaimConstants.CLAIM_NAME, "Local Admin User"),
            new Claim(ClaimConstants.CLAIM_SHORT_NAME, "Admin User"),
            new Claim(ClaimTypes.Name, "Local Admin User"),
            new Claim(ClaimTypes.NameIdentifier, "local-admin-user-001"),
            new Claim(ClaimTypes.Email, "admin-dev@example.com")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        // Redirect to return URL or home page
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}