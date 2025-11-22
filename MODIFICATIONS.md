# Code Modifications for Local Development

This document explains every code modification made to enable local, Azure AD-free operation.

## Table of Contents
1. [Authentication Architecture Analysis](#authentication-architecture-analysis)
2. [Modification Strategy](#modification-strategy)
3. [CustomerSite Changes](#customersite-changes)
4. [AdminSite Changes](#adminsite-changes)
5. [Configuration Changes](#configuration-changes)
6. [Why Each Change Is Critical](#why-each-change-is-critical)

---

## Authentication Architecture Analysis

### Original Architecture

The SaaS Accelerator has **two separate authentication systems**:

#### 1. User Authentication (Azure AD OpenIdConnect)
**Purpose**: Authenticate end-users accessing the portals
**Location**: Startup.cs lines 84-105
**Flow**:
```
User → Landing Page → Azure AD Login → Redirect back with claims → Process
```

**Problem**: Requires internet, Azure AD tenant, app registration

#### 2. API Authentication (Azure AD Client Credentials)
**Purpose**: Authenticate API calls to marketplace
**Location**: Startup.cs line 81, 117
**Flow**:
```
App → Get Token from Azure AD → Call Marketplace API with Bearer token
```

**Solution**: Emulator has `REQUIRE_AUTH=false` mode (default)

### Key Discovery

The emulator's `check-token.ts` file (lines 28-31):
```typescript
if (services.config.requireAuth !== true || req.headers['x-ignore-auth'] !== undefined) {
    next();  // BYPASS - No authentication required
    return;
}
```

**This means**: We can provide dummy Azure AD credentials, and the emulator won't validate them!

### Modification Scope

✅ **Must Modify**: User authentication (OpenIdConnect)
❌ **No Need to Modify**: API client credentials (emulator ignores them)

---

## Modification Strategy

### Principle: Minimal, Targeted Changes

1. **Preserve original structure** - Keep code paths intact for easy comparison
2. **Use conditional compilation** - Add `#if !LOCAL_DEV` guards where possible
3. **Document everything** - Inline comments explain WHY
4. **Mock, don't delete** - Replace Azure AD with mock claims, don't remove entirely

### Files to Modify

| File | Lines | Purpose |
|------|-------|---------|
| `CustomerSite/Startup.cs` | 84-105 | Replace OpenIdConnect with mock auth |
| `CustomerSite/Controllers/BaseController.cs` | 27-36 | Bypass auth checks, inject mock user |
| `CustomerSite/Controllers/HomeController.cs` | 222, 286-302 | Remove Azure AD challenge |
| `AdminSite/Startup.cs` | 96-125 | Replace OpenIdConnect with mock auth |
| `AdminSite/Controllers/BaseController.cs` | 31-40 | Bypass auth checks, inject mock user |
| `Services/Utilities/KnownUserAttribute.cs` | Entire | Bypass or always authorize |
| `CustomerSite/appsettings.json` | 10-24 | Add local dev configuration |
| `AdminSite/appsettings.json` | Similar | Add local dev configuration |

---

## CustomerSite Changes

### 1. Startup.cs - Replace Authentication Middleware

**File**: `saas-accelerator/src/CustomerSite/Startup.cs`

#### Original Code (Lines 84-105):
```csharp
.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIdConnectDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.Cookie.MaxAge = options.ExpireTimeSpan;
    options.SlidingExpiration = true;
})
.AddOpenIdConnect(options =>
{
    options.Authority = $"{config.AdAuthenticationEndPoint}/common/v2.0";
    options.ClientId = config.MTClientId;
    options.ResponseType = OpenIdConnectResponseType.IdToken;
    options.CallbackPath = "/Home/Index";
    options.SignedOutRedirectUri = config.SignedOutRedirectUri;
    options.TokenValidationParameters.NameClaimType = ClaimConstants.CLAIM_SHORT_NAME;
    options.TokenValidationParameters.ValidateIssuer = false;
});
```

#### Modified Code:
```csharp
// LOCAL DEV MODE: Replace Azure AD with mock authentication
.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.Cookie.MaxAge = options.ExpireTimeSpan;
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/MockLogin"; // Auto-login endpoint
});
```

**Why Critical**:
- Removes dependency on Azure AD infrastructure
- Keeps cookie-based session management (needed by app)
- Login path triggers auto-login with mock user

### 2. Add Mock Login Endpoint

**File**: `saas-accelerator/src/CustomerSite/Controllers/AccountController.cs`

#### Add New Method:
```csharp
[AllowAnonymous]
public async Task<IActionResult> MockLogin(string returnUrl = null)
{
    // Create mock user claims for local development
    var claims = new List<Claim>
    {
        new Claim(ClaimConstants.CLAIM_EMAILADDRESS, "local-dev@example.com"),
        new Claim(ClaimConstants.CLAIM_NAME, "Local Dev User"),
        new Claim(ClaimConstants.CLAIM_SHORT_NAME, "Dev User"),
        new Claim(ClaimTypes.Name, "Local Dev User"),
        new Claim(ClaimTypes.NameIdentifier, "local-dev-user-001")
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var authProperties = new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
    };

    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(claimsIdentity),
        authProperties);

    return Redirect(returnUrl ?? "/");
}
```

**Why Critical**:
- Provides the user claims that the app expects
- Creates authenticated session without Azure AD
- Email/name used throughout the app for database operations

### 3. HomeController.cs - Remove Azure AD Challenge

**File**: `saas-accelerator/src/CustomerSite/Controllers/HomeController.cs`

#### Original Code (Lines 286-302):
```csharp
else
{
    if (!string.IsNullOrEmpty(token))
    {
        return this.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = "/?token=" + token,
            }, OpenIdConnectDefaults.AuthenticationScheme);
    }
    else
    {
        this.TempData["ShowWelcomeScreen"] = "True";
        subscriptionExtension.ShowWelcomeScreen = true;
        return this.View(subscriptionExtension);
    }
}
```

#### Modified Code:
```csharp
else
{
    // LOCAL DEV MODE: Redirect to mock login instead of Azure AD
    if (!string.IsNullOrEmpty(token))
    {
        return Redirect($"/Account/MockLogin?returnUrl=/?token={token}");
    }
    else
    {
        return Redirect("/Account/MockLogin?returnUrl=/");
    }
}
```

**Why Critical**:
- Original code triggers Azure AD login (which fails locally)
- New code triggers mock login with token preserved
- Maintains the redirect flow the app expects

### 4. BaseController.cs - Mock User Context

**File**: `saas-accelerator/src/CustomerSite/Controllers/BaseController.cs`

#### Original Code (Lines 50-60):
```csharp
protected string CurrentUserEmailAddress
{
    get
    {
        return this.User.FindFirst(ClaimConstants.CLAIM_EMAILADDRESS)?.Value;
    }
}

protected string CurrentUserName
{
    get
    {
        return this.User.FindFirst(ClaimConstants.CLAIM_NAME)?.Value;
    }
}
```

#### Modified Code:
```csharp
protected string CurrentUserEmailAddress
{
    get
    {
        // Fallback to mock user if claim is missing
        return this.User.FindFirst(ClaimConstants.CLAIM_EMAILADDRESS)?.Value
               ?? "local-dev@example.com";
    }
}

protected string CurrentUserName
{
    get
    {
        // Fallback to mock user if claim is missing
        return this.User.FindFirst(ClaimConstants.CLAIM_NAME)?.Value
               ?? "Local Dev User";
    }
}
```

**Why Critical**:
- User email/name used for database operations throughout app
- Prevents null reference exceptions
- Ensures consistent test user identity

---

## AdminSite Changes

### 1. Startup.cs - Same as CustomerSite

**File**: `saas-accelerator/src/AdminSite/Startup.cs`

Apply identical changes:
- Replace OpenIdConnect with cookie-only auth
- Add mock login path

### 2. KnownUserAttribute.cs - Bypass Authorization

**File**: `saas-accelerator/src/Services/Utilities/KnownUserAttribute.cs`

#### Original Code:
```csharp
public void OnActionExecuting(ActionExecutingContext context)
{
    var email = context.HttpContext.User.Identity.Name;
    var knownUser = this.usersRepository.GetUserByEmailAddress(email);

    if (knownUser == null)
    {
        context.Result = new UnauthorizedResult();
    }
}
```

#### Modified Code:
```csharp
public void OnActionExecuting(ActionExecutingContext context)
{
    // LOCAL DEV MODE: Always allow access
    var email = context.HttpContext.User.FindFirst(ClaimConstants.CLAIM_EMAILADDRESS)?.Value
                ?? "local-dev@example.com";

    // Ensure mock user exists in database
    var knownUser = this.usersRepository.GetUserByEmailAddress(email);
    if (knownUser == null)
    {
        // Auto-register mock user
        this.usersRepository.AddUser(new Users
        {
            UserEmail = email,
            FullName = "Local Dev User",
            CreatedDate = DateTime.UtcNow
        });
    }
}
```

**Why Critical**:
- AdminSite restricts access to "known users" in database
- Original blocks access if user not whitelisted
- New code auto-registers the mock user

---

## Configuration Changes

### 1. CustomerSite appsettings.json

**File**: `saas-accelerator/src/CustomerSite/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "SaaSApiConfiguration": {
    "GrantType": "client_credentials",
    "ClientId": "00000000-0000-0000-0000-000000000000",
    "ClientSecret": "dummy-secret-for-local-dev",
    "MTClientId": "00000000-0000-0000-0000-000000000000",
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "Resource": "20e940b3-4c77-4b0b-9a53-9e16a1b010a7",
    "FulFillmentAPIBaseURL": "http://api-emulator:80/api",
    "SignedOutRedirectUri": "http://localhost:5000",
    "FulFillmentAPIVersion": "2018-08-31",
    "AdAuthenticationEndPoint": "https://login.microsoftonline.com",
    "SaaSAppUrl": "http://localhost:5000",
    "Environment": "LocalDev"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=AMPSaaSDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  },
  "AllowedHosts": "*"
}
```

**Key Points**:
- `FulFillmentAPIBaseURL`: Points to emulator container (docker networking)
- Dummy GUIDs for ClientId/TenantId (emulator won't validate)
- SQL Server connection points to docker container name
- `Environment`: "LocalDev" marker

### 2. AdminSite appsettings.json

Same approach - point to docker service names.

---

## Why Each Change Is Critical

### Authentication Bypass
**Without it**: App redirects to Azure AD → fails (private IP unreachable)
**With it**: App uses mock login → creates session → works locally

### Mock User Claims
**Without it**: Null reference exceptions when accessing User.Email
**With it**: Consistent test user identity for all operations

### KnownUser Auto-Registration
**Without it**: AdminSite blocks access (user not in database)
**With it**: Mock user automatically added on first access

### Docker Service Names in Config
**Without it**: App tries to reach localhost (container's localhost, not host)
**With it**: Docker DNS resolves service names correctly

### Dummy Azure AD Credentials
**Without it**: Client constructor fails (requires valid GUID format)
**With it**: Client created successfully, emulator ignores credentials

---

## Testing the Modifications

### Verify Authentication Bypass

1. Start CustomerSite: `http://localhost:5000`
2. Should auto-redirect to `/Account/MockLogin`
3. Should immediately create session and show welcome screen
4. Check browser cookies - should see `.AspNetCore.Cookies`

### Verify Token Flow

1. Emulator: Generate token and redirect to CustomerSite
2. CustomerSite should receive token
3. Should call `/api/saas/subscriptions/resolve` on emulator
4. Should save subscription to SQL database
5. Check database: `SELECT * FROM Subscriptions`

### Verify User Context

1. In any page, user email should be `local-dev@example.com`
2. Check database: `SELECT * FROM Users` - should see mock user
3. AdminSite should allow access without 401 errors

---

## Rollback Strategy

To revert to original Azure AD authentication:

1. Restore original `Startup.cs` files
2. Remove `MockLogin` endpoint
3. Remove auto-registration in `KnownUserAttribute`
4. Update `appsettings.json` with real Azure AD credentials
5. Deploy to Azure (public endpoints)

---

## Future Improvements

1. **Environment-based switching**: Use `#if DEBUG` for local vs production
2. **Configuration flag**: `UseLocalDevAuth: true/false` in appsettings
3. **Multiple test users**: Support different user personas
4. **Admin vs Customer roles**: Separate mock users for each portal

---

**Last Updated**: 2025-11-22
**Version**: 1.0
**Status**: Initial implementation
