using System;
using Microsoft.Data.Sqlite;

namespace EDM.Services.History
{
    public static class HistoryServiceExtensions
    {
        public static void UpdateVerification(this HistoryService service, long id, Models.VerificationState state, string? algorithm, string? trustedHash, string? computedHash, string? message, DateTime? time)
        {
            try
            {
                var conn = service.GetType().GetField("_connManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(service) as EDM.Services.Data.SqliteConnectionManager;
                if (conn == null) return;
                var connection = conn.GetConnection();
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "UPDATE downloads SET verification_state=@state, verification_algorithm=@alg, trusted_hash=@trusted, computed_hash=@computed, verification_message=@msg, verification_time=@time WHERE id=@id";
                    cmd.Parameters.AddWithValue("@state", (int)state);
                    cmd.Parameters.AddWithValue("@alg", (object?)algorithm ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@trusted", (object?)trustedHash ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@computed", (object?)computedHash ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@msg", (object?)message ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@time", (object?)time?.ToString("o") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Prepare();
                    cmd.ExecuteNonQuery();
                }
                finally
                {
                    conn.ReturnConnection(connection);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[HistoryServiceExtensions.UpdateVerification] Failed", ex);
            }
        }
    }
}
