using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Troy_Web_Property_Manager.Data;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>
    /// Real ASP.NET Core Identity (users, roles, password checks, lockout, token providers and the sign-in cookies) on a
    /// <see cref="TestDatabase"/>, for testing the Identity page models outside the web app. One request's worth:
    /// <see cref="Page{T}"/> gives a page model this host's request, and signing in writes its cookie to the response.
    /// </summary>
    public sealed class IdentityTestHost : IAsyncDisposable
    {
        public const string Password = "Test#2026";

        private readonly TestDatabase _db = new();
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        /// <param name="requireConfirmedAccount">The app always requires it; false reaches the scaffolded sign-in-straight-away path.</param>
        /// <param name="rejectUpdates">Identity refuses every change to a user that's already saved (creating one still works).</param>
        public IdentityTestHost(bool requireConfirmedAccount = true, bool rejectUpdates = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ => _db.CreateContext());
            var identity = services.AddIdentity<IdentityUser, IdentityRole>(o => o.SignIn.RequireConfirmedAccount = requireConfirmedAccount)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            if (rejectUpdates) identity.AddUserValidator<RejectUpdates>();

            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            HttpContext = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
            _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = HttpContext;
        }

        public HttpContext HttpContext { get; }

        public UserManager<IdentityUser> Users => _scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        public SignInManager<IdentityUser> SignIn => _scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();

        public async Task<IdentityUser> CreateUserAsync(string email, bool confirmed = true)
        {
            var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = confirmed };
            var result = await Users.CreateAsync(user, Password);
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
            return user;
        }

        /// <summary>Gives <paramref name="page"/> this host's request, signed in as <paramref name="user"/> if there is one.</summary>
        public T Page<T>(T page, IdentityUser? user = null) where T : PageModel
        {
            if (user is not null)
            {
                HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "Test"));
            }
            page.PageContext = new PageContext { HttpContext = HttpContext, RouteData = new RouteData() };
            page.Url = new ControllerTestContext.FakeUrlHelper(page.PageContext);
            page.TempData = new TempDataDictionary(HttpContext, new ControllerTestContext.NoTempDataProvider());
            return page;
        }

        /// <summary>The Set-Cookie headers written so far, i.e. whether anyone was signed in or out.</summary>
        public string Cookies => HttpContext.Response.Headers.SetCookie.ToString();

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            _db.Dispose();
        }

        private sealed class RejectUpdates : IUserValidator<IdentityUser>
        {
            public async Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
            {
                return await manager.FindByIdAsync(user.Id) is null
                    ? IdentityResult.Success
                    : IdentityResult.Failed(new IdentityError { Description = "Refused by the test." });
            }
        }
    }
}
