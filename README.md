# Troy Web Property Manager

An ASP.NET Core MVC app (.NET 10, EF Core 10, SQL Server) for renting out units. Applicants find available units and apply; property managers review the applications and approve, return or deny them. Sign-up, log-in and roles use ASP.NET Core Identity.

## Prerequisites

| What | Why | Notes |
|---|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Build and run | `dotnet --list-sdks` should show a `10.0.x` SDK. |
| SQL Server LocalDB | The database | Comes with Visual Studio's **ASP.NET and web development** workload, or install **SQL Server Express LocalDB** on its own. Any other SQL Server works too; see [Connection string](#connection-string). LocalDB is Windows only. |
| Visual Studio 2026 *(optional)* | IDE | With the **ASP.NET and web development** workload. |
| SQL Server Management Studio *(optional)* | Browse the database | |
| Google Chrome *(optional)* | Only for the UI tests | Any recent version; the matching ChromeDriver is downloaded automatically. |
| `dotnet-ef` *(optional)* | Only for adding migrations, generating the Production migration script, or dropping the database | `dotnet tool install --global dotnet-ef` |

NuGet packages are restored on the first build, so the first build needs internet access.

## Getting started

```sh
git clone <repo-url>
cd "Troy Web Property Manager"
dotnet run --launch-profile https
```

Then open https://localhost:7066, or http://localhost:5140 with `--launch-profile http`. In Visual Studio, open `Troy Web Property Manager.slnx` and press F5.

If the browser doesn't trust the HTTPS development certificate, run `dotnet dev-certs https --trust` once.

**No manual database setup is needed in Development.** On startup the app:

1. In **Development only**, creates the database (`Troy_Web_Property_Manager_DB` on `(localdb)\mssqllocaldb`) if it doesn't exist, and applies pending migrations. Other environments require migrations to be applied before startup.
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

Shared settings in `appsettings.json` use safe defaults. `appsettings.Development.json` enables LocalDB, detailed diagnostics and on-page confirmation links; `appsettings.Production.json` keeps those shortcuts off. Telemetry (the startup ping and the GoatCounter page counter) is configured only in `appsettings.Production.json`, so local runs never ping the production health check or count as production page views. To try it in Development, set `Telemetry:StartupPingUrl` or `Telemetry:GoatCounterUrl` in user secrets (preferably to a separate dev check or site); Development then turns on GoatCounter's localhost support. Identity pages never get the page counter. Set a telemetry URL to an empty string to disable that integration. Keep secrets out of these files: use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in Development (the project already has a `UserSecretsId`) and environment variables in Production.

The `http` and `https` launch profiles select Development. The `Production` profile selects Production; without a launch profile or an environment setting, ASP.NET Core defaults to Production. Set `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` consistently when configuring a host.

Every Development shortcut checks for Development itself, so any other environment (Staging, for example) gets Production's safeguards:

| | Development | Any other environment |
|---|---|---|
| Connection string | LocalDB (`appsettings.Development.json`) | Required; startup stops with a clear error without one |
| Migrations | Applied on startup | Must be deployed first; startup stops if any are pending |
| Demo data | Seeded into an empty database | Never; roles and lookups only |
| Sign-up roles | Applicant or Property Manager | Applicant only (managers come from `Bootstrap:ManagerEmail`) |
| Confirmation link on the page | When the email fails, or with `Registration:ShowConfirmationLink` | Never |
| No SendGrid key | Emails are written to the log | Sending fails; the account stays unconfirmed |
| OpenAPI, migrations endpoint, database error page, "Development Mode" on `/Error` | On | Off |
| Telemetry | Off unless set in user secrets | Production only, from `appsettings.Production.json` |

### Production setup

