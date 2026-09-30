using Microsoft.Data.SqlClient;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// Spots the SQL Server errors our services know how to deal with. EF might wrap them in a DbUpdateException.
    /// </summary>
    /// <remarks>
    /// A few rules are really enforced by the database (unique indexes, serializable transactions) because a
    /// check-then-write in code can lose a race to another request. When the database stops that, these helpers let
    /// the service recognize the error and show a friendly message or retry. Anything else still blows up normally.
    /// </remarks>
    internal static class SqlErrors
    {
        /// <summary>Error 1205: we lost a deadlock.</summary>
        public static bool IsDeadlock(Exception ex)
        {
            return HasNumber(ex, 1205);
        }

        /// <summary>Errors 2601/2627: hit a unique index or unique constraint.</summary>
        public static bool IsUniqueViolation(Exception ex)
        {
            return HasNumber(ex, 2601, 2627);
        }

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
