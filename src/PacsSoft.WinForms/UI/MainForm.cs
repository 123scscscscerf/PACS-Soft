using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PacsSoft.Configuration;
using PacsSoft.Data;
using PacsSoft.Device;
using PacsSoft.Models;
using PacsSoft.Services;

namespace PacsSoft.UI
{
    public sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly MariaDbConnectionFactory _dbFactory;
        private readonly PersonRepository _persons;
        private readonly AccessEventRepository _events;
        private readonly SecurityRepository _security;
        private readonly IDahuaDeviceService _dahua;
        private readonly EventProcessingService _pipeline;
        private readonly AccessControlService _access;

        private readonly ListBox _logs = new ListBox();
        private readonly DataGridView _eventsGrid = new DataGridView();
        private readonly Label _dbStatus = new Label();
        private readonly Label _dahuaStatus = new Label();
        private readonly Label _stats = new Label();
        private readonly TextBox _searchCard = new TextBox();
        private readonly TextBox _uid = new TextBox();
        private readonly TextBox _card = new TextBox();
        private readonly TextBox _name = new TextBox();
        private readonly TextBox _surname = new TextBox();
        private readonly NumericUpDown _course = new NumericUpDown();
        private readonly ListBox _audit = new ListBox();

        public MainForm()
        {
            _settings = AppSettings.Load();
            _dbFactory = new MariaDbConnectionFactory(_settings.Database);
            _persons = new PersonRepository(_dbFactory);
            _events = new AccessEventRepository(_dbFactory);
            _security = new SecurityRepository(_dbFactory);
            _dahua = new DahuaDeviceService(_settings.Dahua);
            _pipeline = new EventProcessingService(_events, _persons, _settings.HttpBatchEndpoint);
            _access = new AccessControlService(_persons, _events, _security, _dahua);

            Text = "СКУД Dahua + MariaDB - Управление и Мониторинг";
            Size = new Size(1180, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BuildLayout();
            WireServices();
            Load += async (s, e) => await StartAsync();
            FormClosing += (s, e) => Shutdown();
        }

        private void BuildLayout()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildMonitorTab());
            tabs.TabPages.Add(BuildUsersTab());
            tabs.TabPages.Add(BuildLogsTab());
            tabs.TabPages.Add(BuildAdminTab());
            ApplyRoleBasedAccess(tabs);

