using System;
using System.IO;
using System.Text.Json;

namespace PacsSoft.Configuration
{
    public sealed class AppSettings
    {
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();
        public DahuaSettings Dahua { get; set; } = new DahuaSettings();
        public string HttpBatchEndpoint { get; set; } = "http://gos.masterkliuch.kz/kit/hs/dacp";

        public static AppSettings Load()
        {
            var path = File.Exists("appsettings.json") ? "appsettings.json" : "appsettings.example.json";
            if (!File.Exists(path)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
    }

    public sealed class DatabaseSettings
    {
        public string Host { get; set; } = "vweb-01.local";
        public int Port { get; set; } = 3306;
        public string Database { get; set; } = "pacs_soft";
        public string User { get; set; } = "root";
        public string PasswordEnvironmentVariable { get; set; } = "PACS_DB_PASSWORD";
        public string Password => Environment.GetEnvironmentVariable(PasswordEnvironmentVariable) ?? string.Empty;
    }

    public sealed class DahuaSettings
    {
        public string Host { get; set; } = "192.168.100.121";
        public int Port { get; set; } = 37777;
        public string User { get; set; } = "admin";
        public string PasswordEnvironmentVariable { get; set; } = "PACS_DAHUA_PASSWORD";
        public string Password => Environment.GetEnvironmentVariable(PasswordEnvironmentVariable) ?? "Passw0rd!";
    }
}
