# Local Azure Marketplace SaaS Accelerator - Complete Development Environment

> **📊 Visual Learner?** Check out our professional architecture diagrams:
> - [User Flow Diagram](./diagrams/01-user-flow-diagram.svg) - Complete user journey
> - [Technical Architecture Diagram](./diagrams/02-technical-architecture-diagram.svg) - System internals

## 🎯 What Are We Trying to Achieve?

This project creates a **completely local, offline-capable development environment** for testing Azure Marketplace SaaS applications without requiring:
- Azure Partner Center access
- Active Azure subscriptions
- Real marketplace offer publishing
- Public DNS/hosting infrastructure
- Internet connectivity (after initial setup)

## 🚀 Why Are We Doing This?

### The Problem

Individual developers and small teams face significant barriers when developing Azure Marketplace SaaS solutions:

1. **Partner Center Access Requirements**
   - Requires company registration
   - Verification process can take weeks
   - Not accessible to individual developers
   - Testing requires published offers

2. **Complex Azure AD Integration**
   - Service principals and app registrations
   - Multi-tenant authentication complexity
   - Token exchange and validation
   - Difficult to debug authentication flows

3. **Testing Limitations**
   - Cannot test subscription flows without real marketplace
   - Webhook callbacks require public endpoints
   - Fulfillment API testing needs published offers
   - Costly mistakes in production

4. **Development Friction**
   - Long feedback loops
   - Difficult to reproduce issues locally
   - Hard to onboard new team members
   - Testing requires cloud deployments

### The Solution

By combining:
- **Microsoft's Commercial Marketplace SaaS API Emulator** (simulates marketplace APIs)
- **Azure's SaaS Accelerator** (landing page, subscription management)
- **Modified authentication** (bypass Azure AD for local development)
- **Docker containerization** (consistent, reproducible environment)

We create a complete local environment where developers can:
- Test the full purchase → subscription → management flow
- Debug webhook callbacks locally
- Iterate rapidly without cloud deployments
- Learn marketplace integration patterns safely

## 🌟 Value Proposition

### For Individual Developers
- Learn Azure Marketplace patterns without barriers
- Build proof-of-concept solutions
- Test integration before Partner Center registration
- Portfolio/learning projects

### For Small Teams
- Faster onboarding of new developers
- Local development without cloud costs
- Consistent development environments
- Safer experimentation

### For the Community
- **First documented approach** to running SaaS Accelerator completely locally
- Reference implementation for authentication bypass
- Docker-based setup others can replicate
- Reduces barrier to entry for marketplace development

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         Your Browser                            │
│                    http://172.28.10.162:8080                    │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    API Emulator (Docker)                        │
│  - Simulates Azure Marketplace                                  │
│  - Generates purchase tokens                                    │
│  - Mocks Fulfillment API                                        │
│  - Handles webhooks                                             │
│  Port: 8080                                                     │
└────────────────────────────┬────────────────────────────────────┘
                             │
                    Token Redirect & API Calls
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                  CustomerSite (Docker)                          │
│  - Landing page for subscription activation                     │
│  - Modified: No Azure AD requirement                            │
│  - Mock user authentication                                     │
│  - Port: 5000                                                   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   AdminSite (Docker)                            │
│  - Publisher portal for managing subscriptions                  │
│  - Modified: No Azure AD requirement                            │
│  - Mock admin authentication                                    │
│  - Port: 5001                                                   │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   SQL Server (Docker)                           │
│  - Stores subscriptions, plans, offers                          │
│  - User records                                                 │
│  - Audit logs                                                   │
│  Port: 1433                                                     │
└─────────────────────────────────────────────────────────────────┘
```

## 🔄 End-to-End Flow

1. **Purchase Simulation**
   ```
   User → Browser → http://172.28.10.162:8080/
   - Browse marketplace emulator UI
   - Select a SaaS offer
   - Click "Subscribe"
   - Emulator generates token
   ```

2. **Landing Page Redirect**
   ```
   Emulator → http://172.28.10.162:5000/?token=<marketplace_token>
   - CustomerSite receives token
   - No Azure AD login required (bypassed)
   - Calls emulator's resolve API
   ```

3. **Token Resolution**
   ```
   CustomerSite → http://172.28.10.162:8080/api/saas/subscriptions/resolve
   - Emulator decodes token
   - Returns subscription details (offer, plan, purchaser info)
   - CustomerSite saves to SQL database
   ```

4. **Subscription Activation**
   ```
   User → Activates subscription → CustomerSite
   CustomerSite → http://172.28.10.162:8080/api/saas/subscriptions/{id}/activate
   - Emulator marks subscription as active
   - Webhook notification sent back to CustomerSite
   ```

5. **Management Portals**
   ```
   Customer: http://172.28.10.162:5000/Subscriptions
   - View/manage active subscriptions
   - Change plans, update quantity

   Publisher: http://172.28.10.162:5001/
   - Manage all subscriptions
   - Configure offers and plans
   - View audit logs
   ```

## 🔧 What Makes This Unique?

### Novel Contributions

1. **Authentication Bypass Pattern**
   - First documented approach to running SaaS Accelerator without Azure AD
   - Clean separation of concerns (API auth vs user auth)
   - Maintains code structure for easy upstream merging

2. **Complete Dockerization**
   - Both CustomerSite and AdminSite containerized
   - Single docker-compose command to start entire environment
   - Network configuration for container-to-container communication

3. **Local-First Design**
   - No public endpoints required
   - Works on private networks (WSL, VMs, etc.)
   - Deterministic, reproducible setup

4. **Educational Value**
   - Clear code modifications with inline comments
   - Documentation explains WHY, not just WHAT
   - Reference for understanding marketplace integration patterns

## 📊 Project Status

- **Status**: Experimental / Proof of Concept
- **Tested On**: WSL Ubuntu 22.04 on Windows 11
- **Target Audience**: Individual developers, learning projects, local testing
- **Production Ready**: ❌ Not intended for production use
- **Upstream Compatible**: ⚠️ Requires code modifications (documented)

## ⚠️ Important Disclaimers

1. **Not Production Ready**
   - Authentication bypass is for local development only
   - No real security validation
   - Simplified error handling

2. **Diverges from Upstream**
   - Modified authentication flows
   - Cannot merge upstream updates without review
   - Maintains fork of original accelerator

3. **Testing Only**
   - Emulator does not perfectly replicate real marketplace behavior
   - Some edge cases may differ
   - Always test against real marketplace before production deployment

4. **No Support Guarantee**
   - Community-driven modifications
   - Not officially supported by Microsoft
   - Use at your own risk

## 🎓 Learning Outcomes

By setting up and using this environment, you'll understand:

- Azure Marketplace SaaS Fulfillment API flows
- Token-based subscription activation
- Webhook notification patterns
- Multi-tenant SaaS architecture patterns
- ASP.NET Core authentication pipelines
- Docker multi-container orchestration
- API emulation techniques

## 📝 Next Steps

See `MODIFICATIONS.md` for detailed code changes.
See `README.md` for setup and testing instructions.

## 🙏 Credits

- **Microsoft Azure Team**: Original SaaS Accelerator
- **Microsoft Marketplace Team**: API Emulator
- **Community Contributors**: Authentication bypass patterns
- **This Project**: Integration and dockerization

---

**Last Updated**: 2025-11-22
**Maintainer**: Community-driven
**License**: MIT (inherited from upstream projects)
