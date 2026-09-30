namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// Whoever's signed in. We pass this to the services so they can do their ownership and role checks.
    /// <c>AppController.CurrentUser</c> builds it from the auth cookie's claims, never from request data, so a post
    /// can't fake it. Using a simple record instead of HttpContext keeps the services free of ASP.NET and easy to test.
    /// </summary>
    /// <param name="Id">The Identity user id (AspNetUsers.Id).</param>
    /// <param name="IsManager">True for property managers, false for applicants.</param>
    public record CurrentUser(string Id, bool IsManager);
}
