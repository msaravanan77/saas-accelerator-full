# Local Azure Marketplace SaaS Accelerator

**Complete local development environment for Azure Marketplace SaaS applications**

Run the entire Azure Marketplace SaaS flow locally without Azure Partner Center, Azure AD, or internet connectivity.

## 📚 Documentation

- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Project vision, architecture, and value proposition
- **[MODIFICATIONS.md](./MODIFICATIONS.md)** - Detailed code changes and technical explanations

## 📊 Visual Architecture Diagrams

Professional-grade SVG diagrams to understand the complete system:

- **[User Flow Diagram](./diagrams/01-user-flow-diagram.svg)** - End-to-end user journey from marketplace to subscription management
  - Shows the complete flow: Purchase → Authentication Bypass → Activation → Management
  - Highlights the problem we solve vs traditional approach
  - Actor interactions: Developer, Customer, Publisher, System Components

- **[Technical Architecture Diagram](./diagrams/02-technical-architecture-diagram.svg)** - Internal system architecture and component interactions
  - Docker container architecture and networking
  - API communication flows (Resolve, Activate, Webhooks)
  - Authentication bypass mechanism (Traditional vs Our Solution)
  - Database schema and relationships
  - Code modification highlights

**Tip:** Open these SVG files in your browser for best viewing experience. They're fully interactive and scalable.

## 🚀 Quick Start

### Prerequisites

- **Docker** and **Docker Compose** installed
- **WSL Ubuntu** (if on Windows) or **Linux/macOS**
- **8GB+ RAM** recommended
- **10GB+ free disk space**

### 1. Clone and Navigate

```bash
cd /path/to/saas-accelerator-full
```

### 2. Start All Services

```bash
docker-compose up --build
```

This will start:
- **SQL Server** (port 1433)
- **API Emulator** (port 8080)
- **CustomerSite** (port 5000)
- **AdminSite** (port 5001)

### 3. Wait for Services to be Ready

Watch the logs for:
```
saas-customersite | Application started. Press Ctrl+C to shut down.
saas-adminsite | Application started. Press Ctrl+C to shut down.
```

### 4. Initialize the Database

On **first run**, you need to create the database schema:

```bash
# Connect to SQL Server container
docker exec -it saas-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd'

# Create the database
CREATE DATABASE AMPSaaSDB;
GO
```

Then apply Entity Framework migrations:

```bash
# From CustomerSite container
docker exec -it saas-customersite bash
cd /app
dotnet ef database update
exit
```

**Alternative**: Manually apply the schema from `saas-accelerator/src/DataAccess/Migrations/`

---

## 🧪 Testing the Complete Flow

### Test 1: Simulated Marketplace Purchase

1. **Open Marketplace Emulator**
   ```
   http://172.28.10.162:8080/
   ```

2. **Select an Offer**
   - Click on "Sample SaaS Offer"
   - Choose a plan (Bronze, Silver, Gold)
   - Click "Subscribe"

3. **You'll be Redirected to Landing Page**
   ```
   http://172.28.10.162:5000/?token=<generated_token>
   ```

4. **Auto-Login**
   - You'll be automatically logged in as `local-dev@example.com`
   - No Azure AD required!

5. **Activate Subscription**
   - Review subscription details
   - Click "Activate"
   - Subscription is now active

### Test 2: Customer Portal

1. **View Subscriptions**
   ```
   http://172.28.10.162:5000/Subscriptions
   ```

2. **Manage Subscription**
   - Change plan
   - Update quantity
   - View subscription history

### Test 3: Publisher Portal

1. **Open Admin Portal**
   ```
   http://172.28.10.162:5001/
   ```

2. **Auto-Login as Admin**
   - Logged in as `admin-dev@example.com`
   - No Azure AD required!

3. **Manage All Subscriptions**
   - View all active subscriptions
   - See customer details
   - Manage offers and plans
   - View audit logs

### Test 4: API Emulator Configuration

1. **Open Emulator Config**
   ```
   http://172.28.10.162:8080/config
   ```

2. **View/Modify Settings**
   - Landing Page URL
   - Webhook URL
   - Publisher ID
   - Operation delays

---

## 🐳 Docker Management

### View Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f customersite
docker-compose logs -f api-emulator
docker-compose logs -f sqlserver
```

### Stop Services

```bash
docker-compose down
```

### Stop and Remove Volumes (Fresh Start)

```bash
docker-compose down -v
```

### Rebuild After Code Changes

```bash
docker-compose up --build
```

---

## 🔧 Configuration

### Ports

| Service | Internal Port | External Port | URL |
|---------|--------------|---------------|-----|
| API Emulator | 80 | 8080 | http://172.28.10.162:8080 |
| CustomerSite | 80 | 5000 | http://172.28.10.162:5000 |
| AdminSite | 80 | 5001 | http://172.28.10.162:5001 |
| SQL Server | 1433 | 1433 | localhost:1433 |

### Mock Users

**Customer Portal:**
- Email: `local-dev@example.com`
- Name: Local Dev User

**Admin Portal:**
- Email: `admin-dev@example.com`
- Name: Local Admin User

### Database Connection

```
Server: localhost,1433
Database: AMPSaaSDB
User: sa
Password: YourStrong@Passw0rd
```

---

## 🔍 Troubleshooting

### Issue: Services fail to start

**Solution**: Check if ports are already in use

```bash
# Check port usage
sudo lsof -i :8080
sudo lsof -i :5000
sudo lsof -i :5001
sudo lsof -i :1433

