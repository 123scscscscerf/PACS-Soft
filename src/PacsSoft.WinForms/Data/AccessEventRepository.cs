using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using PacsSoft.Models;

namespace PacsSoft.Data
{
    public sealed class AccessEventRepository
    {
        private readonly MariaDbConnectionFactory _factory;

        public AccessEventRepository(MariaDbConnectionFactory factory) => _factory = factory;

        public async Task InsertBatchAsync(IReadOnlyList<AccessEventRecord> batch)
        {
            if (batch.Count == 0) return;
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false))
            {
                foreach (var item in batch)
                {
                    const string sql = @"INSERT IGNORE INTO Access_Events(uid,card_id,reader_id,event_time,event_type,result,reason,event_hash)
VALUES(@uid,@card,@reader,@time,@type,@result,@reason,@hash);";
                    using (var command = new MySqlCommand(sql, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@uid", (object)item.Uid ?? DBNull.Value);
                        command.Parameters.AddWithValue("@card", item.CardId);
                        command.Parameters.AddWithValue("@reader", item.ReaderId);
                        command.Parameters.AddWithValue("@time", item.EventTime);
                        command.Parameters.AddWithValue("@type", item.EventType.ToString());
                        command.Parameters.AddWithValue("@result", item.Result.ToString());
                        command.Parameters.AddWithValue("@reason", (object)item.Reason ?? DBNull.Value);
                        command.Parameters.AddWithValue("@hash", item.EventHash);
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    if (item.Result == AccessResult.denied)
                    {
                        using (var command = new MySqlCommand("INSERT INTO Failed_Attempts(card_id,reader_id,attempt_time,reason) VALUES(@card,@reader,@time,@reason)", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@card", item.CardId);
                            command.Parameters.AddWithValue("@reader", item.ReaderId);
                            command.Parameters.AddWithValue("@time", item.EventTime);
                            command.Parameters.AddWithValue("@reason", (object)item.Reason ?? DBNull.Value);
                            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }

                    if (item.Uid.HasValue && item.Result == AccessResult.granted)
                    {
                        using (var command = new MySqlCommand(@"INSERT INTO Person_Status(uid,last_reader_id,last_event_time,state)
VALUES(@uid,@reader,@time,@state)
ON DUPLICATE KEY UPDATE last_reader_id=@reader,last_event_time=@time,state=@state", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@uid", item.Uid.Value);
                            command.Parameters.AddWithValue("@reader", item.ReaderId);
                            command.Parameters.AddWithValue("@time", item.EventTime);
                            command.Parameters.AddWithValue("@state", item.EventType == AccessEventType.IN ? "inside" : "outside");
                            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }
                }
                await transaction.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<AccessEventRecord>> SearchAsync(string cardId, long? uid, string readerId, DateTime? from, DateTime? to)
        {
            var result = new List<AccessEventRecord>();
            var sql = @"SELECT uid,card_id,reader_id,event_time,event_type,result,reason,event_hash FROM Access_Events
WHERE (@card IS NULL OR card_id=@card) AND (@uid IS NULL OR uid=@uid) AND (@reader IS NULL OR reader_id=@reader)
AND (@from IS NULL OR event_time>=@from) AND (@to IS NULL OR event_time<=@to)
ORDER BY event_time DESC LIMIT 500";
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@card", string.IsNullOrWhiteSpace(cardId) ? (object)DBNull.Value : cardId);
                command.Parameters.AddWithValue("@uid", uid.HasValue ? (object)uid.Value : DBNull.Value);
                command.Parameters.AddWithValue("@reader", string.IsNullOrWhiteSpace(readerId) ? (object)DBNull.Value : readerId);
                command.Parameters.AddWithValue("@from", from.HasValue ? (object)from.Value : DBNull.Value);
                command.Parameters.AddWithValue("@to", to.HasValue ? (object)to.Value : DBNull.Value);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new AccessEventRecord
                        {
                            Uid = reader.IsDBNull(reader.GetOrdinal("uid")) ? (long?)null : reader.GetInt64("uid"),
                            CardId = reader.GetString("card_id"),
                            ReaderId = reader.GetString("reader_id"),
                            EventTime = reader.GetDateTime("event_time"),
                            EventType = (AccessEventType)Enum.Parse(typeof(AccessEventType), reader.GetString("event_type")),
                            Result = (AccessResult)Enum.Parse(typeof(AccessResult), reader.GetString("result")),
                            Reason = reader.IsDBNull(reader.GetOrdinal("reason")) ? null : reader.GetString("reason"),
                            EventHash = reader.GetString("event_hash")
                        });
                    }
                }
            }
            return result;
        }

        public async Task<int> CountEntriesTodayAsync()
        {
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand("SELECT COUNT(*) FROM Access_Events WHERE event_type='IN' AND DATE(event_time)=CURDATE()", connection))
            {
                return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
            }
        }

        public async Task<int> CountDeniedSinceAsync(DateTime since)
        {
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand("SELECT COUNT(*) FROM Access_Events WHERE result='denied' AND event_time>=@since", connection))
            {
                command.Parameters.AddWithValue("@since", since);
                return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false));
            }
        }
    }
}
