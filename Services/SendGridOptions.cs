namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// The "SendGrid" config section, bound in Program.cs with the options pattern. Keeping it in config means each
    /// environment can use its own key (user secrets locally, environment variables in production).
    /// </summary>
    public class SendGridOptions
    {
        public string ApiKey { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "";
    }
}
