using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Troy_Web_Property_Manager.Areas.Identity.Pages.Account;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Tests.Controllers;

public class RegisterTests
{
    // Only the environment matters for the role list and the GET; the rest is used when posting.
    private static RegisterModel Page(string environment) => new(null!, null!, NullLogger<RegisterModel>.Instance, null!,
        Options.Create(new SendGridOptions()), new ConfigurationBuilder().Build(), new TestWebHostEnvironment(environment));

    [Fact]
    public void RegistrationRoles_InDevelopment_IncludePropertyManager()
    {
        Assert.Equal(AppRoles.All, Page("Development").RegistrationRoles);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void RegistrationRoles_OutsideDevelopment_AreApplicantOnly(string environment)
    {
        Assert.Equal([AppRoles.Applicant], Page(environment).RegistrationRoles);
    }

    [Theory]
    [InlineData("Production", AppRoles.Applicant)]
    [InlineData("Staging", AppRoles.Applicant)]
    [InlineData("Development", null)]
    public void OnGet_PreselectsTheRole_OnlyWhenThereIsOneChoice(string environment, string? expectedRole)
    {
        var page = Page(environment);

        page.OnGet("/");

        Assert.Equal(expectedRole, page.Input?.Role);
    }
}