# Kill processes or change ports in docker-compose.yml
```

### Issue: Database connection fails

**Solution**: Wait for SQL Server to be ready

```bash
# Check SQL Server health
docker logs saas-sqlserver

# Manually test connection
docker exec -it saas-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -Q 'SELECT 1'
```

### Issue: CustomerSite shows "Database not initialized"

**Solution**: Apply EF migrations

```bash
# Option 1: From container
docker exec -it saas-customersite dotnet ef database update

# Option 2: Manually run SQL scripts from saas-accelerator/src/DataAccess/Migrations/
```

### Issue: Redirect to Azure AD login

**Solution**: Verify authentication bypass modifications were applied

```bash
# Check if Startup.cs was modified
grep -n "LOCAL DEV MODE" saas-accelerator/src/CustomerSite/Startup.cs
grep -n "MockLogin" saas-accelerator/src/CustomerSite/Controllers/AccountController.cs
```

### Issue: AdminSite shows "Access Denied"

**Solution**: Mock user not in KnownUsers table

```bash
# Connect to SQL
docker exec -it saas-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -d AMPSaaSDB

# Check/add user
SELECT * FROM KnownUsers;
GO

INSERT INTO KnownUsers (UserEmail, RoleId, CreatedDate)
VALUES ('admin-dev@example.com', 1, GETDATE());
GO
```

---

## 🗂️ Project Structure

```
saas-accelerator-full/
├── api-emulator/              # Azure Marketplace API Emulator
│   ├── src/                   # TypeScript source code
│   ├── docker/                # Docker configuration
│   └── docs/                  # Emulator documentation
│
├── saas-accelerator/          # SaaS Accelerator (modified)
│   └── src/
│       ├── CustomerSite/      # Landing page & customer portal
│       │   ├── Dockerfile
│       │   └── appsettings.json (LOCAL DEV CONFIG)
│       ├── AdminSite/         # Publisher portal
│       │   ├── Dockerfile
│       │   └── appsettings.json (LOCAL DEV CONFIG)
│       ├── Services/          # Business logic
│       └── DataAccess/        # Database entities & migrations
│
├── docker-compose.yml         # Orchestrates all services
├── README.md                  # This file
├── ARCHITECTURE.md            # Project vision and architecture
└── MODIFICATIONS.md           # Detailed code changes
```

---

## 🎯 What's Different from Original?

### ✅ Added
- **Mock authentication** (bypasses Azure AD)
- **Docker containerization** (CustomerSite, AdminSite)
- **docker-compose orchestration**
- **Local development configuration**
- **Auto-registration of mock users**

### ⚠️ Modified
- **Startup.cs** (both sites) - Removed OpenIdConnect, added cookie-only auth
- **AccountController.cs** - Added `MockLogin` endpoint
- **BaseController.cs** - Bypass auth checks, fallback user values
- **KnownUserAttribute.cs** - Auto-register mock users
- **appsettings.json** - Local development configuration

### ❌ Removed
- Azure AD dependency for user authentication
- Partner Center requirements
- Public endpoint requirements

---

## 📝 Testing Checklist

- [ ] API Emulator UI loads at `http://172.28.10.162:8080`
- [ ] CustomerSite loads at `http://172.28.10.162:5000`
- [ ] AdminSite loads at `http://172.28.10.162:5001`
- [ ] Database migrations applied successfully
- [ ] Can generate purchase token from emulator
- [ ] Token redirects to CustomerSite landing page
- [ ] Auto-login works (no Azure AD prompt)
- [ ] Can resolve token and view subscription details
- [ ] Can activate subscription
- [ ] Subscription appears in CustomerSite portal
- [ ] Subscription appears in AdminSite portal
- [ ] Can change subscription plan
- [ ] Webhooks work (check emulator logs)

---

## 🙏 Credits

- **Microsoft Azure Team** - Original SaaS Accelerator
- **Microsoft Marketplace Team** - API Emulator
- **This Project** - Authentication bypass and dockerization

---

## ⚠️ Important Disclaimers

1. **Not Production Ready** - This is for local development and learning only
2. **Security**: Mock authentication has no real security validation
3. **Testing**: Always test against real marketplace before production
4. **Support**: Community-driven, not officially supported by Microsoft

---

## 📧 Need Help?

1. Check the [Troubleshooting](#-troubleshooting) section
2. Review [ARCHITECTURE.md](./ARCHITECTURE.md) for design details
3. Review [MODIFICATIONS.md](./MODIFICATIONS.md) for code changes
4. Check container logs: `docker-compose logs -f`

---

**Last Updated**: 2025-11-22
**Version**: 1.0
**License**: MIT
