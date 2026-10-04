using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Troy_Web_Property_Manager.Services;
using Troy_Web_Property_Manager.ViewComponents;
using static Troy_Web_Property_Manager.Tests.TestDatabase;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>The manager-only panels render nothing at all for an applicant, or for an application that isn't there.</summary>
    public sealed class ViewComponentTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose()
        {
            _db.Dispose();
        }

        private static T As<T>(T component, CurrentUser user) where T : ViewComponent
        {
            component.ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext { HttpContext = new DefaultHttpContext { User = ControllerTestContext.Principal(user) } }
            };
            return component;
        }

        private ApplicationService Service()
        {
            return new(_db.CreateContext());
        }

        private static void AssertRendersNothing(IViewComponentResult result)
        {
            Assert.Equal("", Assert.IsType<ContentViewComponentResult>(result).Content);
        }

        [Fact]
        public async Task History_ForAnApplicant_RendersNothing()
        {
            AssertRendersNothing(await As(new ApplicationHistoryViewComponent(Service()), ApplicantUser).InvokeAsync(1));
        }

        [Fact]
        public async Task ManagerNotes_ForAnApplicant_RendersNothing()
        {
            AssertRendersNothing(await As(new ManagerNotesViewComponent(Service()), ApplicantUser).InvokeAsync(1));
        }

        [Fact]
        public async Task ManagerNotes_ForAMissingApplication_RendersNothing()
        {
            AssertRendersNothing(await As(new ManagerNotesViewComponent(Service()), ManagerUser).InvokeAsync(9999));
        }
    }
}
