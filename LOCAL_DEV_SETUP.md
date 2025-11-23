# Local Development Environment - Complete Change Documentation

## Overview
This document details every code change made to enable local Docker development without Azure AD authentication or Azure Partner Center access. All changes are marked with "LOCAL DEV MODE" comments in the code for easy identification.

**Last Updated:** 2025-11-23
**Environment:** Docker Compose on WSL Ubuntu 22.04
**Purpose:** Enable complete local testing of Azure Marketplace SaaS accelerator

---

## Table of Contents
1. [Summary of Changes](#summary-of-changes)
2. [API Emulator Changes](#api-emulator-changes)
3. [CustomerSite Changes](#customersite-changes)
4. [AdminSite Changes](#adminsite-changes)
5. [Services Layer Changes](#services-layer-changes)
6. [Docker Configuration](#docker-configuration)
7. [Complete File List](#complete-file-list)
8. [Replication Checklist](#replication-checklist)

---

## Summary of Changes

### What Was Changed and Why

| Component | Change | Reason |
|-----------|--------|--------|
| **Authentication** | Removed Azure AD, added cookie-based auth | No internet/Azure AD in local dev |
| **API Communication** | Added HTTPS to API emulator | Azure SDK requires TLS for bearer tokens |
| **Credentials** | Created MockTokenCredential | Azure SDK requires non-null credentials |
| **SSL Validation** | Bypass certificate checks | Self-signed certificates in local dev |
| **Webhooks** | Disable JWT validation | API emulator doesn't send Azure AD tokens |
| **Database** | Auto-run migrations on startup | No manual database setup required |
| **Middleware** | Handle null JWT tokens | Allow mock tokens to pass through |

### Architecture

```
┌─────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│  API Emulator   │◄────────┤  CustomerSite    │◄────────┤     Browser     │
│  (HTTPS:8080)   │         │  (HTTP:5000)     │         │  (User)         │
│                 │         │                  │         │                 │
│ • Self-signed   │         │ • MockLogin      │         │ • No Azure AD   │
│ • REQUIRE_AUTH  │         │ • Cookie Auth    │         │ • Local access  │
│   = false       │         │ • SSL bypass     │         │                 │
└─────────────────┘         └──────────────────┘         └─────────────────┘
        │                            │
        │                            │
        │                   ┌────────▼─────────┐
        │                   │   SQL Server     │
        │                   │   AMPSaaSDB      │
        │                   │   (Port 1433)    │
        └───────────────────┤                  │
          Webhook Calls     │ • Auto-migrate   │
                            │ • Config DB      │
                            └──────────────────┘
```

---

## API Emulator Changes

### File 1: `api-emulator/src/index.ts`
**Purpose:** Add HTTPS support to satisfy Azure SDK bearer token requirements

**Line 1-4: Add imports**
```typescript
import * as path from 'path';
import * as dotenv from 'dotenv';
import * as fs from 'fs';              // ADD THIS
import * as https from 'https';        // ADD THIS
import express from 'express';
```

**Lines 117-140: Replace server startup**
```typescript
  // Start the server (HTTPS in LocalDev mode to satisfy Azure SDK bearer token requirements)
  const enableHttps = (process.env.ENABLE_HTTPS ?? '').toLowerCase() === 'true';
  let server;

  if (enableHttps) {
    // Use self-signed certificate for local development
    const httpsOptions = {
      key: fs.readFileSync('/etc/ssl/private/selfsigned.key'),
      cert: fs.readFileSync('/etc/ssl/certs/selfsigned.crt')
    };
    server = https.createServer(httpsOptions, app);
    server.listen(port, () => {
      console.log(`\nListening on HTTPS port ${port}`);
    });
  } else {
    server = app.listen(port, () => {
      console.log(`\nListening on HTTP port ${port}`);
    });
  }

  server.on('upgrade', (req, socket, head) => {
    servicesContainer.notifications.upgradeConnection(socket, req, head);
  })
```

---

### File 2: `api-emulator/src/extract-publisher.ts`
**Purpose:** Handle mock JWT tokens when REQUIRE_AUTH=false

**Lines 11-18: Add null token handling**
```typescript
  if (token === undefined) {
    // LOCAL DEV MODE: If REQUIRE_AUTH=false and no token, check for publisherId in query or use config default
    if (!services.config.requireAuth) {
      publisherId = (req.query.publisherId as string) || services.config.publisherId || 'DefaultPublisher';
      (req as RequestWithPublisher).publisherId = publisherId;
      next();
      return;
    }

    if (req.query.publisherId === undefined || req.query.publisherId === '') {
      res.status(401).send('Either a bearer token in the header or a PublisherId query string parameter is required.');
      return;
    }

    publisherId = req.query.publisherId as string;
  } else {
```

**Lines 29-39: Add invalid JWT token handling**
```typescript
    const decoded = services.jwt.decodeToken(token);

    // LOCAL DEV MODE: If token decode fails and REQUIRE_AUTH=false, use config publisherId
    if (decoded === null || decoded === undefined) {
      if (!services.config.requireAuth) {
        publisherId = services.config.publisherId || 'DefaultPublisher';
        (req as RequestWithPublisher).publisherId = publisherId;
        next();
        return;
      }
      res.status(401).send('Invalid bearer token.');
      return;
    }

    if (decoded.tid === undefined || decoded.appid === undefined) {
      res.status(401).send('TenantId & AppId required in token.');
      return;
    }
```

---

### File 3: `api-emulator/docker/Dockerfile`
**Purpose:** Generate self-signed SSL certificate during build

**Lines 18-27: Add certificate generation**
```dockerfile
# Update Alpine packages for security
RUN apk update && apk upgrade

# Install OpenSSL for certificate generation
RUN apk add --no-cache openssl

# Generate self-signed certificate for local HTTPS development
# This allows Azure SDK to send bearer tokens over localhost
RUN mkdir -p /etc/ssl/private /etc/ssl/certs && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /etc/ssl/private/selfsigned.key \
    -out /etc/ssl/certs/selfsigned.crt \
    -subj "/C=US/ST=State/L=City/O=LocalDev/CN=api-emulator"

# Upgrade to latest npm (compatible with Node 20+)
RUN npm install -g npm
```

---

## CustomerSite Changes

### File 4: `saas-accelerator/src/CustomerSite/Program.cs`
**Purpose:** Remove obsolete ServicePointManager code

**REMOVE these using statements:**
```csharp
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
```

**REMOVE this code from Main() method:**
```csharp
ServicePointManager.ServerCertificateValidationCallback =
    (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
```

**Final using statements should be:**
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
```

---

### File 5: `saas-accelerator/src/CustomerSite/Startup.cs`
**Purpose:** Configure local dev mode with mock authentication and SSL bypass

#### Change 1: Add Environment property (around line 80)
```csharp
var config = new SaaSApiClientConfiguration()
{
    AdAuthenticationEndPoint = this.Configuration["SaaSApiConfiguration:AdAuthenticationEndPoint"],
    ClientId = this.Configuration["SaaSApiConfiguration:ClientId"],
    ClientSecret = this.Configuration["SaaSApiConfiguration:ClientSecret"],
    MTClientId = this.Configuration["SaaSApiConfiguration:MTClientId"],
    FulFillmentAPIBaseURL = this.Configuration["SaaSApiConfiguration:FulFillmentAPIBaseURL"],
    FulFillmentAPIVersion = this.Configuration["SaaSApiConfiguration:FulFillmentAPIVersion"],
    GrantType = this.Configuration["SaaSApiConfiguration:GrantType"],
    Resource = this.Configuration["SaaSApiConfiguration:Resource"],
    SaaSAppUrl = this.Configuration["SaaSApiConfiguration:SaaSAppUrl"],
    SignedOutRedirectUri = this.Configuration["SaaSApiConfiguration:SignedOutRedirectUri"],
    TenantId = this.Configuration["SaaSApiConfiguration:TenantId"],
    Environment = this.Configuration["SaaSApiConfiguration:Environment"]  // ADD THIS LINE
};
```

#### Change 2: Add MockTokenCredential (around line 86)
```csharp
// LOCAL DEV MODE: Use mock credentials for API emulator (dummy token, no real auth)
// The API emulator runs with REQUIRE_AUTH=false and ignores bearer tokens
// In production, use real credentials for HTTPS endpoints
Azure.Core.TokenCredential creds = config.Environment == "LocalDev"
    ? new MockTokenCredential()
    : new ClientSecretCredential(config.TenantId.ToString(), config.ClientId.ToString(), config.ClientSecret);
```

#### Change 3: Add HttpClientHandler configuration (around line 107)
```csharp
var fulfillmentBaseApi = new Uri(config.FulFillmentAPIBaseURL);

// LOCAL DEV MODE: Configure Azure SDK to ignore SSL certificate errors for API emulator
var marketplaceOptions = new MarketplaceSaaSClientOptions();
if (config.Environment == "LocalDev")
{
    var handler = new System.Net.Http.HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
    marketplaceOptions.Transport = new Azure.Core.Pipeline.HttpClientTransport(handler);
}

services
    .AddSingleton<IFulfillmentApiService>(new FulfillmentApiService(
        new MarketplaceSaaSClient(fulfillmentBaseApi, creds, marketplaceOptions),
        config,
        new FulfillmentApiClientLogger()))
```

#### Change 4: Add migrations and webhook config in Configure() (around line 146)
```csharp
public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
{
    // LOCAL DEV MODE: Automatically apply Entity Framework migrations on startup
    using (var scope = app.ApplicationServices.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<SaasKitContext>();
        try
        {
            loggerFactory.CreateLogger<Startup>().LogInformation("Applying database migrations...");
            context.Database.Migrate();
            loggerFactory.CreateLogger<Startup>().LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger<Startup>().LogError(ex, "An error occurred while applying database migrations.");
            throw;
        }

        // LOCAL DEV MODE: Disable webhook JWT validation for local development
        // The API emulator doesn't send valid Azure AD tokens when calling webhooks
        var environment = this.Configuration["SaaSApiConfiguration:Environment"];
        if (environment == "LocalDev")
        {
            var appConfigRepo = scope.ServiceProvider.GetRequiredService<IApplicationConfigRepository>();
            try
            {
                appConfigRepo.SaveValueByName("ValidateWebhookJwtToken", "false");
                loggerFactory.CreateLogger<Startup>().LogInformation("Disabled webhook JWT validation for LocalDev environment.");
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger<Startup>().LogWarning(ex, "Failed to disable webhook JWT validation.");
            }
        }
    }

    if (env.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    // ... rest of Configure method unchanged ...
}
```

---

### File 6: `saas-accelerator/src/CustomerSite/appsettings.json`
**Purpose:** Point to HTTPS API emulator and set LocalDev environment

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
    "AdAuthenticationEndPoint": "https://login.microsoftonline.com",
    "ClientId": "00000000-0000-0000-0000-000000000000",
    "ClientSecret": "mock-client-secret",
    "MTClientId": "00000000-0000-0000-0000-000000000000",
    "FulFillmentAPIBaseURL": "https://api-emulator:80/api",
    "FulFillmentAPIVersion": "2018-08-31",
    "GrantType": "client_credentials",
    "Resource": "62d94f6c-d599-489b-a797-3e10e42fbe22",
    "SaaSAppUrl": "https://localhost",
    "SignedOutRedirectUri": "https://localhost/Home/Index",
    "TenantId": "00000000-0000-0000-0000-000000000000",
    "Environment": "LocalDev"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=AMPSaaSDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  },
  "AllowedHosts": "*"
}
```

**Key changes:**
- `"FulFillmentAPIBaseURL": "https://api-emulator:80/api"` - Changed from http:// to https://
- `"Environment": "LocalDev"` - Added this line

---

### File 7: `saas-accelerator/src/CustomerSite/Controllers/AccountController.cs`
**Purpose:** Add mock login endpoint for local dev

**Add this method to the AccountController class:**

```csharp
/// <summary>
/// LOCAL DEV MODE: Mock login endpoint that creates authentication cookie without Azure AD
/// </summary>
[AllowAnonymous]
public async Task<IActionResult> MockLogin(string returnUrl = null)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimConstants.CLAIM_EMAILADDRESS, "local-dev@example.com"),
        new Claim(ClaimConstants.CLAIM_NAME, "Local Dev User"),
        new Claim(ClaimConstants.CLAIM_OBJECTIDENTIFIER, Guid.NewGuid().ToString()),
        new Claim(ClaimConstants.CLAIM_TENANTID, Guid.NewGuid().ToString())
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var authProperties = new AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
    };

    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(claimsIdentity),
        authProperties);

    return Redirect(returnUrl ?? "/");
}
```

---

## AdminSite Changes

### File 8: `saas-accelerator/src/AdminSite/Program.cs`
**Purpose:** Remove obsolete ServicePointManager code

**Apply the same changes as CustomerSite/Program.cs (see File 4 above)**

---

### File 9: `saas-accelerator/src/AdminSite/Startup.cs`
**Purpose:** Configure local dev mode (same as CustomerSite)

**Apply ALL the same changes as CustomerSite/Startup.cs (see File 5 above):**
1. Add Environment property to config
2. Add MockTokenCredential logic
3. Add HttpClientHandler configuration
4. Add migrations and webhook config in Configure()

---

### File 10: `saas-accelerator/src/AdminSite/appsettings.json`
**Purpose:** Point to HTTPS API emulator

**Apply the same changes as CustomerSite/appsettings.json (see File 6 above):**
- Change `FulFillmentAPIBaseURL` to `https://api-emulator:80/api`
- Add `Environment": "LocalDev"`

---

### File 11: `saas-accelerator/src/AdminSite/Controllers/AccountController.cs`
**Purpose:** Redirect to MockLogin instead of Azure AD

**Modify the SignIn method:**
```csharp
public IActionResult SignIn(string returnUrl = null)
{
    // LOCAL DEV MODE: Redirect to mock login instead of Azure AD
    return RedirectToAction("MockLogin", new { returnUrl });
}
```

**Modify the SignOut method:**
```csharp
public async Task<IActionResult> SignOut()
{
    // LOCAL DEV MODE: Only sign out of cookie authentication, not OpenIdConnect
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return RedirectToAction(nameof(HomeController.Index), "Home");
}
```

**Add the MockLogin method (same as CustomerSite - see File 7 above)**

---

## Services Layer Changes

### File 12: `saas-accelerator/src/Services/Utilities/MockTokenCredential.cs`
**Purpose:** NEW FILE - Provide dummy Azure credentials for local dev

**Create this new file with the following content:**

```csharp
using Azure.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.Services.Utilities
{
    /// <summary>
    /// LOCAL DEV MODE: Mock token credential for local development without Azure AD
    /// Returns a dummy token that satisfies MarketplaceSaaSClient constructor
    /// The API emulator ignores this token when REQUIRE_AUTH=false
    /// </summary>
    public class MockTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken("mock-token-for-local-dev", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(new AccessToken("mock-token-for-local-dev", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}
```

---

## Docker Configuration

### File 13: `docker-compose.yml`
**Purpose:** Enable HTTPS on API emulator

**Modify the api-emulator service:**

```yaml
  api-emulator:
    build:
      context: ./api-emulator
      dockerfile: docker/Dockerfile
    container_name: saas-api-emulator
    environment:
      - PORT=80
      - LANDING_PAGE_URL=http://172.28.10.162:5000/
      - WEBHOOK_URL=http://customersite:80/api/AzureWebhook
      - REQUIRE_AUTH=false
      - PUBLISHER_ID=LocalDevPublisher
      - ENABLE_HTTPS=true  # ADD THIS LINE
    ports:
      - "8080:80"
    networks:
      - saas-network
    healthcheck:
      test: wget --no-verbose --tries=1 --spider --no-check-certificate https://localhost:80/ || exit 1  # CHANGED to https://
      interval: 10s
      timeout: 3s
      retries: 5
      start_period: 5s
```

---

## Complete File List

### Modified Files (12):
1. ✅ `api-emulator/src/index.ts`
2. ✅ `api-emulator/src/extract-publisher.ts`
3. ✅ `api-emulator/docker/Dockerfile`
4. ✅ `saas-accelerator/src/CustomerSite/Program.cs`
5. ✅ `saas-accelerator/src/CustomerSite/Startup.cs`
6. ✅ `saas-accelerator/src/CustomerSite/appsettings.json`
7. ✅ `saas-accelerator/src/CustomerSite/Controllers/AccountController.cs`
8. ✅ `saas-accelerator/src/AdminSite/Program.cs`
9. ✅ `saas-accelerator/src/AdminSite/Startup.cs`
10. ✅ `saas-accelerator/src/AdminSite/appsettings.json`
11. ✅ `saas-accelerator/src/AdminSite/Controllers/AccountController.cs`
12. ✅ `docker-compose.yml`

### New Files (1):
13. ✅ `saas-accelerator/src/Services/Utilities/MockTokenCredential.cs` **(NEW)**

**Total: 13 files**

---

## Replication Checklist

Use this checklist to apply these changes to your production codebase:

### Phase 1: Services Layer
- [ ] Create `saas-accelerator/src/Services/Utilities/MockTokenCredential.cs`

### Phase 2: CustomerSite
- [ ] Update `CustomerSite/Program.cs` (remove ServicePointManager code)
- [ ] Update `CustomerSite/Startup.cs` (add all 4 changes)
- [ ] Update `CustomerSite/appsettings.json` (add Environment, change URL)
- [ ] Update `CustomerSite/Controllers/AccountController.cs` (add MockLogin)

### Phase 3: AdminSite
- [ ] Update `AdminSite/Program.cs` (remove ServicePointManager code)
- [ ] Update `AdminSite/Startup.cs` (add all 4 changes)
- [ ] Update `AdminSite/appsettings.json` (add Environment, change URL)
- [ ] Update `AdminSite/Controllers/AccountController.cs` (modify SignIn/SignOut, add MockLogin)

### Phase 4: API Emulator
- [ ] Update `api-emulator/src/index.ts` (add HTTPS support)
- [ ] Update `api-emulator/src/extract-publisher.ts` (handle null tokens)
- [ ] Update `api-emulator/docker/Dockerfile` (add certificate generation)

### Phase 5: Docker
- [ ] Update `docker-compose.yml` (add ENABLE_HTTPS, change healthcheck)

### Phase 6: Testing
- [ ] Rebuild all Docker images: `docker compose build --no-cache`
- [ ] Start containers: `docker compose up -d`
- [ ] Test: Subscribe → Landing Page → Activate → Manage → Unsubscribe
- [ ] Verify webhooks work (check logs for 200 OK, not 401)
- [ ] Verify database has subscriptions table populated

---

## Key Environment Variables

| Variable | Value | Purpose |
|----------|-------|---------|
| `Environment` | `LocalDev` | Enable local dev mode in .NET apps |
| `ENABLE_HTTPS` | `true` | Enable HTTPS in API emulator |
| `REQUIRE_AUTH` | `false` | Disable auth in API emulator |
| `PUBLISHER_ID` | `LocalDevPublisher` | Default publisher for local dev |

---

## Architecture Decisions

### Why HTTPS for API Emulator?
The Azure SDK has a hardcoded security check that prevents bearer tokens from being sent over HTTP. We needed to:
1. Add HTTPS support to the API emulator
2. Use self-signed certificates (fine for local dev)
3. Configure HttpClientHandler to bypass certificate validation

### Why MockTokenCredential?
The `MarketplaceSaaSClient` constructor requires a non-null `TokenCredential`. We can't pass `null`, so we created a mock that returns a dummy token. The API emulator ignores this token when `REQUIRE_AUTH=false`.

### Why Disable Webhook JWT Validation?
The webhook endpoint validates Azure AD JWT tokens by default. The API emulator doesn't send valid Azure AD tokens in local dev mode. We automatically set `ValidateWebhookJwtToken=false` in the database during startup when `Environment=LocalDev`.

### Why Auto-Run Migrations?
To eliminate manual database setup. On first startup, the application creates all tables automatically.

---

## Troubleshooting

### Issue: Tables not created in database
**Solution:** Check CustomerSite logs for migration errors. Ensure connection string is correct.

### Issue: 401 Unauthorized on webhooks
**Solution:** Verify `ValidateWebhookJwtToken` is `false` in ApplicationConfiguration table.

### Issue: SSL connection errors
**Solution:** Ensure API emulator has `ENABLE_HTTPS=true` and CustomerSite/AdminSite have HttpClientHandler configuration.

### Issue: Can't resolve subscription token
**Solution:** Check API emulator logs for authentication errors. Verify extract-publisher.ts has null token handling.

---

## Next Steps

After applying these changes:

1. **Test End-to-End Flow:**
   - Access API emulator at `https://localhost:8080`
   - Create subscription with offer
   - Get redirected to landing page with token
   - Activate subscription
   - Verify in database
   - Test unsubscribe webhook

2. **Verify Logs:**
   - CustomerSite: Should show "Disabled webhook JWT validation"
   - API emulator: Should show "Listening on HTTPS port 80"
   - No 401 errors in webhook calls

3. **Production Considerations:**
   - Remove or comment out all "LOCAL DEV MODE" code before production
   - Set `Environment` to something other than `LocalDev`
   - Use real Azure AD credentials
   - Enable webhook JWT validation
   - Use proper SSL certificates

---

## Support

For issues or questions about these changes:
- Review the code comments marked "LOCAL DEV MODE"
- Check Docker container logs: `docker compose logs -f`
- Verify database state: `docker exec saas-sqlserver /opt/mssql-tools18/bin/sqlcmd ...`

---

**Document Version:** 1.0
**Git Branch:** `claude/accelerator-full-merge-01Gj6WyGmb6ihjECVgoeEGRr`
**Commit:** Latest on branch
