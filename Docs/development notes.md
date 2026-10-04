# Troy Web Property Manager - Development Notes

## What this is

A walk through every check-in in the repository, oldest first. Each step is one commit: what changed and **why** - .

## How to read it

- **Step header:** `Step N: <commit message>`.
- **Assessment** lists the parts of the PDF the step works on, using the PDF's own numbering:

  | Reference | What it covers |
  |---|---|
  | Technical 1.a-1.c | ASP.NET Core MVC + Razor: controllers, view models, views, partials, view components, modals |
  | Technical 2.a-2.c | .NET 10 backend: Identity, create/migrate/seed on start (Bogus), unit tests |
  | Technical 3.a | SQL Server with EF Core code-first migrations |
  | Functional 1-6 | Users, Properties & Units, Rental Application, Review, Application List |
  | Bonus 1-5 | Paged/sorted grid + JSON/OpenAPI, review queue, manager notes, save-with-errors, multiple applicants |
  | Considerations | Production-ready code, conventions, explainable design decisions |
  | Deliverables | README.md with setup instructions |

- **File status:** **A** = added, **M** = modified, **D** = deleted, **R** = renamed.
- Merge commits (one per pull request) add no changes of their own, so they aren't steps.
- Third-party files under `wwwroot/lib` are summarised, not listed one by one.

> **Security note found while writing this:** Step 5 committed a real SendGrid API key in `Docs/Email Setup.txt`.
> Step 48 replaced it with a placeholder, but the key is still in the git history. It must be revoked in SendGrid
> (and the history rewritten if the repository is public).

## Phase 1: Project setup

### Step 1: Initial Commit

**Assessment:** Technical 1, 2, 2.a, 3 - the starting point: ASP.NET Core on .NET 10, Identity, SQL Server.

**Why:** Created from the ASP.NET Core Web App template with Individual Accounts. That gives ASP.NET Identity (sign up, log in, log out - Functional 1.a), a LocalDB connection string, EF Core for SQL Server and a Bootstrap layout for free. The template is Razor Pages; MVC controllers and views are added on top later (Step 16) because Technical 1.a asks for controllers, view models, views, partials and view components.

**Files:**

- **A** `.gitattributes` - Normalises line endings across Windows/Git.
- **A** `.gitignore` - Keeps bin/ and obj/ out of source control.
- **A** `Troy Web Property Manager.csproj` - net10.0 web project with the Identity UI, EF Core SQL Server and tools packages.
- **A** `Troy Web Property Manager.slnx` - Solution in the new XML .slnx format (later also holds the test projects).
- **A** `Troy Web Property Manager.csproj.user` - Visual Studio per-user settings (removed in Step 42).
- **A** `Program.cs` - Template host: AddDbContext (SQL Server), AddDefaultIdentity with RequireConfirmedAccount, Razor Pages, the HTTP pipeline.
- **A** `appsettings.json, appsettings.Development.json` - Connection string (LocalDB) and logging levels.
- **A** `Properties/launchSettings.json` - http (5140) / https (7066) profiles, both in the Development environment.
- **A** `Properties/serviceDependencies.json, serviceDependencies.local.json, serviceDependencies.local.json.user` - Visual Studio "connected services" record of the SQL Server dependency (the .user file is removed in Step 48).
- **A** `Data/ApplicationDbContext.cs` - IdentityDbContext; grows into the whole schema from Step 11.
- **A** `Data/Migrations/00000000000000_CreateIdentitySchema.cs (+ .Designer.cs), ApplicationDbContextModelSnapshot.cs` - The Identity tables (AspNetUsers, AspNetRoles, ...) as the first code-first migration (Technical 3.a).
- **A** `Pages/Index, Privacy, Error (.cshtml + .cs), Pages/_ViewImports.cshtml, Pages/_ViewStart.cshtml` - Template Razor Pages (home, privacy, error page).
- **A** `Pages/Shared/_Layout.cshtml (+ .css), _LoginPartial.cshtml, _ValidationScriptsPartial.cshtml` - Shared layout, login/logout menu and the jQuery validation scripts. The layout is reused by the MVC views and the Identity pages.
- **A** `Areas/Identity/Pages/_ViewStart.cshtml` - Makes the Identity UI pages use the app's layout.
- **A** `wwwroot/css/site.css, wwwroot/js/site.js, wwwroot/favicon.ico` - Template static files (site.js becomes the modal plumbing in Step 17).
- **A** `wwwroot/lib/bootstrap, jquery, jquery-validation, jquery-validation-unobtrusive` - Client libraries kept in the repo (no CDN): Bootstrap for layout and modals, jQuery validation for browser-side DataAnnotations checks (Technical 1.b/1.c).
- **A** `README.md` - Placeholder title; filled in by the Deliverables steps.

### Step 2: Register Email SendGrid

**Assessment:** Functional 1.a (sign up).

**Why:** The template sets RequireConfirmedAccount, so a new user must click an emailed link before logging in - but the default Identity UI only has a do-nothing email sender. A real IEmailSender makes sign-up actually work.

**Files:**

- **M** `Program.cs` - Binds the "SendGrid" config section and registers EmailSender as IEmailSender.
- **A** `Services/EmailSender.cs` - SendGrid implementation of IEmailSender. Click tracking is turned off because SendGrid's link rewriting breaks the confirmation token. Logs and throws on failure.
- **M** `Troy Web Property Manager.csproj` - Adds the SendGrid 9.29.3 package.

### Step 3: Add installation instructions to README

**Assessment:** Deliverables (README with setup instructions).

**Files:**

- **M** `README.md` - First install list: Visual Studio 2026, .NET 10, LocalDB, SSMS, dotnet-ef.

### Step 4: Revise installation section in README.md

**Assessment:** Deliverables.

**Files:**

- **M** `README.md` - Restructured the install list into nested steps.

### Step 5: add Notes for setting up Email service provider

**Assessment:** Functional 1.a; Considerations (traceability of decisions).

**Files:**

- **A** `Docs/Email Setup.txt` - How to set up SendGrid (account, verified sender, API key) and the user-secrets commands. NOTE: this version contained a live API key - see the security note at the top.
- **A** `Docs/Troy Web Technical Assessment.pdf` - The brief, kept with the code so every decision can be traced to it.
- **A** `Docs/DB Schema - Default.pdf` - The template's Identity schema, as a reference when designing tables.

### Step 6: Create DB in application

**Assessment:** Technical 2.b.i (create the database on start).

**Files:**

- **M** `Program.cs` - Adds CreateDatabase() at startup, so a fresh clone needs no manual "dotnet ef database update". It used EnsureCreated() for now (there were no domain migrations yet) and an empty SeedData() placeholder ("TODO: Use Bogus?" - Technical 2.b.ii.1).

## Phase 2: Analysis and data model

### Step 7: Create Troy Web Property Manager TODO.md

**Assessment:** all - a checklist made from the PDF.

**Files:**

- **A** `Troy Web Property Manager TODO.md` - Identity, database (migrations, init on start, Bogus seed), data analysis, components (controllers, view models, partials, view components, modals), tests. Ticked off as the work landed (Step 48).

### Step 8: Create Application Status State Diagram.png

**Assessment:** Functional 5.b (statuses, terminal states), Challenge a/b (submit, withdraw, return, resubmit).

**Files:**

- **A** `Docs/Application Status State Diagram.png` - The allowed status changes, designed before any code. The state machine in Rules/ApplicationWorkflow.cs implements this diagram.

### Step 9: Mock ups and DB ER

**Assessment:** Functional 1-6 (entities and screens); Considerations (design before build).

**Files:**

- **A** `Docs/ER Diagram.png` - Entities and relationships for properties, units, unit types, applicants, applications, residences, leases, status history.
- **A** `Docs/Troy Web - Property Manager Entity analysis.md` - Written analysis of each entity and field, taken from the nouns in the PDF.
- **A** `Docs/UI/Register.png` - Sign-up with the role picker (Functional 1.a.i).
- **A** `Docs/UI/Properties List.png` - Manager's properties and units (Functional 2.b).
- **A** `Docs/UI/Available Residences.png` - Applicant's available units (Functional 2.b).
- **A** `Docs/UI/Application.png` - The one-page, one-section-at-a-time application (Functional 4.a/4.b).
- **A** `Docs/UI/Residence Applications.png` - The application list (Functional 6.a).

### Step 10: Hand drawn analysis

**Assessment:** as Step 9.

**Files:**

- **A** `Docs/UI/Application Status.jpg, DB Entities.jpg, Wireframes 01.jpg, Wireframes 02.jpg` - The original whiteboard sketches the diagrams and mock-ups were drawn from, kept as the raw source.

### Step 11: Add EF migration for property management schema

**Assessment:** Technical 3.a (EF Core code-first migrations); Functional 2 and 4 (the entities).

**Why:** The tables were first sketched in SQL Server and scaffolded into C# (Scaffold-DbContext, see Step 13). From here the C# model owns the schema and every change is a migration. The branch name says it: "migrateFromDbToCode".

**Files:**

- **A** `Models/Applicant.cs, ApplicationStatusHistory.cs, Lease.cs, Property.cs, RentApplication.cs, Residence.cs, Status.cs, Unit.cs, UnitType.cs` - Entity classes for the domain.
- **M** `Data/ApplicationDbContext.cs` - DbSets plus Fluent API mapping: table names, keys, foreign keys, lengths, defaults.
- **A** `Data/Migrations/20260929195557_AddPropertyManagementSchema.cs (+ .Designer.cs)` - Creates the domain tables.
- **M** `Data/Migrations/ApplicationDbContextModelSnapshot.cs` - EF's record of the current model.
- **M** `Program.cs` - EnsureCreated() replaced by Migrate(): EnsureCreated bypasses migrations, so the schema could never change after the first run (Technical 2.b.i).

