namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// What came back from a service call: it worked (maybe with a new id), it wasn't found, or here are the errors.
    /// </summary>
    /// <remarks>
    /// For the normal "that's not allowed" or "that doesn't exist" cases the services return this instead of
    /// throwing, so controllers don't need try/catch. <see cref="NotFound"/> turns into a 404, and <see cref="Errors"/>
    /// go into ModelState (see <c>AppController.AddErrors</c>) so they show up like any validation message. The keys
    /// are field names, or "" for a general error that goes in the validation summary.
    /// </remarks>
    public class ServiceResult
    {
        /// <summary>Either it doesn't exist or this user can't see it. Both give a 404 so we don't reveal which.</summary>
        public bool NotFound { get; private init; }

        /// <summary>Id of whatever was created or saved, if there is one (e.g. the new application to redirect to).</summary>
        public int Id { get; private init; }

        /// <summary>Field name (or "" for general errors) → message.</summary>
        public Dictionary<string, string> Errors { get; } = [];
        public bool Succeeded
        {
            get { return !NotFound && Errors.Count == 0; }
        }

        public static ServiceResult Ok(int id = 0)
        {
            return new() { Id = id };
        }

        public static ServiceResult Missing()
        {
            return new() { NotFound = true };
        }

        public static ServiceResult Error(string message, string key = "")
        {
            var result = new ServiceResult();
            result.Errors[key] = message;
            return result;
        }
    }
}
