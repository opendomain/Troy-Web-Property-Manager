namespace Troy_Web_Property_Manager.Services
{
    public class ServiceResult
    {
        public bool NotFound { get; private init; }
        public int Id { get; private init; }
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
