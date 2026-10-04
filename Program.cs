using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Troy_Web_Property_Manager.Areas.Identity;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for this environment.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            if (builder.Environment.IsDevelopment())
                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // ASP.NET Identity handles users and roles (Technical 2.a). The default Identity UI gives us sign-up,
            // log-in and log-out for free (1.a). I scaffolded Register into Areas/Identity so it can ask for a role (1.a.i).
            // AddRoles is what makes [Authorize(Roles = ...)] and User.IsInRole(...) work. Users and roles live in
            // the same database as everything else.
            // RequireConfirmedAccount means new users have to click the email link (sent by EmailSender) before logging in.
            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            // The JSON API (/api/...) uses the same auth cookie as the pages. A browser page that isn't signed in should
            // go to the login page, but an API call should just get the status code, so it can't be mistaken for data.
            // Everything else keeps Identity's default (which already answers X-Requested-With requests with a 401).
            builder.Services.ConfigureApplicationCookie(options =>
            {
                var redirectToLogin = options.Events.OnRedirectToLogin;
                var redirectToAccessDenied = options.Events.OnRedirectToAccessDenied;
                options.Events.OnRedirectToLogin = context =>
                    ApiStatusOr(context, StatusCodes.Status401Unauthorized, redirectToLogin);
                options.Events.OnRedirectToAccessDenied = context =>
                    ApiStatusOr(context, StatusCodes.Status403Forbidden, redirectToAccessDenied);
            });

            builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
            builder.Services.AddSingleton<IEmailSender, EmailSender>();

            // I started from the Razor Pages template to get the Identity pages and layout, but the app itself is MVC:
            // controllers, view models, Razor views, partials and view components (Technical 1.a).
            // The default Identity pages that send email (Forgot password, Resend confirmation, Manage email) don't catch
            // a failed send; EmailFailureFilter shows a message instead of the error page.
            builder.Services.AddRazorPages(options =>
                options.Conventions.AddAreaFolderApplicationModelConvention("Identity", "/",
                    model => model.Filters.Add(new EmailFailureFilter())));

            builder.Services.AddControllersWithViews(options =>
            {
                // Check the antiforgery token on every MVC POST (Razor Pages already does). Doing it globally means we
                // can't forget [ValidateAntiForgeryToken] on an action. The form tag helper writes the token, and site.js
                // sends the whole FormData (token and all) when it posts a modal form.
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                // Only an explicit [Required] counts, so display-only view model properties don't trip validation.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            }).AddJsonOptions(options =>
            {
                // Enums go out as their names - "Submitted", not 2 - so API clients don't depend on the numbers.
                // Query strings already accept either.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            // The OpenAPI generator builds its schemas from these options (the minimal API ones), not MVC's above, so
            // they need the same converter for the document to say "Submitted" too.
            builder.Services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            // OpenAPI document for the JSON API, served at /openapi/v1.json in Development. It's built from the API
            // controllers' routes, [ProducesResponseType]s and XML doc comments (GenerateDocumentationFile in the
            // .csproj). The MVC pages use conventional routing, so they stay out of it.
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddOpenApi(options => options.AddOperationTransformer((operation, context, cancellationToken) =>
                {
                    foreach (var (code, response) in operation.Responses ?? [])
                    {
                        if (response.Content is null) continue;
                        // 401 and 403 come from the cookie handler (ApiStatusOr) with no body, whatever MVC would send.
                        if (code is "401" or "403") response.Content.Clear();
                        // [Produces] adds an application/json entry with no schema next to a 400's problem+json.
                        foreach (var type in response.Content.Where(c => c.Value.Schema is null).Select(c => c.Key).ToList())
                        {
                            response.Content.Remove(type);
                        }
                    }
                    return Task.CompletedTask;
                }).AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info.Title = "Troy Web Property Manager API";
                    document.Info.Description =
                        "JSON endpoints behind the site's data grids. Sign in on the site first: the API uses the same " +
                        "ASP.NET Core Identity cookie, and answers 401 without it.";

                    const string cookieScheme = "IdentityCookie";
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes[cookieScheme] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Cookie,
                        // The cookie handler's default name: ".AspNetCore.Identity.Application".
                        Name = CookieAuthenticationDefaults.CookiePrefix + IdentityConstants.ApplicationScheme,
                        Description = "The cookie set when you sign in on the site."
                    };
                    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(cookieScheme, document)] = [] }];
                    return Task.CompletedTask;
                }));
            }
            // "Now" and "today" in the business's time zone (BusinessTimeZone in appsettings), not the server's.
            builder.Services.AddSingleton(BusinessClock.FromConfiguration(builder.Configuration));
            // Scoped = one per request, sharing that request's DbContext (also scoped).
            builder.Services.AddScoped<PropertyService>();
            builder.Services.AddScoped<ApplicationService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

            // No default controller, so "/" stays the template's Razor Pages home page.
            app.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");

            // The API description is for developers, so like the migrations endpoint it's only served in Development.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // Validate the deployed schema; Development also applies pending migrations.
            await CreateDatabase(app);

            serverSideTelemetry(app);

            await app.RunAsync();
        }

        private static void serverSideTelemetry(WebApplication app)
        {
            var pingUrl = app.Configuration["Telemetry:StartupPingUrl"];
            if ((!app.Environment.IsProduction() && !app.Environment.IsDevelopment()) || string.IsNullOrWhiteSpace(pingUrl))
            {
                return;
            }

            app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    await http.PostAsync(pingUrl, new StringContent($"env={app.Environment.EnvironmentName}"));
                }
                catch (Exception ex)
                {
                    app.Logger.LogDebug(ex, "Startup ping failed.");
                }
            }));
        }

        /// <summary>
        /// For requests under /api, answers with <paramref name="statusCode"/> instead of redirecting to the login or
        /// access denied page. Anything else gets the cookie handler's usual redirect.
        /// </summary>
        private static Task ApiStatusOr(RedirectContext<CookieAuthenticationOptions> context, int statusCode,
            Func<RedirectContext<CookieAuthenticationOptions>, Task> otherwise)
        {
            if (!context.Request.Path.StartsWithSegments("/api")) return otherwise(context);
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Development creates and migrates the local database (Technical 2.b.i). Other environments require a
        /// deployed schema. Required roles/lookups are seeded everywhere; demo data only in Development (2.b.ii).
        /// </summary>
        private static async Task CreateDatabase(WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Development manages its local schema. Production migrations are a deployment step.
                var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
                if (app.Environment.IsDevelopment())
                    await dbContext.Database.MigrateAsync();
                else if (pending.Count > 0)
                    throw new InvalidOperationException("The database has pending migrations. Apply them with dotnet ef database update before starting outside Development.");
                if (pending.Count > 0)
                {
                    app.Logger.LogInformation("Applied {Count} database migrations: {Migrations}.", pending.Count, string.Join(", ", pending));
                }

                await AddRequiredDataToDatabaseAsync(scope, app.Logger);

                // The demo accounts all share a known password, so only seed them in Development.
                if (app.Environment.IsDevelopment())
                {
                    await SeedDataAsync(scope, app.Logger);
                }
            }
        }

        private static async Task AddRequiredDataToDatabaseAsync(IServiceScope scope, ILogger logger)
        {
            // Add required data to the database
            // Example: Add default roles, users, or any other necessary data
            // This method can be customized to add specific data to the database as needed

            await SeedRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(), logger);
            await SeedLookupsAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), logger);
            // Outside Development this is how the first Property Manager gets made (sign-up only creates applicants).
            await ManagerBootstrapper.PromoteAsync(scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
                scope.ServiceProvider.GetRequiredService<IConfiguration>()["Bootstrap:ManagerEmail"], logger);
        }

        /// <summary>
        /// Seeds the lookup tables (Technical 2.b.ii). This runs in every environment since the app doesn't work
        /// without them - the Status rows back the RentalApplications.Status FK, and the unit types fill the dropdown.
        /// Only adds rows that are missing.
        /// </summary>
        private static async Task SeedLookupsAsync(ApplicationDbContext dbContext, ILogger logger)
        {
            // The Status ids have to match the ApplicationStatus enum, so we insert them explicitly
            // (the id column is an identity, hence IDENTITY_INSERT). Only the missing ones get added.
            var existingStatusIds = await dbContext.Statuses.Select(s => s.Id).ToHashSetAsync();
            var missingStatuses = Enum.GetValues<ApplicationStatus>()
                .Where(s => !existingStatusIds.Contains((long)s))
                .Select(s => new Status { Id = (long)s, Name = s.ToString() })
                .ToList();

            if (missingStatuses.Count > 0)
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync();
                await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Status] ON");
                dbContext.Statuses.AddRange(missingStatuses);
                await dbContext.SaveChangesAsync();
                await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Status] OFF");
                await transaction.CommitAsync();
                logger.LogInformation("Added application statuses: {Statuses}.", string.Join(", ", missingStatuses.Select(s => s.Name)));
            }

            // Unit types are matched by name. An inactive type stays on units that already have it,
            // but nobody can pick it for a new one.
            (string Name, bool IsActive)[] unitTypes =
            [
                ("Apartment", true),
                ("Studio", true),
                ("Townhouse", true),
                ("Loft", false),
            ];

            var existingUnitTypes = await dbContext.UnitTypes.Select(t => t.Name).ToHashSetAsync();
            var missingUnitTypes = unitTypes.Where(t => !existingUnitTypes.Contains(t.Name)).ToList();
            dbContext.UnitTypes.AddRange(missingUnitTypes.Select(t => new UnitType { Name = t.Name, IsActive = t.IsActive }));
            await dbContext.SaveChangesAsync();
            if (missingUnitTypes.Count > 0)
            {
                logger.LogInformation("Added unit types: {UnitTypes}.", string.Join(", ", missingUnitTypes.Select(t => t.Name)));
            }
        }

        /// <summary>Creates the two Identity roles if they're not there yet.</summary>
        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
        {
            foreach (var role in AppRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Added role {Role}.", role);
                }
            }
        }

        /// <summary>
        /// Seeds demo data with Bogus (Technical 2.b.ii.1) - managers, applicants, properties, units, and applications
        /// in every status. The details are in <see cref="DemoDataSeeder"/>.
        /// </summary>
        private static async Task SeedDataAsync(IServiceScope scope, ILogger logger)
        {
            // Only runs on an empty database (no properties yet), so it never messes with real data.
            var seeded = await DemoDataSeeder.SeedAsync(
                    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                    scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
                    scope.ServiceProvider.GetRequiredService<BusinessClock>());

            if (seeded)
            {
                logger.LogInformation("Seeded demo data. Sign in as manager1@example.com or applicant1@example.com with password {Password}.",
                    DemoDataSeeder.Password);
            }
        }
    }
}
