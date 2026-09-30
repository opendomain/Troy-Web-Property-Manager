using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// What came back from a service call: it worked (maybe with a new id), it wasn't found, you're not allowed,
    /// someone else changed it first, or here are the errors.
    /// </summary>
    /// <remarks>
    /// For the normal "that's not allowed" or "that doesn't exist" cases the services return this instead of
    /// throwing, so controllers don't need try/catch. <see cref="NotFound"/> turns into a 404, <see cref="Forbidden"/>
    /// into a 403, and <see cref="Errors"/> go into ModelState (see <c>AppController.AddErrors</c>) so they show up like
    /// any validation message. The keys are field names, or "" for a general error that goes in the validation summary.
    /// Forbidden and Conflict results carry a message in Errors too, so they can still be shown to the user.
    /// </remarks>
    public class ServiceResult
    {
        /// <summary>Either it doesn't exist or this user can't see it. Both give a 404 so we don't reveal which.</summary>
        public bool NotFound { get; private init; }

        /// <summary>The user can see it but their role isn't allowed to do this. Gives a 403.</summary>
        public bool Forbidden { get; private init; }

        /// <summary>Someone else changed the data after this user loaded it. Reloading is the way forward.</summary>
        public bool Conflict { get; private init; }

        /// <summary>Id of whatever was created or saved, if there is one (e.g. the new application to redirect to).</summary>
        public int Id { get; private init; }

        /// <summary>Field name (or "" for general errors) → message.</summary>
        public Dictionary<string, string> Errors { get; } = [];

        /// <summary>
        /// It worked, but the saved section still breaks some of its rules. These don't make it a failure - sections
        /// can be saved with errors - they just block Submit until they're fixed.
        /// </summary>
        public IReadOnlyList<FieldError> Unresolved { get; private init; } = [];

        /// <summary>
        /// The section's new version after a successful save, so a form that stays open (the residence modal) can save
        /// again without looking stale to itself.
        /// </summary>
        public Guid? Version { get; private init; }
        public bool Succeeded
        {
            get { return !NotFound && !Forbidden && Errors.Count == 0; }
        }

        public static ServiceResult Ok(int id = 0)
        {
            return new() { Id = id };
        }

        /// <summary>Saved, with whatever rule errors are still left on it (see <see cref="Unresolved"/>).</summary>
        public static ServiceResult Saved(IReadOnlyList<FieldError> unresolved, int id = 0, Guid? version = null)
        {
            return new() { Id = id, Unresolved = unresolved, Version = version };
        }

        /// <summary>Not saved, with an error on each field that stopped it.</summary>
        public static ServiceResult Invalid(IEnumerable<FieldError> errors)
        {
            var result = new ServiceResult();
            foreach (var error in errors) result.Errors.TryAdd(error.Field, error.Message);
            return result;
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

        public static ServiceResult Forbid(string message)
        {
            var result = new ServiceResult { Forbidden = true };
            result.Errors[""] = message;
            return result;
        }

        public static ServiceResult Stale(string message = "This application was changed by someone else. Reload the page and try again.")
        {
            var result = new ServiceResult { Conflict = true };
            result.Errors[""] = message;
            return result;
        }
    }
}
