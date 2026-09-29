using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SendGrid.Helpers.Mail;
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

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

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
