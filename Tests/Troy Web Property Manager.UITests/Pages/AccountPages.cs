using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;

namespace Troy_Web_Property_Manager.UITests.Pages
{
    /// <summary>The log in page (Areas/Identity/Pages/Account/Login).</summary>
    public sealed class LoginPage(Browser browser)
    {
        public const string Path = "/Identity/Account/Login";

        public LoginPage Open()
        {
            browser.Go(Path);
            return this;
        }

        /// <summary>Fills in the form and submits it. Doesn't assume it worked - see <see cref="Errors"/>.</summary>
        public void Submit(string email, string password)
        {
            browser.Type(By.Id("Input_Email"), email);
            browser.Type(By.Id("Input_Password"), password);
            browser.ClickAndWaitForPage(By.XPath($"//main{Xp.Button("Log in")}"));
        }

        /// <summary>The form's error messages (validation summary and field errors).</summary>
        public string Errors => string.Join("\n", browser.FindAll(By.CssSelector(".text-danger")).Select(e => e.Text));
    }

    /// <summary>The sign-up page, with the role picker (Areas/Identity/Pages/Account/Register).</summary>
    public sealed class RegisterPage(Browser browser)
    {
        public const string Path = "/Identity/Account/Register";

        public RegisterPage Open()
        {
            browser.Go(Path);
            return this;
        }

        /// <param name="role">The role to pick, or null to leave the role unpicked.</param>
        public void Submit(string email, string password, string? confirmPassword = null, string? role = null)
        {
            browser.Type(By.Id("Input_Email"), email);
            browser.Type(By.Id("Input_Password"), password);
            browser.Type(By.Id("Input_ConfirmPassword"), confirmPassword ?? password);
            if (role is not null) browser.Click(By.Id("role-" + role.Replace(" ", "")));
            browser.Click(By.XPath($"//main{Xp.Button("Register")}"));
        }

        /// <summary>
        /// Submits with jQuery's client-side validation switched off, so the post reaches the server and we see the
        /// server's own validation (what a script or a browser without JavaScript would get).
        /// </summary>
        public void SubmitSkippingBrowserValidation(string email, string password, string confirmPassword, string? role)
        {
            browser.Js("document.querySelector('form').setAttribute('novalidate', ''); if (window.jQuery) jQuery('form').off('submit').removeData('validator');");
            browser.SetValue(By.Id("Input_Email"), email);
            browser.SetValue(By.Id("Input_Password"), password);
            browser.SetValue(By.Id("Input_ConfirmPassword"), confirmPassword);
            if (role is not null) browser.Click(By.Id("role-" + role.Replace(" ", "")));
            browser.ClickAndWaitForPage(By.XPath($"//main{Xp.Button("Register")}"));
        }

        public string Errors => string.Join("\n", browser.FindAll(By.CssSelector(".text-danger")).Select(e => e.Text));
    }

    /// <summary>The menu bar in _Layout, the same for every page.</summary>
    public sealed class NavBar(Browser browser)
    {
        /// <summary>The menu's link texts (Home, Privacy, then the role's links).</summary>
        public IReadOnlyList<string> Links =>
            browser.FindAll(By.CssSelector(".navbar-nav.flex-grow-1 .nav-link")).Select(e => e.Text.Trim()).ToList();

        /// <summary>The highlighted (current page) link, if any.</summary>
        public string? Active =>
            browser.FindAll(By.CssSelector(".navbar-nav .nav-link.active[aria-current='page']")).Select(e => e.Text.Trim()).FirstOrDefault();

        /// <summary>"Hello x@y!" when signed in, otherwise null.</summary>
        public string? Greeting =>
            browser.FindAll(By.CssSelector("a[title='Manage']")).Select(e => e.Text.Trim()).FirstOrDefault();

        public bool IsSignedIn => Greeting is not null;

        public void LogOut()
        {
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Logout")));
        }
    }

    /// <summary>The success/error messages _Layout shows after a redirect (AppController.SetMessage/SetError).</summary>
    public sealed class Flash(Browser browser)
    {
        public string? Success => browser.FindAll(By.CssSelector(".alert-success")).Select(e => e.Text.Trim()).FirstOrDefault();

        public string? Error => browser.FindAll(By.CssSelector("main > .alert-danger")).Select(e => e.Text.Trim()).FirstOrDefault();
    }
}