### Step 12: Change name of DB

**Files:**

- **M** `appsettings.json` - Readable database name "Troy_Web_Property_Manager_DB" instead of the template's GUID name.
- **M** `Troy Web Property Manager.csproj` - UserSecretsId renamed to match.

### Step 13: Create DB Migration Commands.txt

**Files:**

- **A** `Docs/DB Migration Commands.txt` - The Scaffold-DbContext and Add-Migration commands used, so they can be repeated.

## Phase 3: Identity, roles and the MVC skeleton

### Step 14: Add Login Scaffolding

**Assessment:** Functional 1.a (sign up, log in, log out).

**Why:** Only the Identity pages that need changing were scaffolded into the project (Register needs the role picker; the others get the app's styling). Everything else stays in the Identity UI package.

**Files:**

- **A** `Areas/Identity/Pages/Account/Login.cshtml (+ .cs), Logout.cshtml (+ .cs), Register.cshtml (+ .cs), Manage/Index.cshtml (+ .cs), Manage/ChangePassword.cshtml (+ .cs), Areas/Identity/Pages/_ViewImports.cshtml` - The scaffolded account pages.
- **M** `Troy Web Property Manager.csproj.user` - Visual Studio scaffolder state.

### Step 15: Add Model Roles

**Assessment:** Functional 1.a.i (choose Applicant or Property Manager at sign-up); Technical 2.a (Identity roles).

**Files:**

- **A** `Models/AppRoles.cs` - The two role names as constants plus an All list - one source for [Authorize(Roles)], the sign-up picker and the startup seeding.
- **M** `Areas/Identity/Pages/Account/Register.cshtml` - Radio buttons for the role.
- **M** `Areas/Identity/Pages/Account/Register.cshtml.cs` - Role is [Required]; the posted value is checked against AppRoles.All on the server (never trust a posted role); the user is added to the role, and deleted again if that fails so no account is left without a role.
- **M** `Program.cs` - `AddRoles<IdentityRole>()` so roles work at all; roles created on startup.

### Step 16: Add Role Navigation to Layout

**Assessment:** Challenge ("the permissions in the controllers and the UI reflect this"); Technical 1.a (MVC).

**Files:**

- **M** `Pages/Shared/_Layout.cshtml` - Menu links per role: managers get Properties and Applications, applicants get Available units and My applications.
- **M** `Pages/_ViewImports.cshtml, Areas/Identity/Pages/_ViewImports.cshtml` - Import Models so AppRoles works in views.
- **M** `Program.cs` - AddControllersWithViews with a global AutoValidateAntiforgeryToken filter (every MVC POST is checked, so no action can forget it) and SuppressImplicitRequiredAttributeForNonNullableReferenceTypes (only an explicit [Required] counts). Conventional MVC route. Services registered as scoped.
- **A** `Services/ApplicationService.cs, Services/PropertyService.cs` - Empty classes that set up the service layer: the business rules live in services, not in controllers.

### Step 17: Layout

**Assessment:** Technical 1.b (modals filled from partial views; re-render on error, close and refresh on success).

**Files:**

- **M** `Pages/Shared/_Layout.cshtml` - Success/error messages from TempData, and the one shared modal (#app-modal).
- **A** `Views/_ViewImports.cshtml, Views/_ViewStart.cshtml` - MVC views get the tag helpers and the same layout.
- **M** `wwwroot/js/site.js` - The modal plumbing: data-modal-url loads a partial into the modal; data-modal-form posts with fetch; an HTML response means "redraw with errors", JSON means "close and refresh this part of the page". Driven by data- attributes, so new modals need no new JavaScript.

### Step 18: Default controllers and Data Model

**Assessment:** Technical 1.a; Challenge (permissions); Technical 2.b.ii (lookups); Functional 2.c, 4, 5.

**Files:**

- **A** `Controllers/AppController.cs` - Base controller: CurrentUser from the claims, modal result helpers, flash messages.
- **A** `Controllers/ApplicationsController.cs, PropertiesController.cs, UnitsController.cs` - Controllers locked to their roles with [Authorize(Roles = ...)].
- **A** `Services/CurrentUser.cs` - The signed-in user (id + manager flag) handed to services, so they can do ownership checks without HttpContext - and can be unit tested.
- **A** `Services/ServiceResult.cs` - One result type (Ok / Missing / Error) instead of exceptions for expected outcomes.
- **M** `Services/ApplicationService.cs, Services/PropertyService.cs` - First queries.
- **A** `Models/Enums.cs` - ApplicationStatus and friends.
- **M** `Models/Applicant.cs` - UserId links an applicant profile to its Identity user.
- **M** `Models/ApplicationStatusHistory.cs` - ChangedByUser becomes the Identity user id (a string), not an int.
- **M** `Models/RentApplication.cs` - Submitted is nullable (drafts haven't been submitted); flags for "section saved" (Functional 4.b.ii: Submit only once both sections are saved).
- **M** `Models/Unit.cs` - Rent float -> decimal: money must be exact.
- **M** `Data/ApplicationDbContext.cs` - One profile per user (unique index), FK to AspNetUsers, decimal(10,2) rent, 500-character review comments.
- **A** `Data/Migrations/20260930020509_ApplicationWorkflowSchema.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs` - The schema changes above.
- **M** `Program.cs` - Seeds the lookups on startup: Status rows whose ids match the enum (needs IDENTITY_INSERT), and unit types including an inactive "Loft" (Functional 2.c).
- **A** `Views/Applications/Index.cshtml, Views/Properties/Index.cshtml, Views/Units/Index.cshtml` - Placeholder pages.
- **M** `Views/_ViewImports.cshtml` - Namespaces for views.
- **M** `wwwroot/js/site.js` - Handles 401/403 on AJAX calls (sent with X-Requested-With) so a signed-out user is sent to the login page instead of seeing it inside the modal.

### Step 19: Fix Confirm

**Assessment:** Technical 1.a (partial views), 1.b (modals).

**Files:**

- **A** `ViewModels/ConfirmViewModel.cs` - Title, message, URL and button text for a confirmation.
- **A** `Views/Shared/_Confirm.cshtml` - One reusable "are you sure?" modal for every destructive action, with its own antiforgery token.
- **M** `Views/_ViewImports.cshtml` - Imports the ViewModels namespace.

### Step 20: Properties

**Assessment:** Functional 2.b (managers add/edit/remove properties and units through modals), 2.c (inactive unit types), 2.d (lease term, "available"); Technical 1.b.

**Files:**

- **M** `Controllers/PropertiesController.cs` - GET returns the modal partial, POST returns the same partial with a 422 if invalid or JSON (close and refresh) if saved - Technical 1.b exactly. The unit type dropdown is rebuilt from the unit's stored type, never the post.
- **M** `Services/PropertyService.cs` - Property/unit CRUD, the unit type options, available units.
- **A** `Rules/BusinessRules.cs` - Pure rules, easy to unit test: an inactive type is only allowed on a unit that already has it (2.c); the 12-month lease term; "lease active on a day" as an expression EF turns into SQL (2.d).
- **A** `ViewModels/PropertyViewModels.cs` - Form and display models with DataAnnotations.
- **M** `Views/Properties/Index.cshtml`, **A** `Views/Properties/_PropertyForm.cshtml, _PropertyList.cshtml, _UnitForm.cshtml` - The page plus the modal partials and the list partial it refreshes.

### Step 21: Update Program.cs

**Files:**

- **M** `Program.cs` - Adds app.UseAuthentication(). Without it the auth cookie was never read, so [Authorize] treated everyone as signed out.

## Phase 4: Rental applications

### Step 22: Application

**Assessment:** Functional 4 (application), 5.a/5.b (review outcomes and statuses).

**Files:**

- **M** `Rules/BusinessRules.cs` - The status state machine (CanTransition, IsTerminal), IsEditable (Draft or Returned only - 4.d), RequiresComment (Return/Deny - 5.a), and Next/Previous section (4.b).
- **M** `Services/ApplicationService.cs` - Visible() - "who can see what" in one place, as a query filter - plus Start, the editor model, section saves, residences and review.
- **A** `ViewModels/ApplicationViewModels.cs` - Models for the application page.
- **M** `Models/ApplicationStatusHistory.cs, Models/Enums.cs` - History stores the review outcome; ReviewOutcome enum.
- **M** `Models/Residence.cs` - Move-in / move-out dates, the PDF's wording (4.a.ii).
- **M** `Data/ApplicationDbContext.cs`, **A** `Data/Migrations/20260930051442_ReviewOutcomeAndOptionalComment.cs (+ .Designer.cs)` - Outcome column; the comment becomes optional (only Return and Deny need one).
- **A** `Data/Migrations/20260930052945_ResidenceMoveDates.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs` - Renames StartDate/EndDate to MoveInDate/MoveOutDate.

### Step 23: Units

**Assessment:** Functional 2.b (applicants browse available units and start an application), 2.d.

**Files:**

- **M** `Controllers/UnitsController.cs` - Available units page with a property filter.
- **M** `Controllers/ApplicationsController.cs` - Start(unitId) as a POST (it creates data, and gets the antiforgery check).
- **M** `Services/PropertyService.cs` - Available = no lease covering today, filtered in SQL.
- **M** `ViewModels/PropertyViewModels.cs`, **M** `Views/Units/Index.cshtml` - The page and its model.

### Step 24: Create Edit Model

**Assessment:** Functional 4.a-4.d; Technical 1.a/1.b.

**Why:** 4.b asks for a single page that shows one section at a time, one view model, one form posting to one action, with the clicked button deciding what happens.

**Files:**

- **M** `Controllers/ApplicationsController.cs` - Edit GET (one section at a time) and one Edit POST that switches on the button's command (back / continue / submit); residence and withdraw modal actions.
- **M** `Services/ApplicationService.cs`, **M** `ViewModels/ApplicationViewModels.cs`, **M** `Models/Enums.cs (ApplicationSection)` - The editor's data and the section enum.
- **A** `Views/Applications/Edit.cshtml` - The one page and one form.
- **A** `Views/Applications/_ApplicantInformation.cshtml, _ResidenceHistory.cshtml, _Summary.cshtml` - One partial per section (4.b). The same partial renders editable or read-only from a server-side flag (4.d); the Summary reuses both.
- **A** `Views/Applications/_ResidenceForm.cshtml` - Residence add/edit modal (4.c).
- **M** `Views/_ViewImports.cshtml`, **M** `wwwroot/js/site.js` - Supporting changes for the new modals.

### Step 25: Add History

**Assessment:** Functional 5.a (review modal), 5.c (history: who, when, comment), 2.d/4.e (lease on approval, no second lease); Technical 1.a (view components are required).

**Files:**

- **M** `Controllers/ApplicationsController.cs` - Review GET/POST (managers only).
- **M** `Services/ApplicationService.cs` - Approval runs in a serializable transaction so two approvals can't both pass the "unit not leased" check; a deadlock (the race loser) becomes a friendly message.
- **A** `ViewComponents/ApplicationHistoryViewComponent.cs`, **A** `Views/Shared/Components/ApplicationHistory/Default.cshtml` - The history panel. A view component because it loads its own data, which the page's view model (also used for applicants) shouldn't carry.
- **A** `Views/Applications/_ReviewForm.cshtml` - Approve / Return / Deny with a comment.
- **M** `Views/Applications/Edit.cshtml`, **M** `Views/_ViewImports.cshtml` - Show the panel; enable `<vc:...>` tags.

### Step 26: Application LIst Filtered

**Assessment:** Functional 6.a (list filtered by status and property).

**Files:**

- **M** `Views/Applications/Index.cshtml` - The list with a GET filter form (status, property).
- **M** `Rules/BusinessRules.cs` - Formatting only.

### Step 27: Fixes Step 1

**Assessment:** Functional 6.a ("filtering done in the database, not in memory").

**Files:**

- **M** `Controllers/ApplicationsController.cs` - Index binds the filters and calls ListAsync, which adds a Where per filter to the IQueryable - so SQL Server does the filtering.
- **M** `Views/Applications/Index.cshtml` - Labels, Clear link, role-based title.

### Step 28: Fix field names

**Assessment:** Functional 4.a; Considerations (names that match the domain).

**Why:** Names were brought in line with the PDF's vocabulary, and section 1 became a per-application copy so editing one application can't change another that was already submitted.

**Files:**

- **R** `Models/RentApplication.cs` → `Models/RentalApplication.cs` - "Rental application", as in the PDF.
- **M** `Models/Applicant.cs, ApplicationStatusHistory.cs, Lease.cs, Residence.cs, Status.cs, Unit.cs, UnitType.cs, Enums.cs` - Renames: ApplicantSectionSaved -> ApplicantInformationSaved, Rent -> MonthlyRent, Active -> IsActive, and so on.
- **A** `Models/ApplicantInformation.cs` - Section 1 stored per application; the applicant profile only pre-fills it.
- **M** `Data/ApplicationDbContext.cs`, **M** `ApplicationDbContextModelSnapshot.cs`
- **A** `Data/Migrations/20260930065413_NormalizeNamesToAssessment.cs (+ .Designer.cs)` - Table/column/index renames.
- **A** `Data/Migrations/20260930070114_PerApplicationApplicantInformation.cs (+ .Designer.cs)` - New table, existing data copied across by SQL in the migration.
- **M** `Controllers/ApplicationsController.cs, Program.cs, Rules/BusinessRules.cs, Services/ApplicationService.cs, Services/PropertyService.cs, ViewModels/ApplicationViewModels.cs, Views/Applications/Edit.cshtml, Views/Applications/_ApplicantInformation.cshtml` - Follow the renames.
- **A** `Troy Web Technical Assessment.pdf` - Copy of the brief added to the root by mistake (removed in Step 29).

### Step 29: Make separate files

**Assessment:** Considerations (conventions: one type per file).

**Files:**

- **D** `Models/Enums.cs` → **A** `Models/ApplicationSection.cs, ApplicationStatus.cs, ReviewOutcome.cs, EnumExtensions.cs` - Enums split; DisplayName() reads [Display] names for the UI.
- **R** `Rules/BusinessRules.cs` → `Rules/ApplicationWorkflow.cs`, **A** `Rules/LeaseRules.cs`, **A** `Rules/UnitTypeRules.cs` - One rules class per concern.
- **D** `ViewModels/ApplicationViewModels.cs`, **D** `ViewModels/PropertyViewModels.cs` → **A** one file per view model: ApplicantInformationViewModel, ApplicationEditorViewModel, ApplicationListItemViewModel, ApplicationListViewModel, AvailableUnitViewModel, AvailableUnitsPageViewModel, HistoryItemViewModel, PropertyFormViewModel, PropertyViewModel, ResidenceViewModel, ReviewViewModel, UnitFormViewModel, UnitViewModel.
- **A** `Services/SendGridOptions.cs`, **M** `Services/EmailSender.cs` - Options class moved to its own file.
- **M** `Models/ApplicationStatusHistory.cs, RentalApplication.cs, Status.cs, Program.cs, Services/ApplicationService.cs` - Status-backed columns become long, so the enums are ": long".
- **A** `Data/Migrations/20260930071136_EnumBackedColumnsToBigint.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs` - int -> bigint for the status columns and the Status key.
- **D** `Troy Web Technical Assessment.pdf` - Duplicate of Docs/ removed.

### Step 30: moar fixes

**Assessment:** Functional 4.e/5 (no second lease, safe concurrent changes); Considerations (production-ready).

**Files:**

- **M** `Data/ApplicationDbContext.cs` - RentalApplication.Status is a concurrency token (a withdraw racing an approval fails instead of overwriting); a filtered unique index allows one open application per applicant and unit; unit numbers are unique within a property.
- **A** `Data/Migrations/20260930074112_ConcurrencyAndUniqueIndexes.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs`
- **A** `Services/SqlErrors.cs` - Recognises SQL Server unique-key and deadlock errors, so services can turn them into messages.
- **M** `Services/ApplicationService.cs`, **M** `Services/PropertyService.cs` - Catch those cases: retry Start after a double-click, "someone else changed it" on a stale save, duplicate unit number.
- **M** `Services/EmailSender.cs`, **M** `Program.cs` - EmailSender becomes a singleton reusing one SendGridClient (and its HttpClient); HTML-only messages so the plain-text part doesn't show raw tags.
- **M** `Controllers/ApplicationsController.cs` - Withdraw modal returns 404 unless the application can be withdrawn; lease length comes from LeaseRules.TermMonths.

## Phase 5: Tests, seed data, clarity

### Step 31: Add Test Project

**Assessment:** Technical 2.c (unit tests for business logic).

**Files:**

- **A** `Tests/Troy Web Property Manager.Tests/Troy Web Property Manager.Tests.csproj` - xUnit project referencing the app.
- **A** `Tests/.../TestDatabase.cs` - In-memory SQLite built from the real EF model, so constraints, unique indexes and the concurrency token behave like the real database.
- **A** `Tests/.../Rules/ApplicationWorkflowTests.cs, LeaseRulesTests.cs, UnitTypeRulesTests.cs` - The pure rules.
- **A** `Tests/.../Services/ApplicationServiceTests.cs, PropertyServiceTests.cs` - The services against the database.
- **A** `Tests/.../ViewModels/ViewModelValidationTests.cs` - The DataAnnotations rules.
- **M** `Troy Web Property Manager.csproj` - Excludes `Tests/**` so the web project doesn't compile the test code.
- **M** `Troy Web Property Manager.slnx` - Adds the test project under /Tests/.
- **M** `.gitignore` - bin/ and obj/ ignored in nested projects too.

### Step 32: Add Seed Data

**Assessment:** Technical 2.b.ii and 2.b.ii.1 (seed idempotently with Bogus: managers, applicants, properties, units, applications in every status).

**Files:**

- **A** `Data/DemoDataSeeder.cs` - Bogus with a fixed seed. Each application walks a legal path through the state machine (with history, residences and leases), so the data is something the app could have made. Only runs on an empty database (idempotent), in one transaction.
- **M** `Program.cs` - Seeds demo data in Development only - the demo accounts share a known password.
- **A** `Tests/.../Data/DemoDataSeederTests.cs`, **M** `Tests/.../TestDatabase.cs` - Every status present, legal histories, no overlapping leases, running twice adds nothing.
- **M** `Troy Web Property Manager.csproj` - Adds the Bogus package.

### Step 33: Expand all Arrow functions

**Assessment:** Considerations (one consistent, readable style).

**Why:** Expression-bodied members (=>) were turned into block bodies everywhere, for one consistent style that is easier to read, set breakpoints in and extend.

**Files (all modified):**

- `Controllers/AppController.cs, ApplicationsController.cs, PropertiesController.cs, UnitsController.cs`
- `Data/DemoDataSeeder.cs`
- `Models/EnumExtensions.cs`
- `Pages/Error.cshtml.cs`
- `Rules/ApplicationWorkflow.cs, LeaseRules.cs, UnitTypeRules.cs`
- `Services/ApplicationService.cs, PropertyService.cs, ServiceResult.cs, SqlErrors.cs`
- `ViewModels/ApplicationEditorViewModel.cs`
- `Tests/.../DemoDataSeederTests.cs, ApplicationWorkflowTests.cs, LeaseRulesTests.cs, UnitTypeRulesTests.cs, ApplicationServiceTests.cs, PropertyServiceTests.cs, ViewModelValidationTests.cs`

### Step 34: Add comments

**Assessment:** Considerations ("be ready to explain the rationale behind your design decisions").

**Why:** XML doc comments and inline comments were added across the code, saying why it's built that way and which part of the PDF each piece covers. No behaviour changed.

**Files (all modified):**

- `Areas/Identity/Pages/Account/Register.cshtml (+ .cs)`
- `Controllers/AppController.cs, ApplicationsController.cs, PropertiesController.cs, UnitsController.cs`
- `Data/ApplicationDbContext.cs, DemoDataSeeder.cs`
- `Models/AppRoles.cs, Applicant.cs, ApplicantInformation.cs, ApplicationSection.cs, ApplicationStatus.cs, ApplicationStatusHistory.cs, Lease.cs, Property.cs, RentalApplication.cs, ReviewOutcome.cs, Status.cs, Unit.cs, UnitType.cs`
- `Pages/Shared/_Layout.cshtml`
- `Program.cs`
- `Rules/ApplicationWorkflow.cs, LeaseRules.cs, UnitTypeRules.cs`
- `Services/ApplicationService.cs, CurrentUser.cs, EmailSender.cs, PropertyService.cs, SendGridOptions.cs, ServiceResult.cs, SqlErrors.cs`
- `Tests/.../DemoDataSeederTests.cs, ApplicationServiceTests.cs, TestDatabase.cs`
- `ViewComponents/ApplicationHistoryViewComponent.cs`
- `ViewModels/ApplicantInformationViewModel.cs, ApplicationEditorViewModel.cs, ApplicationListItemViewModel.cs, ApplicationListViewModel.cs, AvailableUnitViewModel.cs, AvailableUnitsPageViewModel.cs, ConfirmViewModel.cs, HistoryItemViewModel.cs, PropertyFormViewModel.cs, PropertyViewModel.cs, ResidenceViewModel.cs, ReviewViewModel.cs, UnitFormViewModel.cs, UnitViewModel.cs`
- `Views/Applications/Edit, Index, _ApplicantInformation, _ResidenceForm, _ResidenceHistory, _ReviewForm, _Summary`
- `Views/Properties/Index, _PropertyForm, _PropertyList, _UnitForm`
- `Views/Shared/Components/ApplicationHistory/Default.cshtml, Views/Shared/_Confirm.cshtml, Views/Units/Index.cshtml`
- `Views/_ViewImports.cshtml, Views/_ViewStart.cshtml`
- `wwwroot/js/site.js`

### Step 35: add constraints for bedrooms and dates

**Assessment:** Technical 3.a; Considerations (the database protects its own data, not just the UI).

**Files:**

- **M** `Data/ApplicationDbContext.cs` - CHECK constraints: bedrooms 0-10, move-out on or after move-in, lease end after start.
- **A** `Data/Migrations/20260930164227_AddCheckConstraints.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs`
- **A** `Tests/.../Data/CheckConstraintTests.cs` - The database rejects bad rows even if code skips validation.

### Step 36: minimum bedrooms

**Assessment:** Functional 2.b (applicants browse available units).

**Files:**

- **M** `Controllers/UnitsController.cs, Services/PropertyService.cs, ViewModels/AvailableUnitsPageViewModel.cs, Views/Units/Index.cshtml` - "Bedrooms 1+/2+/3+" filter, applied in SQL.
- **M** `Tests/.../PropertyServiceTests.cs` - Filter tests.

### Step 37: add index

**Assessment:** Functional 2.d/4.e (the "active lease today" check); Considerations (performance).

**Files:**

- **M** `Data/ApplicationDbContext.cs` - Composite index on Lease (UnitId, StartDate, EndDate) for the lookup that availability, submit and approval all run.
- **A** `Data/Migrations/20260930164927_IndexActiveLeaseLookup.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs`

## Phase 6: Correctness, concurrency and code review

### Step 38: fix forbidden and conflict outcomes

**Assessment:** Challenge (permissions); Functional 4.d ("controllers reject posts that are not allowed").

**Files:**

- **M** `Services/ServiceResult.cs` - Adds Forbidden (403) and Conflict (someone else changed it), next to NotFound.
- **M** `Controllers/AppController.cs` - IsAccessFailure()/Failure() map them to real 404/403 responses. Someone else's application is a 404, so it doesn't even admit it exists.
- **M** `Controllers/ApplicationsController.cs, PropertiesController.cs, Services/ApplicationService.cs` - Use them.
- **M** `Tests/.../ApplicationServiceTests.cs` - Tests for each outcome.

### Step 39: handle 409 conflicts in modals

**Assessment:** Technical 1.b.

**Files:**

- **M** `Controllers/AppController.cs` - ModalFailed(): the same partial with the service's message; 409 for a conflict, 422 for a validation failure, so the script can tell them apart.
- **M** `Controllers/ApplicationsController.cs, PropertiesController.cs` - Use ModalFailed.
- **M** `wwwroot/js/site.js` - Redraws the partial on 409 as well; posts the clicked button with the form.

### Step 40: refresh only the edited property card

**Assessment:** Technical 1.b ("refresh the affected part of the page").

**Files:**

- **A** `Views/Properties/_PropertyCard.cshtml` - One property card as its own partial.
- **M** `Views/Properties/_PropertyList.cshtml, Views/Properties/Index.cshtml` - Built from cards.
- **M** `Controllers/PropertiesController.cs` - Card(id) action; edits redraw just that card instead of the whole list.
- **M** `Services/PropertyService.cs, Tests/.../PropertyServiceTests.cs` - Single-property query and its tests.

### Step 41: show submission blockers on summary

**Assessment:** Functional 4.b.ii (Submit only once both sections are saved), 4.e (active lease).

**Files:**

- **A** `Rules/SubmissionRules.cs` - One list of what's stopping Submit, used by both the Summary and SubmitAsync, so the page and the server can't disagree.
- **M** `Services/ApplicationService.cs, ViewModels/ApplicationEditorViewModel.cs, Views/Applications/_Summary.cshtml` - Show the list on the Summary and disable Submit while it isn't empty.
- **A** `Tests/.../Rules/SubmissionRulesTests.cs`, **M** `Tests/.../ApplicationServiceTests.cs`

### Step 42: Fix failed save and race condition

**Assessment:** Technical 1.b; Functional 2.b; Considerations (production-ready).

**Files:**

- **M** `Controllers/PropertiesController.cs` - A refused save redraws the modal with the reason; delete modals return 404 for something that no longer exists.
- **M** `Services/PropertyService.cs, Services/SqlErrors.cs` - A delete or unit-number save that loses a race against another request (FK / unique violation) becomes a message, not a 500.
- **M** `Services/ApplicationService.cs` - GuardStatus(): residence edits also check the application's status, so an old tab can't change a submitted application.
- **M** `Services/EmailSender.cs` - With no API key (a fresh clone), Development writes the email to the log instead of failing; elsewhere it throws and Register shows a message.
- **M** `Pages/Index.cshtml` - Home page with role-specific links instead of the template text.
- **M** `Tests/.../ApplicationServiceTests.cs`
- **D** `Troy Web Property Manager.csproj.user` - Per-user Visual Studio file; shouldn't be in the repo.

### Step 43: Fix title

**Files:**

- **M** `Pages/Shared/_Layout.cshtml` - "Troy Web Property Manager" (spaces) in the title, brand and footer instead of the assembly name with underscores.

### Step 44: Add warning to Readme

**Assessment:** Deliverables; Functional 1.a.i.

**Files:**

- **M** `README.md` - Explains running without SendGrid, and a security note: picking your own role (as 1.a.i asks) means anyone can become a Property Manager - a real deployment should invite or approve managers.

### Step 45: Fix ignored files

**Files:**

- **M** `.gitignore` - Ignore `*.user`, IDE folders, test results, coverage, publish output and logs.

### Step 46: Validate email sender

**Assessment:** Functional 1.a.

**Files:**

- **M** `Services/EmailSender.cs` - Fails with a clear error when SendGrid:FromEmail is missing, and reads SendGrid's error body safely.

### Step 47: Change to Async await

**Assessment:** Considerations (conventions).

**Files:**

- **M** `Program.cs` - async Main, MigrateAsync and async seeding, replacing the blocking .GetAwaiter().GetResult() calls.

### Step 48: Fix code review findings and redact SendGrid key

**Assessment:** Functional 6.a (who sees what); Considerations; security.

**Files:**

- **M** `Docs/Email Setup.txt` - The real API key replaced with a placeholder (still in history - revoke it).
- **D** `Properties/serviceDependencies.local.json.user` - Machine-specific file removed.
- **M** `Services/ApplicationService.cs` - Managers only see applications that have been submitted at least once - a draft that was never submitted stays private to its applicant. Section 1 saves also check the status (GuardStatus).
- **M** `Rules/LeaseRules.cs` - IsActiveOn compares directly instead of compiling an expression on every call.
- **M** `Program.cs` - Async ToHashSetAsync in the lookup seeding.
- **M** `Services/EmailSender.cs, wwwroot/js/site.js` - Tidy-ups from the review.
- **M** `Tests/.../DemoDataSeederTests.cs, ApplicationServiceTests.cs` - Follow the visibility change.
- **M** `README.md` - "Entity Framework Core 10".
- **M** `Troy Web Property Manager TODO.md` - Finished items ticked.

## Phase 7: Bonus features

### Step 49: Add Property Manager Notes

**Assessment:** Bonus 3 (notes visible and editable only by managers, never rendered or returned to an applicant).

**Files:**

- **A** `Models/ManagerNote.cs`, **M** `Data/ApplicationDbContext.cs`, **A** `Data/Migrations/20260930182537_AddManagerNotes.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs` - A separate table, one row per application, with a Version used as a concurrency token so two managers can't overwrite each other.
- **M** `Services/ApplicationService.cs` - The only way to read or save notes, with the manager check inside the service.
- **A** `ViewComponents/ManagerNotesViewComponent.cs`, **A** `Views/Shared/Components/ManagerNotes/Default.cshtml` - The panel loads its own data, so notes never go into the editor view model that applicants receive.
- **A** `ViewModels/ManagerNotesViewModel.cs`, **A** `Views/Applications/_ManagerNotesForm.cshtml` - The edit modal.
- **M** `Controllers/ApplicationsController.cs, Views/Applications/Edit.cshtml` - Manager-only actions; show the panel.
- **M** `wwwroot/css/site.css` - Notes keep their line breaks.
- **A** `Docs/Bonus-PropertyManagerNotes.md` - How it works and why.
- **M** `Tests/.../ApplicationServiceTests.cs, ViewModelValidationTests.cs` - Includes a test that the note text appears nowhere in anything returned to an applicant.

### Step 50: Manager Review Queue

**Assessment:** Bonus 2 (claim a submitted application - Under Review - and release it back to the queue).

**Files:**

- **M** `Models/ApplicationStatus.cs, Rules/ApplicationWorkflow.cs` - New UnderReview status; Submitted -> Under Review -> outcome; release goes back to Submitted.
- **M** `Models/RentalApplication.cs, Data/ApplicationDbContext.cs`, **A** `Data/Migrations/20260930185846_ReviewQueue.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs` - ReviewerUser and ReviewClaimed columns (a concurrency token too, against claim/release/claim races), a CHECK that only Under Review has a claim, and indexes for the queue.
- **M** `Services/ApplicationService.cs, Controllers/ApplicationsController.cs` - Claim, release, and review only by the manager who holds the claim.
- **A** `ViewModels/ReviewQueueViewModel.cs`, **A** `Views/Applications/Queue.cshtml` - My claims / Waiting / Claimed by others.
- **M** `ViewModels/ApplicationEditorViewModel.cs, Views/Applications/Edit.cshtml, Views/Applications/Index.cshtml, Views/Shared/Components/ApplicationHistory/Default.cshtml, Pages/Shared/_Layout.cshtml` - Claim/Release/Review buttons, the claim line, the menu link.
- **M** `Data/DemoDataSeeder.cs` - Seeded reviews go through the queue.
- **A** `Docs/Bonus-ReviewQueue.md`
- **M** `Tests/.../CheckConstraintTests.cs, DemoDataSeederTests.cs, ApplicationWorkflowTests.cs, ApplicationServiceTests.cs, TestDatabase.cs`

### Step 51: Allow save application even if invalid

**Assessment:** Bonus 4 (save a section even when invalid; the Summary lists everything blocking submission; rules defined once per section; errors on their fields).

**Files:**

- **A** `Rules/SectionValidator.cs` - Runs a section's DataAnnotations (and its cross-field rule) and returns every error keyed to its field - the one definition of each section's rules.
- **M** `Rules/SubmissionRules.cs` - Saved-but-invalid sections now count as blockers.
- **M** `Services/ApplicationService.cs, Services/ServiceResult.cs` - Save first, then report what's still wrong (Unresolved). Only text too long for its column stops a save.
- **M** `Models/ApplicantInformation.cs, Models/Residence.cs, Data/ApplicationDbContext.cs`, **A** `Data/Migrations/20260930194904_AllowSectionsSavedWithErrors.cs (+ .Designer.cs)`, **M** `Data/Migrations/ApplicationDbContextModelSnapshot.cs` - Residence dates become nullable and CK_Residence_Dates is dropped, because invalid data now has to be storable.
- **M** `ViewModels/ApplicantInformationViewModel.cs, ApplicationEditorViewModel.cs, ResidenceViewModel.cs`
- **M** `Controllers/ApplicationsController.cs, Views/Applications/Edit.cshtml, _ApplicantInformation.cshtml, _ResidenceForm.cshtml, _ResidenceHistory.cshtml, _Summary.cshtml` - Errors under each field and residence row; a Next button to move on anyway.
- **M** `wwwroot/js/site.js` - data-save-invalid: the residence modal saves without the browser check first.
- **A** `Docs/Bonus-AllowSave.md`
- **A** `Tests/.../Rules/SectionValidatorTests.cs`, **M** `CheckConstraintTests.cs, SubmissionRulesTests.cs, ApplicationServiceTests.cs`

### Step 52: allow multiple applicants

**Assessment:** Bonus 5 (several applicants per application; ownership for all of them; saves to different sections don't interfere; a second save to the same section is rejected as stale).

**Files:**

- **A** `Models/ApplicationApplicant.cs`, **M** `Models/Applicant.cs, Models/RentalApplication.cs, Data/ApplicationDbContext.cs`, **A** `Data/Migrations/20260930201707_MultipleApplicants.cs (+ .Designer.cs)`, **M** `Data/Migrations/ApplicationDbContextModelSnapshot.cs` - Join table (existing starters back-filled by SQL), plus a version GUID per section.
- **M** `Services/ApplicationService.cs` - Visible() checks membership; each section save is a compare-and-swap on that section's version in the same transaction, so a stale save is refused instead of silently overwriting; add / remove / leave.
- **M** `Services/ServiceResult.cs` - Carries the new version back to the page.
- **A** `ViewModels/ApplicationApplicantViewModel.cs`, **M** `ApplicationEditorViewModel.cs, ResidenceViewModel.cs`
- **A** `Views/Applications/_Applicants.cshtml, _AddApplicantForm.cshtml`, **M** `Edit.cshtml, _ResidenceForm.cshtml, _ResidenceHistory.cshtml` - The applicants panel and modals; hidden version fields.
- **M** `Controllers/AppController.cs (ModalRedirect), Controllers/ApplicationsController.cs, wwwroot/js/site.js` - After leaving an application the modal sends you to your list.
- **M** `Data/DemoDataSeeder.cs` - Some seeded applications get a second applicant.
- **A** `Docs/Bonus-MultipleApplicants.md`
- **A** `Tests/.../MultipleApplicantsTests.cs`, **M** `DemoDataSeederTests.cs, ApplicationServiceTests.cs, TestDatabase.cs`

### Step 53: Fix issues with multiple applicants

**Assessment:** Functional 2.d (a lease that "covers today" - whose today?); Bonus 5.

**Files:**

- **A** `Services/BusinessClock.cs`, **M** `appsettings.json (BusinessTimeZone)`, **M** `Program.cs` - "Now" and "today" in the business's time zone, not the server's, for lease dates and availability. Registered once; the seeder uses it too.
- **M** `Services/ApplicationService.cs, Services/PropertyService.cs` - Use the clock.
- **M** `Controllers/ApplicationsController.cs` - Remove-applicant message when the email is already gone.
- **M** `Views/Properties/_PropertyCard.cshtml, Views/Units/Index.cshtml` - Rent shown with cents.
- **M** `Data/DemoDataSeeder.cs, Docs/Bonus-MultipleApplicants.md`
- **A** `Tests/.../BusinessClockTests.cs`, **M** `Tests/.../ApplicationServiceTests.cs`

### Step 54: Sort applications via SQL

**Assessment:** Bonus 1 (paging and sorting in the database; a reusable grid view component driven by a JSON endpoint returning the page and the filtered total; documented with OpenAPI).

**Files:**

- **A** `ViewModels/ApplicationListQuery.cs, ViewModels/PagedResult.cs` - Filters, sort and page in; page of rows + total out.
- **M** `Services/ApplicationService.cs` - COUNT, then ORDER BY ... OFFSET/FETCH, with the id as a tiebreaker so paging is stable.
- **A** `Controllers/Api/ApplicationsApiController.cs` - GET /api/applications, same role and visibility rules.
- **A** `ViewComponents/DataGridViewComponent.cs`, **A** `ViewModels/DataGridViewModel.cs`, **A** `Views/Shared/Components/DataGrid/Default.cshtml`, **A** `wwwroot/js/grid.js` - The reusable grid: columns are configured in the page, rows come from the endpoint.
- **M** `Views/Applications/Index.cshtml, Controllers/ApplicationsController.cs, ViewModels/ApplicationListViewModel.cs, ViewModels/ApplicationListItemViewModel.cs` - The list now uses the grid.
- **M** `Program.cs` - Enums as names in JSON; OpenAPI document (Development); 401/403 instead of login redirects for /api.
- **M** `Troy Web Property Manager.csproj` - Microsoft.AspNetCore.OpenApi package; XML docs feed the OpenAPI descriptions.
- **M** `Services/CurrentUser.cs, Controllers/AppController.cs` - CurrentUser.From(ClaimsPrincipal), shared with the API.
- **M** `Pages/Shared/_Layout.cshtml, wwwroot/css/site.css` - Load grid.js; grid styles.
- **A** `Docs/Bonus-ApplicationGrid.md`
- **M** `Tests/.../ApplicationServiceTests.cs, MultipleApplicantsTests.cs, DemoDataSeederTests.cs` - Paging/sorting tests, including that the paging happens in SQL.

### Step 55: Update README.md

**Assessment:** Deliverables.

**Files:**

- **M** `README.md` - Full setup: prerequisites, first run, demo accounts, configuration (connection string, time zone, SendGrid), tests, API/OpenAPI, migrations, project layout.

## Phase 8: UI polish and UI tests

### Step 56: ui fixes and email

**Assessment:** Technical 1.c (styling); Functional 1.a.

**Files:**

- **M** `Pages/Shared/_Layout.cshtml` - The scoped-CSS link pointed at "Troy_Web_Property_Manager.styles.css", but the bundle is named after the assembly with spaces, so the layout's styles never loaded.
- **M** `Services/EmailSender.cs` - The Development log shows the email decoded, so the confirmation link can be copied without `&amp;` breaking it.

### Step 57: update Styles for UI

**Assessment:** Technical 1.c ("structure and style the web application to your liking using best practices").

**Files:**

- **M** `wwwroot/css/site.css` - A light blue palette set once through Bootstrap's CSS variables, so every component follows it.
- **M** `Pages/Shared/_Layout.cshtml, Pages/Shared/_Layout.cshtml.css, Pages/Shared/_LoginPartial.cshtml` - Themed navbar and footer; the current page is highlighted (aria-current); old hard-coded colours removed.
- **M** `Pages/Index.cshtml` - Welcome panel.
- **M** `Areas/Identity/Pages/Account/Login.cshtml, Register.cshtml, Manage/Index.cshtml, Manage/ChangePassword.cshtml` - Account forms in a narrower column instead of full page width.

### Step 58: Add Font Awesome

**Assessment:** Technical 1.c.

**Files:**

- **A** `wwwroot/lib/fontawesome (css/fontawesome.min.css, css/solid.min.css, webfonts/fa-solid-900.woff2, LICENSE.txt)` - Font Awesome Free, solid style only, kept locally like Bootstrap.
- **M** `Pages/Shared/_Layout.cshtml, _LoginPartial.cshtml, Pages/Index.cshtml, Pages/Privacy.cshtml, Areas/Identity/Pages/Account/Login, Logout, Register, Manage/Index, Manage/ChangePassword (.cshtml), Views/Applications/Edit, Index, Queue, _AddApplicantForm, _ApplicantInformation, _Applicants, _ManagerNotesForm, _ResidenceForm, _ResidenceHistory, _ReviewForm, _Summary (.cshtml), Views/Properties/Index, _PropertyCard, _PropertyForm, _UnitForm (.cshtml), Views/Shared/Components/ApplicationHistory/Default.cshtml, Views/Shared/Components/ManagerNotes/Default.cshtml, Views/Units/Index.cshtml` - Icons on menus, headings, buttons and messages. All aria-hidden, and every button keeps its text, so screen readers hear the same as before.
- **M** `wwwroot/js/grid.js, wwwroot/css/site.css` - Sort arrows in the grid become icons.

### Step 59: Add sorting to units

**Assessment:** Functional 2.b (browse available units).

**Files:**

- **M** `Services/PropertyService.cs` - Sort by property, bedrooms, rent or type in SQL, with property/unit as tiebreakers.
- **M** `Controllers/UnitsController.cs, ViewModels/AvailableUnitsPageViewModel.cs, Views/Units/Index.cshtml` - Sortable column headers (links, so no JavaScript; aria-sort for screen readers) that keep the filters, and a filter form that keeps the sort.
- **M** `wwwroot/css/site.css` - Header links styled like the grid's.
- **M** `Tests/.../PropertyServiceTests.cs` - Every column, both directions, with ties.

### Step 60: Add UI Tests

**Assessment:** every functional requirement and bonus item checked end to end in a browser; Technical 2.c.

**Files:**

- **A** `Tests/Troy Web Property Manager.UITests/Troy Web Property Manager.UITests.csproj` - xUnit + Selenium WebDriver.
- **A** `.../Infrastructure/AppFactory.cs, UiFixture.cs` - Runs the real app in-process on Kestrel against a throwaway LocalDB database (created, migrated and seeded by Program.cs, dropped afterwards), with a guard that refuses to run against any other database.
- **A** `.../Infrastructure/CapturingEmailSender.cs` - Keeps emails in memory so sign-up tests can follow the link.
- **A** `.../Infrastructure/Browser.cs` - Headless Chrome wrapper: waits, modals, requests with the browser's cookies (for exact 403/404 checks), screenshots when a wait times out.
- **A** `.../Pages/AccountPages.cs, ApplicationPage.cs, ListPages.cs, PropertiesPage.cs` - One page object per page.
- **A** `.../Workflows/Workflows.cs` - Composable steps (log in, register and confirm, apply, complete sections, submit, claim, review, approve) that tests chain together.
- **A** `.../SmokeTests.cs and .../Tests/AccountTests.cs, SecurityTests.cs, PropertyTests.cs, UnitTests.cs, ApplicationEditorTests.cs, ReviewTests.cs, QueueAndNotesTests.cs, ApplicationListTests.cs, MultipleApplicantsTests.cs, SeedAndNavigationTests.cs` - 124 tests: both roles, security (wrong role, other people's data, tampered posts, missing antiforgery token) and the error cases.
- **M** `Troy Web Property Manager.slnx` - Adds the UI test project.
- **M** `README.md` - How to run the UI tests and how they're structured.
- **M** `Views/Applications/_ResidenceForm.cshtml` - Bug the UI tests found: jQuery validation cancelled the residence modal's submit when a required field was blank, contradicting Bonus 4 ("saves even when invalid"). formnovalidate on Save fixes it.

## Phase 9: Logging

### Step 61: Add Logging

**Assessment:** Considerations ("clean, robust, and ready for a production environment"); the TODO list's "Logging" item.

**Why:** Until now only the Identity pages, EmailSender and the demo seeding logged anything - the services, where every business action happens, were silent. In production you need to answer "who changed this application, and why was that refused?" from the logs. Logging follows the pattern already in the code: `ILogger<T>` injected through the constructor, structured message templates with named placeholders ({ApplicationId}, {UserId}), Information for normal actions, Warning for the unusual, Error with the exception for failures. Only ids are logged - never an applicant's details or the managers' private notes (Bonus 3).

**Files:**

- **M** `Services/ApplicationService.cs` - Takes `ILogger<ApplicationService>` as an optional constructor parameter, the same way it already takes BusinessClock (tests that don't pass one get a logger that discards everything; DI supplies the real one). A Logged() helper records how each action turned out: done and ordinary refusals (a rule said no, with the reason) are Information; losing to someone else's change, or asking for an application that doesn't exist or isn't yours (which can mean someone is trying ids), is a Warning. Specific messages for starting an application, adding/removing applicants, reviews and the lease an approval creates (with its dates). Editing an application that can't be edited any more - an old tab or a hand-made post (Functional 4.d) - is a Warning.
- **M** `Services/PropertyService.cs` - Same optional logger: properties and units added, updated and removed, and every refusal with its reason. Posting an inactive unit type is a Warning, since the form never offers one (Functional 2.c is enforced on the server).
- **M** `Program.cs` - Startup logs the migrations it applied and the statuses, unit types and roles it added (Technical 2.b), next to the existing demo-seed message.
- **M** `Services/EmailSender.cs` - A success line to go with the existing error lines.
- **A** `Tests/Troy Web Property Manager.Tests/TestLogger.cs` - An `ILogger<T>` that records what was logged, so tests can check it.
- **M** `Tests/.../Services/ApplicationServiceTests.cs` - Services get the TestLogger. New tests: a submit is logged with who and which application; a review and its lease are logged; warnings for editing a submitted application and for a stale save; a refusal is logged with its reason; the managers' note text never appears in the log.
- **M** `Tests/.../Services/PropertyServiceTests.cs` - Adding and removing a unit are logged; posting an inactive type logs a warning.

## Phase 10: Documentation

### Step 62: Create development notes.md

**Assessment:** Considerations ("explainable design decisions"); Deliverables.

**Files:**

- **A** `Docs/development notes.md` - This file: every check-in, oldest first, with what changed, why, and which part of the PDF it covers.

## Phase 11: Final review hardening

A last review of the application workflow looked for rules that the page enforces but the server didn't, and for
check-then-write races that only show up under concurrent requests. Each fix keeps to the pattern from Phase 6: the
service enforces the rule itself (never trusting the page or the controller), and a race the database stops becomes a
friendly "reload and try again" message instead of a 500.

### Step 63: Submit from Summary Only

**Assessment:** Functional 4.b.ii (Submit from the read-only Summary); Considerations (security).

**Why:** The Submit button is only rendered on the Summary, but the Edit POST accepted `command=submit` from any
section. A hand-made or stale post could submit straight from section 1 or 2, skipping the Summary the applicant is
meant to check first.

**Files:**

- **M** `Controllers/ApplicationsController.cs` - The `submit` command returns 400 unless the posted section is the Summary.

### Step 64: Validate review outcomes against the defined enum values.

**Assessment:** Functional 5.a (Approve / Return / Deny); Considerations (security).

**Why:** Model binding happily turns `Outcome=99` into a `ReviewOutcome` that isn't one of the three. `[Required]` only
caught a missing value, so an undefined outcome reached the review logic.

**Files:**

- **M** `ViewModels/ReviewViewModel.cs` - `[EnumDataType]` on Outcome, so the form shows "Choose a valid outcome."
- **M** `Services/ApplicationService.cs` - ReviewAsync checks the outcome again, since the service doesn't trust its caller.

### Step 65: Make the submit lease check atomic with submission

**Assessment:** Functional 4.e (no submission for a leased unit); 2.d (one active lease); Considerations (concurrency).

**Why:** Submit checked the unit's lease and then saved in a separate step, so a manager's approval of another
application could lease the unit in between. The lease check now runs in the same Serializable transaction as the
status change and section-version swaps. Approval already uses Serializable (Step 25), so the two
can't both get past the lease check; if they collide, SQL Server picks one as a deadlock victim and the applicant is
asked to retry.

**Files:**

- **M** `Services/ApplicationService.cs` - SaveSectionsAsync becomes a wrapper around a new SaveSectionsWithCheckAsync, which takes an isolation level and a check to run inside the transaction before anything is written. SubmitAsync uses it with Serializable and turns a deadlock (SQL error 1205) into a stale result.

### Step 66: Protect "one open application per applicant per unit" rule for co-applicants

**Assessment:** Functional 3 (rental application); Bonus 5 (multiple applicants); Considerations (concurrency).

**Why:** The filtered unique index only covers the applicant who *started* an application. Since Bonus 5, someone can
also be *added* to one, so "already on an open application for this unit" is a check in code that the index can't
back up. Starting an application and being added to another one at the same moment could both pass it.

**Files:**

- **M** `Services/ApplicationService.cs` - StartOnceAsync and AddApplicantAsync both run their open-application check and insert in a Serializable transaction, so neither can slip past the other. A deadlock becomes a stale result in both.

### Step 67: Normalize or reject unknown section values

**Assessment:** Functional 4.b (one section at a time); Considerations (security).

**Why:** `?section=99` bound to an `ApplicationSection` that isn't one of the three, and the editor tried to show it.

**Files:**

- **M** `Controllers/ApplicationsController.cs` - Edit GET and POST return 400 for a section that isn't defined or didn't bind.
- **M** `Services/ApplicationService.cs` - GetEditorAsync falls back to the Summary for an undefined section, in case another caller passes one.

### Step 68: Review follow-ups

**Assessment:** Bonus 5; Functional 4.b.ii, 4.e; Technical 2.c (unit tests); Considerations (concurrency).

**Why:** A review of Steps 63-67 found gaps:

- Under Serializable, a double-click on Apply usually ends in a deadlock, not the unique-key error StartAsync was
  written to retry, so the applicant got "reload and try again" instead of their application.
- A retry that lost a second time threw from inside the catch block and became a 500.
- Submit stopped at section errors before checking the lease, so the applicant only heard the unit was leased after
  fixing everything else - unlike the Summary, which lists both.
- None of Steps 63-67 had tests, and SQLite (used by the unit tests) never produces SQL Server deadlocks.

**Files:**

- **M** `Services/ApplicationService.cs` - StartAsync retries up to three times on a deadlock or unique-key error (MaxStartAttempts) and only returns a stale result if every attempt loses. SubmitAsync builds the whole blocker list - section errors and the lease - inside the Serializable transaction, the same list the Summary shows. GetEditorAsync's nested ternary for picking the section became an if/else.
- **A** `Tests/Troy Web Property Manager.Tests/FakeSqlErrors.cs` - Builds a real SqlException with a given error number (through SqlClient's internal factory, since it has no public constructor) and an interceptor that fails SaveChanges with a sequence of them.
- **A** `Tests/.../Controllers/ApplicationsControllerTests.cs` - Submit from section 1 or 2, an undefined section on GET and POST, and a section that didn't bind all return 400.
- **M** `Tests/.../Services/ApplicationServiceTests.cs` - Start retries after a deadlock, and after a unique-key error then a deadlock; losing every attempt is stale, not an exception. A deadlocked Submit rolls back the status and section versions so the same Summary can submit again. Submit reports section errors and the lease together. An undefined review outcome changes nothing. An undefined section opens the Summary.
- **M** `Tests/.../Services/MultipleApplicantsTests.cs` - Adding someone who was added (not started) on another open application for the unit is rejected; a deadlocked add adds no one.
- **M** `Tests/.../ViewModels/ViewModelValidationTests.cs` - An undefined review outcome is invalid.

### Step 69: Fix adding a unit while its property is removed

**Assessment:** Functional 2.b (manage units); Considerations (concurrency, error handling).

**Why:** SaveUnitAsync checks the property exists and then inserts the unit. If another manager removes the property
in between, the foreign key stops the insert, but nothing caught that error, so the modal got a 500. It was the one
write path without a friendly fallback for a race.

**Files:**

- **M** `Services/PropertyService.cs` - SaveUnitAsync catches the foreign-key error (SQL error 547) when adding a unit, logs a warning and returns not found - the same answer as when the property is already gone at the start.
- **M** `Services/SqlErrors.cs` - IsReferenceConflict's comment now covers an insert whose parent row is gone, not just a delete.
- **M** `Tests/.../FakeSqlErrors.cs` - A ReferenceConflict (547) constant.
- **M** `Tests/.../Services/PropertyServiceTests.cs` - Adding a unit whose property is removed during the save is not found and saves nothing.

### Step 70: Trim input before the too-long check

**Assessment:** Functional 4.b.i, 4.c; Bonus 4 (save even when invalid).

**Why:** Sections save even with errors, except text too long for its column (Step 51). That length check ran on the
posted text before it was trimmed, so 50 characters plus a trailing space was refused as too long even though the
trimmed value that gets stored would fit.

**Files:**

- **M** `Services/ApplicationService.cs` - SaveApplicantInformationAsync and SaveResidenceAsync trim the posted text first, then check the length and save those trimmed values, so the check and the stored value always match.
- **M** `Tests/.../Services/ApplicationServiceTests.cs` - Applicant information and a residence that fit once trimmed are saved, trimmed.

### Step 71: Retry Add applicant after losing a race

**Assessment:** Bonus 5 (multiple applicants); Considerations (concurrency).

**Why:** Step 66 put Add applicant in a Serializable transaction, so adding someone at the same moment as another
request (often their own Apply) usually ends in a deadlock. Unlike Start (Step 68), Add applicant didn't retry, so the
user got "reload and try again". Start's retry log also always said the user applied "twice at once", even when the
other request was someone adding them to an application.

**Files:**

- **M** `Services/ApplicationService.cs` - AddApplicantAsync becomes a retry loop around AddApplicantOnceAsync, like StartAsync. A deadlock or unique-key error is retried up to three times. The retry sees what the other request did, so someone added at the same moment now gets "They're already on this application" instead of a stale result. Only if every attempt loses is it stale. MaxStartAttempts is renamed MaxRaceAttempts since both use it. Start's retry log now says the request lost a race with another request.
- **M** `Tests/.../Services/MultipleApplicantsTests.cs` - "A deadlocked add adds no one" becomes "a deadlocked add retries and adds them". New: a unique-key race retries and adds them; losing every attempt is stale and adds no one.

### Step 72: Show a message when an Identity page can't send email

**Assessment:** Functional 1.a (sign up and log in); Considerations (error handling).

**Why:** Outside Development with no SendGrid key, or whenever SendGrid fails, EmailSender throws. Register catches
that and shows the confirmation link, but Forgot password, Resend email confirmation and Manage → Email come from the
default Identity UI and don't catch anything, so they ended on the 500 error page.

**Files:**

- **A** `Services/EmailSendException.cs` - Its own type for "the email didn't go", so it can be told apart from a bug. Still an InvalidOperationException.
- **M** `Services/EmailSender.cs` - Throws EmailSendException for no key, no from address, a refusal, and (new) SendGrid being unreachable.
- **A** `Areas/Identity/EmailFailureFilter.cs`, **M** `Program.cs` - A page filter on the whole Identity area: the form is redrawn with "We couldn't send the email right now" (the Manage pages redirect back with an error status message instead).
- **M** `Tests/.../UITests/Infrastructure/CapturingEmailSender.cs` - Simulated failures throw EmailSendException.
- **M** `Tests/.../UITests/Tests/AccountTests.cs` - Forgot password, Resend confirmation and Change email show the message when the send fails.

### Step 73: Store timestamps in UTC

**Assessment:** Functional 5.c (status history); Considerations (data integrity).

**Why:** Timestamps were stored on the business's wall clock, and the hour the clocks go back each autumn happens
twice, so a history row written in the second 1:30 AM sorted before one from the first. UTC never repeats. Lease dates
stay calendar dates on the business's calendar.

**Files:**

- **M** `Services/BusinessClock.cs` - UtcNow (what gets stored), Today (the business's date, unchanged), ToBusinessTime (UTC to the business's zone, for display) and ToUtc (for the seeder). Now is gone, so local time can't be stored by mistake.
- **M** `Services/ApplicationService.cs` - Stores UtcNow. The editor, queue, history, notes and list convert to the business's zone.
- **M** `ViewModels/*` - Display times are DateTimeOffset in the business's zone, so the list API now sends the offset (2026-10-03T14:05:00-04:00).
- **M** `Data/DemoDataSeeder.cs`, **M** `Program.cs` - The seeder takes the clock, plans its timeline in local time (so leases start on the business's date) and stores each timestamp in UTC.
- **A** `Data/Migrations/..._UtcTimestamps.cs` - Converts existing rows from Eastern time to UTC (Down converts back). No schema change.
- **M** `wwwroot/js/grid.js, Views/Shared/Components/ApplicationHistory/Default.cshtml, Docs/Bonus-*.md` - Comments and column descriptions.
- **M** `Tests/.../BusinessClockTests.cs, ApplicationServiceTests.cs, DemoDataSeederTests.cs` - UTC storage, display offsets, the repeated autumn hour staying in order, the skipped spring hour.

### Step 74: Keep the page counter off the Identity pages

**Assessment:** Considerations (security, privacy).

**Why:** GoatCounter's script sends each page's path with its query string. The Identity pages carry secrets there -
the password reset token (ResetPassword?code=...), confirmation tokens and user ids (ConfirmEmail), and email
addresses (RegisterConfirmation?email=...) - so the counter was sending them to a third party.

**Files:**

- **M** `Pages/Shared/_GoatCounter.cshtml` - Renders nothing on any page in the Identity area.
- **M** `Tests/.../UITests/Infrastructure/AppFactory.cs, UiFixture.cs` - An app fixture with the counter switched on (a .invalid URL).
- **A** `Tests/.../UITests/Tests/TelemetryTests.cs` - Home and Privacy have the counter; the Identity pages don't. Reads the HTML with HttpClient, so the script never runs.

### Step 75: Pre-fill section 1 for the starter only

**Assessment:** Bonus 5 (multiple applicants); Considerations (privacy).

**Why:** Until section 1 was saved on an application, it was pre-filled from the starter's profile for everyone on the
application. That profile holds the name, phone, email and address the starter used on their other applications, so
an applicant added to a new draft could read them before the starter had shared anything on this one.

**Files:**

- **M** `Services/ApplicationService.cs` - GetEditorAsync pre-fills an unsaved section 1 only when the viewer is the starter; anyone else sees it empty. Once it's saved on the application, everyone sees the saved copy as before.
- **M** `Tests/.../Services/MultipleApplicantsTests.cs` - The starter gets the pre-fill, an added applicant doesn't, and both see section 1 once it's saved.
- **M** `Docs/Bonus-MultipleApplicants.md`

### Step 76: Applicant remove race condition

**Assessment:** Bonus 5 (multiple applicants); Considerations (security, concurrency).

**Why:** Every application write checks that the user is on the application, then saves. If the starter removed an
applicant between those two moments, the removed applicant's request had already passed the check and could still
save section 1, residences, a submit or a withdraw on an application they no longer had access to.

**Files:**

- **M** `Models/RentalApplication.cs`, **M** `Data/ApplicationDbContext.cs` - ApplicantAccessVersion, a GUID used as a concurrency token on the application row.
- **A** `Data/Migrations/20261003223033_ApplicantAccessConcurrency.cs (+ .Designer.cs)`, **M** `ApplicationDbContextModelSnapshot.cs` - Adds the column.
- **M** `Services/ApplicationService.cs` - RemoveApplicantAsync sets a new ApplicantAccessVersion in the same save that removes the membership. GuardStatus already makes every write update the application row, so that update now also fails when access was revoked after the check. The write gets the usual "reload and try again" conflict, and its section version swaps and child changes roll back with it.
- **M** `Tests/.../Services/MultipleApplicantsTests.cs` - For each write (section 1, history, add/edit/delete residence, submit, add/remove applicant, withdraw): an applicant removed after the ownership check gets a conflict, and nothing on the application changes except the access version.

## Phase 12: Separate Development and Production

Until now every environment behaved like a developer's machine: LocalDB was the default database, startup migrated
whatever database it was pointed at, anyone could sign up as a Property Manager, and the registration page could show
the confirmation link instead of making people use the email. This phase keeps all of that for Development (so a fresh
clone still just works) and turns it off everywhere else. Each shortcut checks "is this Development?", never "is this
Production?", so Staging or any other environment gets the safe behaviour.

### Step 77: Separate Development and Production environments

**Assessment:** Technical 2.a, 2.b; Considerations (production-ready code, security); Deliverables (README).

**Why:** A deployed copy got Development's conveniences: a LocalDB connection string from appsettings.json, automatic
migrations against the real database, Property Manager in the public role picker, an on-page confirmation link (even
with `Registration:ShowConfirmationLink` left on by mistake), the OpenAPI document and the "Development Mode" text on
the error page.

**Files:**

- **M** `appsettings.json` - Safe defaults only: no connection string, ShowConfirmationLink off, telemetry empty, DetailedErrors off.
- **M** `appsettings.Development.json` - LocalDB, ShowConfirmationLink on.
- **A** `appsettings.Production.json` - ShowConfirmationLink off; the startup ping and GoatCounter URLs.
- **M** `Properties/launchSettings.json` - http/https set DOTNET_ENVIRONMENT as well; new Production profile.
- **M** `Program.cs` - A missing connection string is a clear startup error. The database developer page, OpenAPI and automatic migrations are Development only; anywhere else startup refuses a database with pending migrations. The startup ping only runs in Development and Production.
- **M** `Areas/Identity/Pages/Account/Register.cshtml, Register.cshtml.cs` - RegistrationRoles: both roles in Development, Applicant only elsewhere, for the radio buttons and for checking the post (so a tampered post can't pick Property Manager). The confirmation link only goes into TempData in Development.
- **M** `Areas/Identity/Pages/Account/RegisterConfirmation.cshtml, .cshtml.cs` - Ignores any link outside Development. When the email couldn't be sent it says the account isn't confirmed and offers Resend confirmation email.
- **M** `Pages/Error.cshtml` - The "Development Mode" instructions only show in Development.
- **M** `Pages/Shared/_GoatCounter.cshtml` - Only in Development and Production; allow_local (count localhost) only in Development.
- **M** `Services/EmailSender.cs, Data/ApplicationDbContext.cs, Docs/Bonus-*.md, Docs/Email Setup.txt, README.md` - Comments and docs: migrations are a deployment step outside Development, Production settings come from environment variables.
- **M** `Tests/.../UITests/Infrastructure/AppFactory.cs, UiFixture.cs` - The app's environment is a parameter. Fixtures that aren't Development deploy the migrations before starting the app. New Production, Production-without-email and Development-telemetry fixtures; the telemetry fixture runs as Production.
- **A** `Tests/.../UITests/Tests/ProductionEnvironmentTests.cs` - Registration needs the emailed link even with ShowConfirmationLink on (whether the send works or fails); Property Manager is rejected, tampered posts included; only roles and lookups are seeded; /openapi and /ApplyMigrations are 404; no developer text on /Error; undeployed migrations stop startup and create no database. No SendGrid key: no link and no login. Development telemetry: counter with allow_local, never on Identity pages.
- **M** `Tests/.../UITests/Tests/TelemetryTests.cs` - No allow_local in Production.
- **A** `Tests/.../Controllers/RegisterConfirmationTests.cs` - The link only shows in Development.
- **A** `Tests/.../Services/EmailSenderTests.cs` - With no key, Development logs the email; anywhere else it throws and never logs the token.

### Step 78: Environment review fixes

**Assessment:** Functional 1.a.i (role on sign-up); Considerations (security, production-ready code).

**Why:** A review of Step 77 found four gaps. Development still had the production health check and page counter
URLs, so every local run pinged production's health check and counted as a production visit. With sign-up limited to
Applicant there was no way at all to get a Property Manager in Production. The only role button wasn't selected, so
applicants had to click their one choice or get "Please select a valid role". A comment still said the server checks
against AppRoles.All.

**Files:**

- **M** `appsettings.Development.json` - Telemetry URLs emptied (set them in user secrets to try telemetry locally). Production keeps its values.
- **A** `Data/ManagerBootstrapper.cs`, **M** `Program.cs`, **M** `appsettings.json` - Bootstrap:ManagerEmail. On startup, in any environment, that account becomes a Property Manager and stops being an Applicant, but only once its email is confirmed, so nobody can claim the role by registering the address first. It does nothing once the account is a manager.
- **M** `Areas/Identity/Pages/Account/Register.cshtml.cs` - When Applicant is the only choice, the GET selects it.
- **M** `Areas/Identity/Pages/Account/Register.cshtml` - The comment.
- **M** `README.md` - Telemetry is Production-only by default; how to make the first Property Manager.
- **A** `Tests/.../TestWebHostEnvironment.cs`, **M** `RegisterConfirmationTests.cs, EmailSenderTests.cs` - One shared fake environment instead of a copy per test class.
- **A** `Tests/.../Controllers/RegisterTests.cs` - Roles per environment (Development, Production, Staging) and the preselection.
- **A** `Tests/.../Data/ManagerBootstrapperTests.cs` - A confirmed applicant becomes manager only; an existing manager, an unconfirmed account, no setting and an unknown email change nothing.
- **M** `Tests/.../UITests/Infrastructure/UiFixture.cs`, **A** `Tests/.../UITests/Tests/StagingEnvironmentTests.cs` - A Staging fixture with ShowConfirmationLink and the counter switched on: sign-up is Applicant only and preselected, no confirmation link, no demo data, no counter.
- **M** `Tests/.../UITests/Tests/ProductionEnvironmentTests.cs` - No connection string stops startup with a clear error.

