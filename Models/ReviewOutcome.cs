namespace Troy_Web_Property_Manager.Models
{
    /// <summary>
    /// What a property manager decided in a review (5.a). <c>ApplicationWorkflow.StatusFor</c> turns each one into a
    /// status. Return and Deny need a comment. Saved on the history row.
    /// </summary>
    public enum ReviewOutcome : long
    {
        Approve = 1,
        Return = 2,
        Deny = 3
    }
}
