using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Troy_Web_Property_Manager.Tests
{
    /// <summary>
    /// SQL Server errors the tests can throw. SQLite never deadlocks the way SQL Server does, so the tests fake the
    /// <see cref="SqlException"/> the services look for. SqlException has no public constructor, so this goes through
    /// SqlClient's internal factory.
    /// </summary>
    public static class FakeSqlErrors
    {
        public const int Deadlock = 1205;
        public const int UniqueViolation = 2601;
        public const int ReferenceConflict = 547;

        public static SqlException Create(int number)
        {
            const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;
            // Fill the longest SqlError constructor; its first int is the error number.
            var errorCtor = typeof(SqlError).GetConstructors(Internal).OrderByDescending(c => c.GetParameters().Length).First();
            var numberSet = false;
            var args = errorCtor.GetParameters().Select(p =>
            {
                if (p.ParameterType == typeof(int) && !numberSet)
                {
                    numberSet = true;
                    return number;
                }
                if (p.ParameterType == typeof(string)) return (object)"fake";
                return p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
            }).ToArray();
            var error = (SqlError)errorCtor.Invoke(args);

            var errors = (SqlErrorCollection)typeof(SqlErrorCollection).GetConstructors(Internal).Single(c => c.GetParameters().Length == 0).Invoke(null);
            typeof(SqlErrorCollection).GetMethod("Add", Internal)!.Invoke(errors, [error]);

            var create = typeof(SqlException).GetMethod("CreateException", BindingFlags.NonPublic | BindingFlags.Static,
                [typeof(SqlErrorCollection), typeof(string)])!;
            return (SqlException)create.Invoke(null, [errors, "16.0"])!;
        }

        /// <summary>
        /// Each SaveChanges fails with the next error number in <paramref name="numbers"/>, wrapped the way EF wraps
        /// it; once they run out, saves go through.
        /// </summary>
        public sealed class OnSave(params int[] numbers) : SaveChangesInterceptor
        {
            private int _next;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (_next < numbers.Length) throw new DbUpdateException("Fake SQL Server error.", Create(numbers[_next++]));
                return ValueTask.FromResult(result);
            }
        }
    }
}
