using Microsoft.AspNetCore.Identity;
using Troy_Web_Property_Manager.Data;

namespace Troy_Web_Property_Manager.Areas.Identity
{
    public static class IdentityServiceCollectionExtensions
    {
        /// <summary>
        /// The app's Identity setup, in one place because Identity's options shape the EF model of the AspNet* tables.
        /// Program.cs uses it, and so does anything else that migrates the database (the UI tests deploy the schema
        /// with it before starting the app in Production), so the model always matches the migrations.
        /// </summary>
        /// <remarks>
        /// The default Identity UI gives us sign-up, log-in and log-out for free (1.a). AddRoles is what makes
        /// [Authorize(Roles = ...)] and User.IsInRole(...) work. Users and roles live in the same database as
        /// everything else. RequireConfirmedAccount means new users have to click the email link (sent by EmailSender)
        /// before logging in.
        /// </remarks>
        public static IdentityBuilder AddAppIdentity(this IServiceCollection services)
        {
            return services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
        }
    }
}
