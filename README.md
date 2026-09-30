# Troy Web Property Manager

An ASP.NET Core MVC app (.NET 10, EF Core 10, SQL Server) for renting out units. Applicants find available units and apply; property managers review the applications and approve, return or deny them. Sign-up, log-in and roles use ASP.NET Core Identity.

## Prerequisites

| What | Why | Notes |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Build and run | `dotnet --list-sdks` should show a `10.0.x` SDK. |
| SQL Server LocalDB | The database | Comes with Visual Studio's **ASP.NET and web development** workload, or install **SQL Server Express LocalDB** on its own. Any other SQL Server works too; see [Connection string](#connection-string). LocalDB is Windows only. |
| Visual Studio 2026 *(optional)* | IDE | With the **ASP.NET and web development** workload. |
| SQL Server Management Studio *(optional)* | Browse the database | |
| `dotnet-ef` *(optional)* | Only for adding migrations or dropping the database | `dotnet tool install --global dotnet-ef` |

NuGet packages are restored on the first build, so the first build needs internet access.

## Getting started

```sh
git clone <repo-url>
cd "Troy Web Property Manager"
dotnet run --launch-profile https
```

Then open https://localhost:7066, or http://localhost:5140 with `--launch-profile http`. In Visual Studio, open `Troy Web Property Manager.slnx` and press F5.

If the browser doesn't trust the HTTPS development certificate, run `dotnet dev-certs https --trust` once.

**No manual database setup is needed.** On startup the app:

1. Creates the database (`Troy_Web_Property_Manager_DB` on `(localdb)\mssqllocaldb`) if it doesn't exist, and applies any pending migrations.
2. Seeds the lookup tables (application statuses and unit types). This runs in every environment.
3. In **Development only**, fills an empty database with demo data: properties, units, applicants, and applications in every status.

### Demo accounts (Development)

All demo accounts use the password **`Demo#2026`** and are already email-confirmed.

| Role | Accounts |
|---|---|
| Property manager | `manager1@example.com`, `manager2@example.com` |
| Applicant | `applicant1@example.com`, `applicant2@example.com`, … |

Demo data is only added to an **empty** database (one with no properties), so it never touches real data. To start over, drop the database and run the app again:

```sh
dotnet ef database drop --project "Troy Web Property Manager.csproj"
```

## Configuration

Settings are in `appsettings.json`. Keep secrets out of it: use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in development (the project already has a `UserSecretsId`) and environment variables in production.

### Connection string

The default is LocalDB. To use a different SQL Server:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=Troy_Web_Property_Manager_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

The account needs permission to create the database, or the database must already exist.

### Business time zone

`BusinessTimeZone` (default `America/New_York`) sets what "now" and "today" mean. For example, it decides whether a lease is active today. Use an IANA time zone id; if the setting is left empty, the server's own zone is used.

### Email (SendGrid)

New accounts must confirm their email address before they can log in, so the app sends a confirmation email.

- **Without a SendGrid key the app still runs.** In Development the confirmation email is written to the log (the console) instead, so you can copy the link from there. Outside Development a missing key is an error.
- **To send real email**, set up a SendGrid account, verified sender and API key (see `Docs/Email Setup.txt`), then:

```sh
dotnet user-secrets set "SendGrid:ApiKey" "<your-sendgrid-api-key>"
dotnet user-secrets set "SendGrid:FromEmail" "<verified-sender@your-domain>"
dotnet user-secrets set "SendGrid:FromName" "Property Manager"
```

## Running the tests

```sh
dotnet test
```

The tests (xUnit, in `Tests/Troy Web Property Manager.Tests`) run the services and rules against an in-memory SQLite database built from the real EF model. They don't need SQL Server or any configuration.

## API and OpenAPI

The application list's data grid loads its rows from a JSON endpoint:

- `GET /api/applications`: filters, sorts and pages the list in the database, and returns one page of rows plus the filtered total.
- It uses the same sign-in cookie as the site. Sign in first; without the cookie it answers `401`.
- In **Development**, the OpenAPI document is at **`/openapi/v1.json`**. It's generated from the controllers and their XML doc comments.

Details are in `Docs/Bonus-ApplicationGrid.md`.

## Database migrations

The schema is code-first (`Data/Migrations`), and migrations are applied automatically on startup, so `dotnet ef database update` isn't needed. To change the model, edit the entities or `ApplicationDbContext`, then add a migration:

```sh
dotnet ef migrations add <MigrationName> --project "Troy Web Property Manager.csproj"
```

The next run applies it.

## Project layout

| Folder | What's in it |
|---|---|
| `Controllers` | MVC controllers (thin: bind, validate, call a service). `Controllers/Api` holds the JSON API. |
| `Services` | Business operations and data access (`ApplicationService`, `PropertyService`), plus email and the business clock. |
| `Rules` | Pure business rules (workflow, validation, leases), unit-tested without a database. |
| `Models`, `Data` | EF Core entities, `ApplicationDbContext`, migrations and the demo data seeder. |
| `ViewModels`, `Views`, `ViewComponents` | Razor views, partials (modals), view components and their models. |
| `wwwroot/js` | `site.js` (modal forms) and `grid.js` (data grids). No SPA framework. |
| `Docs` | Assessment, ER diagram, entity analysis, UI wireframes, and a write-up for each bonus feature (`Bonus-*.md`). |

## Security notes

- Sign-up lets you pick your own role, including Property Manager, because the assessment asks for a role picker (1.a.i). Managers can see every applicant's details, so a real deployment should invite or approve managers instead of letting anyone choose that role.
- The demo accounts share a known password, which is why they are only seeded in Development.