            var status = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(35, 35, 35), Padding = new Padding(8) };
            _dbStatus.AutoSize = true;
            _dbStatus.ForeColor = Color.Orange;
            _dahuaStatus.AutoSize = true;
            _dahuaStatus.ForeColor = Color.Orange;
            _stats.AutoSize = true;
            _stats.ForeColor = Color.White;
            status.Controls.AddRange(new Control[] { _dbStatus, Spacer(30), _dahuaStatus, Spacer(30), _stats });

            Controls.Add(tabs);
            Controls.Add(status);
        }

        private TabPage BuildMonitorTab()
        {
            var page = new TabPage("Мониторинг и двери");
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 154, BackColor = Color.FromArgb(30, 30, 30), Padding = new Padding(10), AutoScroll = true };
            panel.Controls.Add(MakeButton("🚪 Д1: Открыть", Color.LightGray, Color.Black, () => _dahua.RemoteOpenDoor(0)));
            panel.Controls.Add(MakeButton("🟢 Д1: Норма", Color.LightGreen, Color.Black, () => _dahua.ChangeDoorMode(0, DoorMode.Normal)));
            panel.Controls.Add(MakeButton("🔓 Д1: Всегда Откр", Color.Gold, Color.Black, () => _dahua.ChangeDoorMode(0, DoorMode.AlwaysOpen)));
            panel.Controls.Add(MakeButton("🔒 Д1: Всегда Закр", Color.DarkRed, Color.White, () => _dahua.ChangeDoorMode(0, DoorMode.AlwaysClosed)));
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);
            panel.Controls.Add(MakeButton("🚪 Д2: Открыть", Color.LightGray, Color.Black, () => _dahua.RemoteOpenDoor(1)));
            panel.Controls.Add(MakeButton("🟢 Д2: Норма", Color.LightGreen, Color.Black, () => _dahua.ChangeDoorMode(1, DoorMode.Normal)));
            panel.Controls.Add(MakeButton("🔓 Д2: Всегда Откр", Color.Gold, Color.Black, () => _dahua.ChangeDoorMode(1, DoorMode.AlwaysOpen)));
            panel.Controls.Add(MakeButton("🔒 Д2: Всегда Закр", Color.DarkRed, Color.White, () => _dahua.ChangeDoorMode(1, DoorMode.AlwaysClosed)));
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);
            panel.Controls.Add(MakeButton("🔥 FIRE ALARM: открыть все", Color.Red, Color.White, async () => await _access.FireAlarmAsync(true), 360));
            panel.Controls.Add(MakeButton("🚨 Аварийно открыть все", Color.OrangeRed, Color.White, () => _dahua.EmergencyOpenAllDoors(), 320));

            _logs.Dock = DockStyle.Fill;
            _logs.Font = new Font("Consolas", 10);
            _logs.BackColor = Color.Black;
            _logs.ForeColor = Color.Lime;
            page.Controls.Add(_logs);
            page.Controls.Add(panel);
            return page;
        }

        private TabPage BuildUsersTab()
        {
            var page = new TabPage("Пользователи");
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 190, ColumnCount = 4, RowCount = 5, Padding = new Padding(12) };
            AddLabeled(grid, "UID", _uid, 0, 0);
            AddLabeled(grid, "card_id", _card, 1, 0);
            AddLabeled(grid, "Имя", _name, 0, 1);
            AddLabeled(grid, "Фамилия", _surname, 1, 1);
            _course.Minimum = 1;
            _course.Maximum = 6;
            AddLabeled(grid, "Курс", _course, 0, 2);
            grid.Controls.Add(MakeButton("Создать студента", Color.SteelBlue, Color.White, async () => await CreateStudentAsync(), 170), 0, 3);
            grid.Controls.Add(MakeButton("Назначить карту", Color.DarkSlateBlue, Color.White, async () => await AssignCardAsync(), 170), 1, 3);
            grid.Controls.Add(MakeButton("Заблокировать", Color.DarkRed, Color.White, async () => await SetBlockedAsync(true), 170), 2, 3);
            grid.Controls.Add(MakeButton("Разблокировать", Color.ForestGreen, Color.White, async () => await SetBlockedAsync(false), 170), 3, 3);
            grid.Controls.Add(MakeButton("Удалить", Color.DimGray, Color.White, async () => await DeletePersonAsync(), 170), 0, 4);

            var search = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12) };
            search.Controls.Add(new Label { Text = "Поиск card_id:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
            _searchCard.Width = 220;
            search.Controls.Add(_searchCard);
            search.Controls.Add(MakeButton("Найти профиль", Color.Goldenrod, Color.Black, async () => await FindProfileAsync(), 150));
            page.Controls.Add(new Label { Dock = DockStyle.Fill, Font = new Font("Consolas", 11), Text = "Введите данные пользователя. UI вызывает только сервисный слой: проверки дублей card_id, блокировка и профиль выполняются в AccessControlService/Repository." });
            page.Controls.Add(search);
            page.Controls.Add(grid);
            return page;
        }

        private TabPage BuildLogsTab()
        {
            var page = new TabPage("Журналы");
            var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            var cardFilter = new TextBox { Width = 120 };
            var uidFilter = new TextBox { Width = 90 };
            var readerFilter = new TextBox { Width = 90 };
            filters.Controls.AddRange(new Control[] { new Label { Text = "card_id" }, cardFilter, new Label { Text = "uid" }, uidFilter, new Label { Text = "reader_id" }, readerFilter });
            filters.Controls.Add(MakeButton("Фильтр", Color.SteelBlue, Color.White, async () => await LoadAccessEventsAsync(cardFilter.Text, uidFilter.Text, readerFilter.Text), 120));
            _eventsGrid.Dock = DockStyle.Fill;
            _eventsGrid.ReadOnly = true;
            _eventsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            page.Controls.Add(_eventsGrid);
            page.Controls.Add(filters);
            return page;
        }

        private TabPage BuildAdminTab()
        {
            var page = new TabPage("Админ / аудит") { Name = "admin" };
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
            top.Controls.Add(MakeButton("Обновить аудит", Color.SteelBlue, Color.White, async () => await LoadAuditAsync(), 150));
            top.Controls.Add(MakeButton("Проверить соединения", Color.DarkCyan, Color.White, async () => await RefreshStatusAsync(), 180));
            _audit.Dock = DockStyle.Fill;
            _audit.Font = new Font("Consolas", 9);
            page.Controls.Add(_audit);
            page.Controls.Add(top);
            return page;
        }

        private static void ApplyRoleBasedAccess(TabControl tabs)
        {
            var roleText = Environment.GetEnvironmentVariable("PACS_UI_ROLE") ?? "superadmin";
            AdminRole role;
            if (!Enum.TryParse(roleText, true, out role)) role = AdminRole.superadmin;
            if (role == AdminRole.teacher)
            {
                tabs.TabPages.RemoveByKey("admin");
            }
        }

        private void WireServices()
        {
            _dahua.LogMessage += Log;
            _pipeline.LogMessage += Log;
            _pipeline.DbStatusChanged += ok => BeginInvoke(new Action(() => SetDbStatus(ok)));
            _dahua.AccessEventReceived += (card, door, reader, time, success) => _pipeline.EnqueueFromDahua(card, door, reader, time, success);
            _pipeline.EventReceived += record => Log($"{{{record.EventTime:dd.MM.yyyy; HH:mm:ss}}} ID: {record.CardId} | Reader: {record.ReaderId} | {record.Result}");
        }

        private async Task StartAsync()
        {
            _pipeline.Start();
            await RefreshStatusAsync();
            try { await new SchemaInitializer(_dbFactory).EnsureSchemaAsync(); }
            catch (Exception ex) { Log($"[SCHEMA] {ex.Message}"); }
            await Task.Run(() => { _dahua.Connect(); _dahua.StartListen(); });
            _dahuaStatus.Text = _dahua.IsConnected ? "Dahua: online" : "Dahua: offline";
            _dahuaStatus.ForeColor = _dahua.IsConnected ? Color.LightGreen : Color.OrangeRed;
            await RefreshStatsAsync();
        }

        private async Task RefreshStatusAsync()
        {
            SetDbStatus(await _dbFactory.CanConnectAsync());
            _dahuaStatus.Text = _dahua.IsConnected ? "Dahua: online" : "Dahua: offline";
            _dahuaStatus.ForeColor = _dahua.IsConnected ? Color.LightGreen : Color.OrangeRed;
        }

        private async Task RefreshStatsAsync()
        {
            try
            {
                var entries = await _access.EntriesTodayAsync();
                var denied = await _access.DeniedLastMinutesAsync(10);
                _stats.Text = $"Входов сегодня: {entries} | Отказов за 10 мин: {denied}";
                if (denied >= 10) Log("🚨 ALERT: всплеск отказов доступа за последние 10 минут.");
            }
            catch (Exception ex) { Log($"[STATS] {ex.Message}"); }
        }

        private async Task CreateStudentAsync()
        {
            await _access.CreateStudentAsync(new Person { CardId = _card.Text, Name = _name.Text, Surname = _surname.Text }, (int)_course.Value);
            Log("Пользователь-студент создан.");
        }

        private async Task AssignCardAsync()
        {
            await _access.AssignCardAsync(ParseUid(), _card.Text);
            Log($"Карта {_card.Text} назначена uid={_uid.Text}.");
        }

        private async Task SetBlockedAsync(bool blocked)
        {
            await _access.SetStudentBlockedAsync(ParseUid(), blocked);
            Log(blocked ? "Пользователь заблокирован." : "Пользователь разблокирован.");
        }

        private async Task DeletePersonAsync()
        {
            await _access.DeletePersonAsync(ParseUid());
            Log("Пользователь удален.");
        }

        private async Task FindProfileAsync()
        {
            var person = await _access.FindByCardAsync(_searchCard.Text);
            MessageBox.Show(person == null ? "Профиль не найден" : $"uid={person.Uid}\n{person.FullName}\nТип: {person.Type}\nКарта: {person.CardId}", "Профиль");
        }

        private async Task LoadAccessEventsAsync(string card, string uidText, string reader)
        {
            long parsedUid;
            var rows = await _events.SearchAsync(card, long.TryParse(uidText, out parsedUid) ? (long?)parsedUid : null, reader, DateTime.Today.AddDays(-7), DateTime.Now);
            _eventsGrid.DataSource = rows;
        }

        private async Task LoadAuditAsync()
        {
            _audit.Items.Clear();
            foreach (var line in await _security.LoadAuditLinesAsync()) _audit.Items.Add(line);
        }

        private long ParseUid() => long.Parse(_uid.Text);
        private void SetDbStatus(bool ok)
        {
            _dbStatus.Text = ok ? "DB: online" : "DB: offline/retry";
            _dbStatus.ForeColor = ok ? Color.LightGreen : Color.OrangeRed;
        }

        private void Log(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            _logs.Items.Insert(0, message);
            while (_logs.Items.Count > 1000) _logs.Items.RemoveAt(_logs.Items.Count - 1);
        }

        private void Shutdown()
        {
            _pipeline.Dispose();
            _dahua.Dispose();
        }

        private static Button MakeButton(string text, Color bg, Color fg, Action action, int width = 145)
        {
            var button = new Button { Text = text, BackColor = bg, ForeColor = fg, Width = width, Height = 40, Cursor = Cursors.Hand, Font = new Font("Consolas", 9, FontStyle.Bold), Margin = new Padding(5) };
            button.Click += (s, e) => action();
            return button;
        }

        private static void AddLabeled(TableLayoutPanel grid, string label, Control control, int column, int row)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            panel.Controls.Add(new Label { Text = label, Width = 80, Padding = new Padding(0, 7, 0, 0) });
            control.Width = 180;
            panel.Controls.Add(control);
            grid.Controls.Add(panel, column, row);
        }

        private static Control Spacer(int width) => new Label { Width = width };
    }
}
