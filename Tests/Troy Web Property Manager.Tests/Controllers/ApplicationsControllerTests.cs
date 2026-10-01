using Microsoft.AspNetCore.Mvc;
using Troy_Web_Property_Manager.Controllers;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Tests.Controllers
{
    /// <summary>
    /// The request checks the editor actions make before calling the service. Each of these is rejected before the
    /// service is touched, so the controller doesn't need real services.
    /// </summary>
    public class ApplicationsControllerTests
    {
        private static ApplicationsController Controller()
        {
            return new(null!, null!);
        }

        [Theory]
        [InlineData(ApplicationSection.ApplicantInformation)]
        [InlineData(ApplicationSection.ResidenceHistory)]
        public async Task Submit_FromAnEditableSection_IsBadRequest(ApplicationSection section)
        {
            var result = await Controller().Edit(1, new ApplicationEditorViewModel { Section = section }, "submit");

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task EditPost_UndefinedSection_IsBadRequest()
        {
            var result = await Controller().Edit(1, new ApplicationEditorViewModel { Section = (ApplicationSection)99 }, "save");

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task EditGet_UndefinedSection_IsBadRequest()
        {
            Assert.IsType<BadRequestResult>(await Controller().Edit(1, (ApplicationSection)99));
        }

        [Fact]
        public async Task EditGet_SectionThatDidNotBind_IsBadRequest()
        {
            var controller = Controller();
            controller.ModelState.AddModelError("section", "The value 'abc' is not valid.");

            Assert.IsType<BadRequestResult>(await controller.Edit(1, ApplicationSection.Summary));
        }
    }
}
