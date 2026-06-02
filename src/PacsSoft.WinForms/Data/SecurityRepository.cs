using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using PacsSoft.Models;

namespace PacsSoft.Data
{
    public sealed class SecurityRepository
    {
        private readonly MariaDbConnectionFactory _factory;
        public SecurityRepository(MariaDbConnectionFactory factory) => _factory = factory;

        public async Task InsertSecurityEventAsync(SecurityEventRecord record)
        {
            const string sql = @"INSERT INTO Security_Events(uid,card_id,reader_id,event_time,event_type,system_state,dahua_alert_state)
VALUES(@uid,@card,@reader,@time,@type,@system,@dahua)";
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@uid", (object)record.Uid ?? DBNull.Value);
                command.Parameters.AddWithValue("@card", (object)record.CardId ?? DBNull.Value);
                command.Parameters.AddWithValue("@reader", (object)record.ReaderId ?? DBNull.Value);
                command.Parameters.AddWithValue("@time", record.EventTime);
                command.Parameters.AddWithValue("@type", record.EventType.ToString());
                command.Parameters.AddWithValue("@system", record.SystemState);
                command.Parameters.AddWithValue("@dahua", (object)record.DahuaAlertState ?? DBNull.Value);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<string>> LoadAuditLinesAsync()
        {
            var rows = new List<string>();
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand("SELECT changed_at,uid,action,old_value,new_value,changed_by FROM Audit_Log ORDER BY changed_at DESC LIMIT 300", connection))
            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    rows.Add($"{reader.GetDateTime("changed_at"):yyyy-MM-dd HH:mm:ss} | uid={reader["uid"]} | {reader["action"]} | {reader["old_value"]} -> {reader["new_value"]} | by={reader["changed_by"]}");
                }
            }
            return rows;
        }
    }
}
