using System;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;
using PacsSoft.Configuration;

namespace PacsSoft.Data
{
    public sealed class MariaDbConnectionFactory
    {
        private readonly DatabaseSettings _settings;

        public MariaDbConnectionFactory(DatabaseSettings settings)
        {
            _settings = settings;
        }

        public async Task<MySqlConnection> OpenAsync()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = _settings.Host,
                Port = (uint)_settings.Port,
                Database = _settings.Database,
                UserID = _settings.User,
                Password = _settings.Password,
                CharacterSet = "utf8mb4",
                Pooling = true,
                ConnectionTimeout = 5,
                DefaultCommandTimeout = 30
            };
            var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }

        public async Task<bool> CanConnectAsync()
        {
            try
            {
                using (var connection = await OpenAsync().ConfigureAwait(false))
                using (var command = new MySqlCommand("SELECT 1", connection))
                {
                    return Convert.ToInt32(await command.ExecuteScalarAsync().ConfigureAwait(false)) == 1;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
