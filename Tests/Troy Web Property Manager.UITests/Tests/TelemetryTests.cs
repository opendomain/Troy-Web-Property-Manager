using Troy_Web_Property_Manager.UITests.Infrastructure;

namespace Troy_Web_Property_Manager.UITests.Tests
{
    /// <summary>
    /// The GoatCounter page counter sends each page's path and query string to a third party, so it must never be on
    /// the Identity pages, whose query strings carry reset and confirmation tokens, user ids and email addresses.
    /// </summary>
    /// <remarks>
    /// These read the HTML with a plain HttpClient rather than a browser, so the counter script is never run and
    /// nothing is sent anywhere.
    /// </remarks>
    [Collection(TelemetryUiCollection.Name)]
    public sealed class TelemetryTests(TelemetryUiFixture app)
    {
        private const string CounterScript = "gc.zgo.at/count.js";

        private async Task<string> GetHtmlAsync(string path)
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.BaseUrl) };
            var response = await http.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Privacy")]
        public async Task SitePages_HaveTheCounter(string path)
        {
            var html = await GetHtmlAsync(path);

            Assert.Contains(CounterScript, html);
            Assert.Contains($"data-goatcounter=\"{TelemetryUiFixture.GoatCounterUrl}\"", html);
            Assert.DoesNotContain("allow_local", html);
        }

        [Theory]
        [InlineData("/Identity/Account/Login?ReturnUrl=%2FApplications")]
        [InlineData("/Identity/Account/Register")]
        [InlineData("/Identity/Account/RegisterConfirmation?email=someone%40uitest.local")]
        [InlineData("/Identity/Account/ForgotPassword")]
        [InlineData("/Identity/Account/ResetPassword?code=not-a-real-token")]
        [InlineData("/Identity/Account/ResendEmailConfirmation")]
        public async Task IdentityPages_DontHaveTheCounter(string path)
        {
            var html = await GetHtmlAsync(path);

            // It's the normal page, with the site layout - just without the counter.
            Assert.Contains("Troy Web Property Manager", html);
            Assert.DoesNotContain(CounterScript, html);
            Assert.DoesNotContain("data-goatcounter", html);
        }
    }
}
