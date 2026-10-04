using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using Troy_Web_Property_Manager.Areas.Identity.Pages.Account;

namespace Troy_Web_Property_Manager.Tests.Controllers;

public class RegisterConfirmationTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void ConfirmationLinkFromTempData_IsOnlyDisplayedInDevelopment(string environment, bool showLink)
    {
        const string link = "https://example.com/Identity/Account/ConfirmEmail?code=secret";
        var page = new RegisterConfirmationModel(new TestEnvironment(environment))
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new EmptyTempDataProvider())
            {
                [RegisterConfirmationModel.ConfirmationLinkKey] = link,
                [RegisterConfirmationModel.EmailFailedKey] = true
            }
        };
        page.OnGet("user@example.com");
        Assert.Equal(showLink ? link : null, page.ConfirmationLink);
        Assert.True(page.EmailFailed);
    }

    private sealed class TestEnvironment(string environment) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
