using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
            builder.Services.AddTransient<IEmailSender, EmailSender>();

            builder.Services.AddRazorPages();

            builder.Services.AddControllersWithViews(options =>
            {
                // Validate antiforgery tokens on every MVC POST (Razor Pages already does this).
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                // Only explicit [Required] attributes count, so display-only view-model properties never fail validation.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            });
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

            // Ensure the database is created and apply any pending migrations
            CreateDatabase(app);

            app.Run();
        }

  
        private static void CreateDatabase(WebApplication app)
        {
            // NOTE: use this method instead of "dotnet ef database update" command

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Applies any pending migrations and creates the database if it doesn't exist
                dbContext.Database.Migrate();
                //dbContext.Database.EnsureCreated();

                AddRequiredDataToDatabase(scope);
                // TODO: Use Bogus?
                SeedData(dbContext);
            }
        }

        private static void AddRequiredDataToDatabase(IServiceScope scope)
        {
            // Add required data to the database
            // Example: Add default roles, users, or any other necessary data
            // This method can be customized to add specific data to the database as needed

            SeedRoles(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
            SeedLookups(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        private static void SeedLookups(ApplicationDbContext dbContext)
        {
            // Status ids must match the ApplicationStatus enum values, so they're inserted explicitly
            // (the id column is an identity, hence IDENTITY_INSERT). Only missing rows are added.
            var existingStatusIds = dbContext.Statuses.Select(s => s.Id).ToHashSet();
            var missingStatuses = Enum.GetValues<ApplicationStatus>()
                .Where(s => !existingStatusIds.Contains((int)s))
                .Select(s => new Status { Id = (int)s, Name = s.ToString() })
                .ToList();

            if (missingStatuses.Count > 0)
            {
                using var transaction = dbContext.Database.BeginTransaction();
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Status] ON");
                dbContext.Statuses.AddRange(missingStatuses);
                dbContext.SaveChanges();
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Status] OFF");
                transaction.Commit();
            }

            // Unit types are matched by name; an inactive type stays on units that already use it
            // but can't be chosen for any other unit.
            (string Name, bool Active)[] unitTypes =
            [
                ("Apartment", true),
                ("Studio", true),
                ("Townhouse", true),
                ("Loft", false),
            ];

            var existingUnitTypes = dbContext.UnitTypes.Select(t => t.Name).ToHashSet();
            dbContext.UnitTypes.AddRange(unitTypes
                .Where(t => !existingUnitTypes.Contains(t.Name))
                .Select(t => new UnitType { Name = t.Name, Active = t.Active }));
            dbContext.SaveChanges();
        }

        private static void SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in AppRoles.All)
            {
                if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
                }
            }
        }

        private static void SeedData(ApplicationDbContext dbContext)
        {
            //throw new NotImplementedException();
        }
    }
}
