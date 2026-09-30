namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// Model for the generic "are you sure?" modal (remove property, unit or residence, and withdraw). They all share
    /// one partial (<c>Views/Shared/_Confirm.cshtml</c>), so a controller just supplies the wording and where to post.
    /// </summary>
    /// <param name="Title">Modal heading.</param>
    /// <param name="Message">The question being confirmed.</param>
    /// <param name="Action">URL the confirmation posts to.</param>
    /// <param name="ConfirmText">Label for the confirm button, e.g. "Remove" or "Withdraw".</param>
    public record ConfirmViewModel(string Title, string Message, string Action, string ConfirmText = "Confirm");
}
