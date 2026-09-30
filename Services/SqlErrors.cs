using Microsoft.Data.SqlClient;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>Recognizes SQL Server errors the services handle. EF may wrap them in a DbUpdateException.</summary>
    internal static class SqlErrors
    {
        /// <summary>Error 1205: chosen as the deadlock victim.</summary>
        public static bool IsDeadlock(Exception ex) => HasNumber(ex, 1205);

        /// <summary>Errors 2601/2627: a unique index or unique constraint was violated.</summary>
        public static bool IsUniqueViolation(Exception ex) => HasNumber(ex, 2601, 2627);

        private static bool HasNumber(Exception ex, params int[] numbers)
        {
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                if (e is SqlException sql && numbers.Contains(sql.Number)) return true;
            }
            return false;
        }
    }
}
