using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;

namespace PacsSoft.Data
{
    public sealed class SchemaInitializer
    {
        private readonly MariaDbConnectionFactory _factory;

        public SchemaInitializer(MariaDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task EnsureSchemaAsync()
        {
            var schemaPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "database", "schema.sql");
            if (!File.Exists(schemaPath)) schemaPath = Path.Combine("database", "schema.sql");
            if (!File.Exists(schemaPath)) return;

            using (var connection = await _factory.OpenAsync().ConfigureAwait(false))
            {
                var statements = File.ReadAllText(schemaPath).Split(';').Select(x => x.Trim()).Where(x => x.Length > 0);
                foreach (var statement in statements)
                {
                    using (var command = new MySqlCommand(statement, connection))
                    {
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
