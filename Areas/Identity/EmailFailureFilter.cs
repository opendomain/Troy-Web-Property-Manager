using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Troy_Web_Property_Manager.Services;

namespace Troy_Web_Property_Manager.Areas.Identity
{
    /// <summary>
    /// Turns a failed email on an Identity page into a message on that page instead of a 500 error page.
    /// </summary>
    /// <remarks>
    /// <para>Forgot password, Resend email confirmation and Manage → Email come from the default Identity UI, and they
    /// call the email sender without catching anything. Outside Development with no SendGrid key (or when SendGrid
    /// fails), <see cref="EmailSender"/> throws an <see cref="EmailSendException"/>, which would end up on the error
    /// page. Program.cs adds this filter to every page in the Identity area, so we don't have to scaffold and maintain
    /// copies of those pages just to add a try/catch. Register catches the exception itself (it shows the
    /// confirmation link instead), so it never gets here.</para>
    /// <para>The Manage pages are redirected back to themselves with an error status message, since their GET is what
    /// loads the user's details for the page. The others redraw the form with the message in the validation summary,
    /// keeping what was typed.</para>
    /// </remarks>
    public sealed class EmailFailureFilter : IAsyncPageFilter
    {
        public const string Message = "We couldn't send the email right now. Please try again later.";

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            var executed = await next();
            if (executed.ExceptionHandled || executed.Exception is not EmailSendException ex || executed.HandlerInstance is not PageModel page)
            {
                return;
            }

            var path = context.ActionDescriptor.ViewEnginePath;
            context.HttpContext.RequestServices.GetRequiredService<ILogger<EmailFailureFilter>>()
                .LogError(ex, "Couldn't send the email for {Page}; showing the user an error instead.", path);
            executed.ExceptionHandled = true;

            if (path.StartsWith("/Account/Manage/", StringComparison.OrdinalIgnoreCase))
            {
                // The Manage pages' _StatusMessage partial shows a message starting with "Error" in red.
                page.TempData["StatusMessage"] = "Error: " + Message;
                executed.Result = page.RedirectToPage();
            }
            else
            {
                page.ModelState.AddModelError(string.Empty, Message);
                executed.Result = page.Page();
            }
        }
    }
}
