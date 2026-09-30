namespace Troy_Web_Property_Manager.Services
{
    /// <summary>The signed-in user, passed to services so they can apply ownership and role rules.</summary>
    public record CurrentUser(string Id, bool IsManager);
}