Production requires an explicit SQL Server connection string and real email delivery. For example, in PowerShell:

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "<production-sql-server-connection-string>"
$env:SendGrid__ApiKey = "<your-sendgrid-api-key>"
$env:SendGrid__FromEmail = "<verified-sender@your-domain>"
$env:SendGrid__FromName = "Property Manager"
dotnet ef database update --project "Troy Web Property Manager.csproj"
dotnet run --launch-profile Production
```

That's fine for trying Production locally. For a real deployment, use your hosting platform's secret configuration, and apply migrations as a deployment step before starting the app (see [Deploying migrations](#deploying-migrations)); startup refuses a database with pending migrations outside Development. Roles and lookup data are still initialized in every environment. Demo accounts and properties are never seeded outside Development. Public registration creates applicants only outside Development. To create a Property Manager, register and confirm the account as usual, then set `Bootstrap:ManagerEmail` to its address (e.g. `$env:Bootstrap__ManagerEmail = "manager@your-domain"`) and restart. On startup that confirmed account becomes a Property Manager (and stops being an Applicant). Unconfirmed accounts are never promoted. Once it's a manager, more managers can be added the same way, one restart each.

### Connection string

Development defaults to LocalDB. Production has no default connection string. To use a different SQL Server in Development:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=Troy_Web_Property_Manager_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

The account needs permission to create the database, or the database must already exist.

### Business time zone

`BusinessTimeZone` (default `America/New_York`) sets what "now" and "today" mean. For example, it decides whether a lease is active today. Use an IANA time zone id; if the setting is left empty, the server's own zone is used.

### Email (SendGrid)

New accounts must confirm their email address before they can log in, so the app sends a confirmation email.

- **Without a SendGrid key the app still runs.** Development registration offers an on-page confirmation link; other Identity emails are written to the log (the console) for local testing. Outside Development a missing key causes delivery to fail and accounts remain unconfirmed.
- **To send real email**, set up a SendGrid account, verified sender and API key (see `Docs/Email Setup.txt`), then:

```sh
dotnet user-secrets set "SendGrid:ApiKey" "<your-sendgrid-api-key>"
dotnet user-secrets set "SendGrid:FromEmail" "<verified-sender@your-domain>"
dotnet user-secrets set "SendGrid:FromName" "Property Manager"
```

**In Development**, failed delivery (including a missing key) shows a **Confirm your account** button. `Registration:ShowConfirmationLink` defaults to `true` in Development, so the page also shows the button after a successful send. To exercise the normal inbox flow in Development:

```sh
dotnet user-secrets set "Registration:ShowConfirmationLink" "false"
```

**Outside Development**, the page never exposes a confirmation token or direct confirmation link, even if `Registration:ShowConfirmationLink` is accidentally enabled. A delivery failure leaves the account unconfirmed, blocks login, and offers a resend link. Confirmation must come from the emailed link; there is no delivery-failure bypass.

## Running the tests

There are two test projects. `dotnet test` from the repo root runs both.

### Unit tests

```sh
dotnet test "Tests/Troy Web Property Manager.Tests"
```

These tests (xUnit) run the services and rules against an in-memory SQLite database built from the real EF model. They also cover what changes between environments (the sign-up roles, the confirmation link, the email sender with no key), the `Bootstrap:ManagerEmail` promotion, and that the Identity setup (`AddAppIdentity`) still matches the migrations. They don't need SQL Server or any configuration, and they take about 10 seconds.

### UI tests

```sh
dotnet test "Tests/Troy Web Property Manager.UITests"
```

These tests (xUnit + Selenium WebDriver, headless Chrome) drive the real app in a browser. They cover every requirement and bonus item in the assessment, for both roles, including security (wrong role, other people's applications, tampered posts, missing antiforgery tokens) and the error cases.

- **Needs:** Google Chrome and SQL Server LocalDB. Selenium Manager downloads the matching ChromeDriver on first run, so the first run needs internet access.
- **What it does:** it starts the app in-process (`WebApplicationFactory` over Kestrel on a free port) against new LocalDB databases. Development fixtures let Program.cs migrate and seed; Production and Staging fixtures deploy the migrations first (with the same `AddAppIdentity` registration as the app) and then start it, as a real deployment would. `ProductionEnvironmentTests` and `StagingEnvironmentTests` check the safeguards in the table under [Configuration](#configuration). The databases are dropped at the end. The tests check they're connected to the throwaway database before running, so your development database is never touched. Emails are captured in memory (registration tests follow the confirmation link), so nothing goes to SendGrid.
- **How long:** a few minutes. The tests share one running app and run one at a time, each in its own browser.
- **When one fails:** the error says what it was waiting for and where the browser was. A screenshot and the page source are saved under the test output folder, in `UiTestArtifacts`.
- **Run a subset:** use a filter, e.g. `--filter "FullyQualifiedName~SecurityTests"`.

The tests are built in layers, so a test reads like the steps a person would take:

| Folder | What's in it |
|---|---|
| `Infrastructure` | `UiFixture` (the running app, test users, database access) and one fixture per configuration (Production, Staging, no SendGrid key, telemetry on, …), `Browser` (waiting, modals, requests with the browser's cookies), the captured email sender and captured logs |
| `Pages` | One page object per page: `LoginPage`, `PropertiesPage`, `UnitsPage`, `ApplicationListPage`, `ApplicationPage`, `QueuePage`, … |
| `Workflows` | Reusable steps built from the pages: `LogInAs`, `RegisterAndConfirm`, `ApplyFor`, `CompleteSections`, `CreateSubmittedApplication`, `Claim`, `ReviewApplication`, `Approve` |
| `Tests` | The tests, one class per area of the assessment |

For example:

```csharp
using var applicant = app.NewBrowserAs(await app.CreateApplicantAsync());
var id = applicant.CreateSubmittedApplication(listing);   // apply, fill both sections, submit
using var manager = app.NewBrowserAs(await app.CreateManagerAsync());
manager.Approve(id);                                      // claim, review, approve
```

### Code coverage

Coverage counts both test projects. The UI tests host the app in-process, so every request they make counts as well. [`coverage.runsettings`](coverage.runsettings) limits it to the app's own hand-written code. It leaves out the test assemblies, packages, EF migrations, Razor markup (`.cshtml`; the `.cshtml.cs` code-behind is still measured) and source-generated files. The settings, the script and the summary are in the **Code Coverage** solution folder.

The latest report is checked in under [`Docs/Code Coverage`](Docs/Code%20Coverage): [`SummaryGithub.md`](Docs/Code%20Coverage/SummaryGithub.md) has the per-class summary, and `index.html` is the full report, line by line (open it in a browser). At the time of writing it shows **99.6% line coverage and 99.0% branch coverage** (3,136 of 3,146 lines, 988 of 997 branches). The few branches left are defensive checks that can't be reached, races between two requests, the demo seeder's fixed random seed, and null checks in the OpenAPI setup.

**In Visual Studio** (any edition of Visual Studio 2026, or Visual Studio 2022 17.8 and later):

1. **Test > Configure Run Settings > Select Solution Wide runsettings File** and pick `coverage.runsettings` in the repo root.
2. **Test > Analyze Code Coverage for All Tests.** This runs the UI tests too, so it needs Chrome and LocalDB and takes about 15 minutes. To skip them, right-click the `Troy Web Property Manager.Tests` project in Test Explorer and choose **Analyze Code Coverage**.
3. The **Code Coverage Results** window shows the numbers per assembly, class and method. Turn on **Show Code Coverage Coloring** to see covered and missed lines in the editor.

While those settings are selected, ordinary test runs collect coverage too. To stop that, choose **Test > Configure Run Settings** and clear the selection.

**From the command line**, [`coverage.ps1`](coverage.ps1) runs the tests with these settings and rebuilds the checked-in report. It gets [ReportGenerator](https://reportgenerator.io) from the local tool manifest (`dotnet-tools.json`), so nothing needs installing first.

```powershell
./coverage.ps1                   # all tests, about 15 minutes, then rebuilds Docs/Code Coverage
./coverage.ps1 -UnitTestsOnly    # unit tests only, under a minute (the report then shows less coverage)
./coverage.ps1 -Open             # open the HTML report when it's done
```

The raw Cobertura files are written to `TestResults/Coverage`, which git ignores. Without the script, you can run `dotnet test --settings coverage.runsettings --collect "Code Coverage;Format=cobertura"`. Rerun the script and commit `Docs/Code Coverage` when the tests change, so the checked-in report stays current.

## API and OpenAPI

The application list's data grid loads its rows from a JSON endpoint:

- `GET /api/applications`: filters, sorts and pages the list in the database, and returns one page of rows plus the filtered total.
- It uses the same sign-in cookie as the site. Sign in first; without the cookie it answers `401`.
- In **Development**, the OpenAPI document is at **`/openapi/v1.json`**. It's generated from the controllers and their XML doc comments.

Details are in `Docs/Bonus-ApplicationGrid.md`.

## Database migrations

The schema is code-first (`Data/Migrations`). Development applies migrations automatically on startup; anywhere else they're a deployment step (see [Deploying migrations](#deploying-migrations)). To change the model, edit the entities or `ApplicationDbContext`, then add a migration:

```sh
dotnet ef migrations add <MigrationName> --project "Troy Web Property Manager.csproj"
```

The next Development run applies it. Deploy it explicitly before the next Production run.

### Deploying migrations

Outside Development the app never changes the schema itself: it refuses to start while migrations are pending, so apply them first, before the new version of the app starts.

**Recommended: an idempotent SQL script.** Generate it where the source is (a build machine or CI), and run it with any SQL client. The server needs neither the .NET SDK nor the source, and a DBA can review exactly what will run:

```sh
dotnet ef migrations script --idempotent --project "Troy Web Property Manager.csproj" --output migrations.sql
sqlcmd -S <server> -d <database> -b -I -i migrations.sql
```

`--idempotent` makes each migration check `__EFMigrationsHistory` first, so the same script is safe on a database at any earlier version, and running it twice does nothing. `-b` stops on the first error; `-I` turns on quoted identifiers, which some of the generated statements need. Ship the script as a build artifact next to the app, so they always match.

**Simple: `dotnet ef database update`** from a machine with the SDK and the source, as in [Production setup](#production-setup). Set `ConnectionStrings__DefaultConnection` (or pass `--connection`) so it targets the production database rather than LocalDB.

**Not currently usable: `dotnet ef migrations bundle`.** The bundle builds, but running it fails with `Could not load file or assembly 'Microsoft.AspNetCore.Identity.UI'` while it looks for the DbContext, even when self-contained.

Take a backup first; some migrations convert existing data (see each migration's `Down` for what a rollback undoes).

## Project layout

| Folder | What's in it |
|---|---|
| `Controllers` | MVC controllers (thin: bind, validate, call a service). `Controllers/Api` holds the JSON API. |
| `Services` | Business operations and data access (`ApplicationService`, `PropertyService`), plus email and the business clock. |
| `Rules` | Pure business rules (workflow, validation, leases), unit-tested without a database. |
| `Models`, `Data` | EF Core entities, `ApplicationDbContext`, migrations, the demo data seeder and `ManagerBootstrapper` (`Bootstrap:ManagerEmail`). |
| `Areas/Identity` | The scaffolded Register and RegisterConfirmation pages, `AddAppIdentity` (the one Identity registration) and `EmailFailureFilter`. |
| `ViewModels`, `Views`, `ViewComponents` | Razor views, partials (modals), view components and their models. |
| `wwwroot/js` | `site.js` (modal forms) and `grid.js` (data grids). No SPA framework. |
| `Docs` | Assessment, ER diagram, entity analysis, UI wireframes, a write-up for each bonus feature (`Bonus-*.md`), `development notes.md` (every check-in, what changed and why), and the latest coverage report in `Code Coverage`. |

## Security notes

- Development sign-up lets you pick your own role, including Property Manager, because the assessment asks for a role picker (1.a.i). Outside Development, both the form and server validation restrict public registration to Applicant; managers are promoted with `Bootstrap:ManagerEmail` (see Production setup).
- The demo accounts share a known password, which is why they are only seeded in Development.
- `Bootstrap:ManagerEmail` only promotes an account whose email is confirmed, so registering someone else's address first doesn't get you the role.
- Outside Development the confirmation link is never put on the page, so every account's email is really verified.
- The GoatCounter page counter is never on the Identity pages, whose query strings carry reset and confirmation tokens.
