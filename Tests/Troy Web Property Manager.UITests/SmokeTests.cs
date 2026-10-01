using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;

namespace Troy_Web_Property_Manager.UITests
{
    [Collection(UiCollection.Name)]
    public sealed class SmokeTests(UiFixture app)
    {
        [Fact]
        public void AppServesPagesScriptsAndStyles()
        {
            using var browser = app.NewBrowser();
            browser.Go("/");

            Assert.Contains("Troy Web Property Manager", browser.PageText);
            foreach (var asset in new[] { "/js/site.js", "/js/grid.js", "/css/site.css", "/lib/bootstrap/dist/css/bootstrap.min.css",
                         "/lib/fontawesome/css/fontawesome.min.css", "/lib/jquery/dist/jquery.min.js" })
            {
                Assert.Equal(200, browser.Fetch(asset).Status);
            }

            browser.Go("/Identity/Account/Login");
            browser.Type(By.Id("Input_Email"), UiFixture.SeededManager.Email);
            browser.Type(By.Id("Input_Password"), UiFixture.SeededManager.Password);
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Log in")));
            browser.Go("/Properties");
            browser.Click(By.XPath(Xp.Button("Add property")));
            browser.WaitForModal("Add property");
        }
    }
}
