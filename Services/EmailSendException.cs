namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// <see cref="EmailSender"/> couldn't send an email: SendGrid isn't configured, refused it, or couldn't be reached.
    /// </summary>
    /// <remarks>
    /// Its own type so callers can tell "the email didn't go" apart from a real bug. Register catches it and shows the
    /// confirmation link instead; on the other Identity pages that send email (Forgot password, Resend email
    /// confirmation, Manage email) <c>EmailFailureFilter</c> turns it into a message rather than a 500.
    /// It's an <see cref="InvalidOperationException"/>, which is what the sender threw before this type existed.
    /// </remarks>
    public class EmailSendException(string message, Exception? innerException = null)
        : InvalidOperationException(message, innerException);
}
