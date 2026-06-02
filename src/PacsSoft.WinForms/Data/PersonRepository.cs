using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using PacsSoft.Models;

namespace PacsSoft.Data
{
    public sealed class PersonRepository
    {
        private readonly MariaDbConnectionFactory _factory;

        public PersonRepository(MariaDbConnectionFactory factory) => _factory = factory;

        public async Task<long> CreatePersonAsync(Person person)
        {
            const string sql = @"INSERT INTO Persons(card_id,name,surname,patronymic,birthdate,type)
VALUES(@card,@name,@surname,@patronymic,@birthdate,@type);
SELECT LAST_INSERT_ID();";
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@card", (object)person.CardId ?? DBNull.Value);
                command.Parameters.AddWithValue("@name", person.Name);
                command.Parameters.AddWithValue("@surname", person.Surname);
                command.Parameters.AddWithValue("@patronymic", (object)person.Patronymic ?? DBNull.Value);
                command.Parameters.AddWithValue("@birthdate", (object)person.Birthdate ?? DBNull.Value);
                command.Parameters.AddWithValue("@type", person.Type.ToString());
                return Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false));
            }
        }

        public async Task AssignCardAsync(long uid, string cardId)
        {
            const string sql = "UPDATE Persons SET card_id=@card WHERE uid=@uid";
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@uid", uid);
                command.Parameters.AddWithValue("@card", cardId);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task DeletePersonAsync(long uid)
        {
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand("DELETE FROM Persons WHERE uid=@uid", connection))
            {
                command.Parameters.AddWithValue("@uid", uid);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<Person> FindByCardIdAsync(string cardId)
        {
            const string sql = "SELECT uid,card_id,name,surname,patronymic,birthdate,type,created_at FROM Persons WHERE card_id=@card";
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@card", cardId);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    return await reader.ReadAsync().ConfigureAwait(false) ? Map(reader) : null;
                }
            }
        }

        public async Task<IReadOnlyList<Person>> SearchAsync(string query)
        {
            const string sql = @"SELECT uid,card_id,name,surname,patronymic,birthdate,type,created_at FROM Persons
WHERE card_id LIKE @q OR name LIKE @q OR surname LIKE @q ORDER BY surname,name LIMIT 200";
            var result = new List<Person>();
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@q", $"%{query}%");
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false)) result.Add(Map(reader));
                }
            }
            return result;
        }

        public async Task<bool> CardExistsAsync(string cardId)
        {
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand("SELECT COUNT(*) FROM Persons WHERE card_id=@card", connection))
            {
                command.Parameters.AddWithValue("@card", cardId);
                return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) > 0;
            }
        }

        public async Task UpsertStudentAsync(Student student)
        {
            const string sql = @"INSERT INTO Students(uid,course,allowed_in,is_blocked,is_card_stolen)
VALUES(@uid,@course,@allowed,@blocked,@stolen)
ON DUPLICATE KEY UPDATE course=@course, allowed_in=@allowed, is_blocked=@blocked, is_card_stolen=@stolen";
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@uid", student.Uid);
                command.Parameters.AddWithValue("@course", student.Course);
                command.Parameters.AddWithValue("@allowed", student.AllowedIn);
                command.Parameters.AddWithValue("@blocked", student.IsBlocked);
                command.Parameters.AddWithValue("@stolen", student.IsCardStolen);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task SetBlockedAsync(long uid, bool blocked)
        {
            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            using (var command = new MySqlCommand("UPDATE Students SET is_blocked=@blocked WHERE uid=@uid", connection))
            {
                command.Parameters.AddWithValue("@uid", uid);
                command.Parameters.AddWithValue("@blocked", blocked);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static Person Map(MySqlDataReader reader) => new Person
        {
            Uid = reader.GetInt64("uid"),
            CardId = reader.IsDBNull(reader.GetOrdinal("card_id")) ? null : reader.GetString("card_id"),
            Name = reader.GetString("name"),
            Surname = reader.GetString("surname"),
            Patronymic = reader.IsDBNull(reader.GetOrdinal("patronymic")) ? null : reader.GetString("patronymic"),
            Birthdate = reader.IsDBNull(reader.GetOrdinal("birthdate")) ? (DateTime?)null : reader.GetDateTime("birthdate"),
            Type = (PersonType)Enum.Parse(typeof(PersonType), reader.GetString("type")),
            CreatedAt = reader.GetDateTime("created_at")
        };
    }
}
