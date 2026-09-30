namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>Model for the generic confirmation modal (remove, withdraw).</summary>
    /// <param name="Title">Modal heading.</param>
    /// <param name="Message">The question being confirmed.</param>
    /// <param name="Action">URL the confirmation posts to.</param>
    /// <param name="ConfirmText">Label for the confirm button, e.g. "Remove" or "Withdraw".</param>
    public record ConfirmViewModel(string Title, string Message, string Action, string ConfirmText = "Confirm");
}
