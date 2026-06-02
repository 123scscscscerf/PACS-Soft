using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlConnector;
using NetSDKCS;

namespace PacsSoft
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class MainForm : Form
    {
        private const string DahuaHost = "192.168.100.121";
        private const int DahuaPort = 37777;
        private const string DahuaUser = "admin";
        private const string DahuaPassword = "Passw0rd!";

        private const string DbHost = "vweb-01.local";
        private const int DbPort = 3306;
        private const string DbName = "pacs_soft";
        private const string DbUser = "root";
        private const string DbPassword = "BatyaAz2288!@";

        private readonly ListBox _logs = new ListBox();
        private readonly ListBox _events = new ListBox();
        private readonly TextBox _cardBox = new TextBox();
        private readonly TextBox _nameBox = new TextBox();
        private readonly TextBox _surnameBox = new TextBox();
        private readonly TextBox _patronymicBox = new TextBox();
        private readonly NumericUpDown _ageBox = new NumericUpDown();
        private readonly NumericUpDown _courseBox = new NumericUpDown();
        private readonly Label _statusLabel = new Label();

        private readonly ConcurrentQueue<AccessEvent> _queue = new ConcurrentQueue<AccessEvent>();
        private readonly AutoResetEvent _queueSignal = new AutoResetEvent(false);
        private Thread _workerThread;
        private volatile bool _running;

        private IntPtr _loginId = IntPtr.Zero;
        private static fMessCallBack _alarmCallback;

        private sealed class AccessEvent
        {
            public string CardId;
            public DateTime Time;
            public string ReaderId;
            public int DoorId;
            public bool Success;
        }

        public MainForm()
        {
            Text = "Simple PACS Prototype - Dahua + MariaDB";
            Size = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUi();
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
        }

        private void BuildUi()
        {
            _statusLabel.Dock = DockStyle.Top;
            _statusLabel.Height = 28;
            _statusLabel.Text = "DB: ? | Dahua: ?";
            _statusLabel.BackColor = Color.FromArgb(35, 35, 35);
            _statusLabel.ForeColor = Color.White;
            _statusLabel.Padding = new Padding(8, 6, 0, 0);

            var main = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 330 };

            _logs.Dock = DockStyle.Fill;
            _logs.Font = new Font("Consolas", 10);
            _logs.BackColor = Color.Black;
            _logs.ForeColor = Color.Lime;
            main.Panel1.Controls.Add(_logs);

            var bottom = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 520 };
            bottom.Panel1.Controls.Add(BuildButtonsPanel());

            _events.Dock = DockStyle.Fill;
            _events.Font = new Font("Consolas", 9);
            bottom.Panel2.Controls.Add(_events);
            main.Panel2.Controls.Add(bottom);

            Controls.Add(main);
            Controls.Add(_statusLabel);
        }

        private Control BuildButtonsPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(245, 245, 245),
                AutoScroll = true
            };

            panel.Controls.Add(Button("Connect Dahua", Color.SteelBlue, Color.White, ConnectDahua, 160));
            panel.Controls.Add(Button("Init DB", Color.DarkSlateBlue, Color.White, () => Task.Run(InitDatabase), 120));
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);

            panel.Controls.Add(Button("Door 1 Open", Color.LightGray, Color.Black, () => OpenDoor(0), 120));
            panel.Controls.Add(Button("Door 2 Open", Color.LightGray, Color.Black, () => OpenDoor(1), 120));
            panel.Controls.Add(Button("D1 Normal", Color.LightGreen, Color.Black, () => ChangeDoorMode(0, 1), 120));
            panel.Controls.Add(Button("D1 Open", Color.Gold, Color.Black, () => ChangeDoorMode(0, 2), 120));
            panel.Controls.Add(Button("D1 Closed", Color.DarkRed, Color.White, () => ChangeDoorMode(0, 3), 120));
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);

            panel.Controls.Add(Button("D2 Normal", Color.LightGreen, Color.Black, () => ChangeDoorMode(1, 1), 120));
            panel.Controls.Add(Button("D2 Open", Color.Gold, Color.Black, () => ChangeDoorMode(1, 2), 120));
            panel.Controls.Add(Button("D2 Closed", Color.DarkRed, Color.White, () => ChangeDoorMode(1, 3), 120));
            panel.Controls.Add(Button("FIRE ALARM", Color.Red, Color.White, FireAlarm, 190));
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);

            panel.Controls.Add(Label("card_id"));
            _cardBox.Width = 110;
            panel.Controls.Add(_cardBox);
            panel.Controls.Add(Label("name"));
            _nameBox.Width = 100;
            panel.Controls.Add(_nameBox);
            panel.Controls.Add(Label("surname"));
            _surnameBox.Width = 100;
            panel.Controls.Add(_surnameBox);
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);

            panel.Controls.Add(Label("patronymic"));
            _patronymicBox.Width = 110;
            panel.Controls.Add(_patronymicBox);
            panel.Controls.Add(Label("age"));
            _ageBox.Minimum = 1;
            _ageBox.Maximum = 120;
            _ageBox.Width = 60;
            panel.Controls.Add(_ageBox);
            panel.Controls.Add(Label("course"));
            _courseBox.Minimum = 1;
            _courseBox.Maximum = 6;
            _courseBox.Width = 60;
            panel.Controls.Add(_courseBox);
            panel.SetFlowBreak(panel.Controls[panel.Controls.Count - 1], true);

            panel.Controls.Add(Button("Add Student Card", Color.ForestGreen, Color.White, () => Task.Run(AddStudent), 160));
            panel.Controls.Add(Button("Add Personell Card", Color.ForestGreen, Color.White, () => Task.Run(AddPersonell), 170));
            panel.Controls.Add(Button("Delete Card", Color.DimGray, Color.White, () => Task.Run(DeleteCard), 130));
            panel.Controls.Add(Button("Reload Events", Color.SteelBlue, Color.White, () => Task.Run(LoadLastEvents), 140));

            return panel;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _running = true;
            _workerThread = new Thread(ProcessQueue) { IsBackground = true };
            _workerThread.Start();
            Task.Run(InitDatabase);
        }

        private void ConnectDahua()
        {
            Task.Run(() =>
            {
                Log("Dahua SDK init...");
                if (!NETClient.Init(null, IntPtr.Zero, null))
                {
                    Log("Dahua SDK init failed");
                    SetStatus(false);
                    return;
                }

                var deviceInfo = new NET_DEVICEINFO_Ex();
                Log("Dahua login " + DahuaHost + ":" + DahuaPort + "...");
                _loginId = NETClient.LoginWithHighLevelSecurity(
                    DahuaHost,
                    DahuaPort,
                    DahuaUser,
                    DahuaPassword,
                    EM_LOGIN_SPAC_CAP_TYPE.TCP,
                    IntPtr.Zero,
                    ref deviceInfo);

                if (_loginId == IntPtr.Zero)
                {
                    Log("Dahua login failed. SDK error: " + NETClient.GetLastError());
                    SetStatus(false);
                    return;
                }

                _alarmCallback = OnAlarmEvent;
                NETClient.SetDVRMessCallBack(_alarmCallback, IntPtr.Zero);
                if (NETClient.StartListen(_loginId)) Log("Dahua listening started");
                else Log("Dahua StartListen failed");
                SetStatus(true);
            });
        }

        private bool OnAlarmEvent(int lCommand, IntPtr lLoginID, IntPtr pBuf, uint dwBufLen, IntPtr pchDVRIP, int nDVRPort, IntPtr dwUser)
        {
            if (lCommand == 0x2213 || lCommand == (int)EM_ALARM_TYPE.ALARM_ACCESS_CTL_EVENT)
            {
                try
                {
                    var info = Marshal.PtrToStructure<NET_ALARM_ACCESS_CTL_EVENT_INFO>(pBuf);
                    var ev = new AccessEvent
                    {
                        CardId = string.IsNullOrWhiteSpace(info.szCardNo) ? "UNKNOWN" : info.szCardNo,
                        DoorId = info.nDoor,
                        ReaderId = string.IsNullOrWhiteSpace(info.szReaderID) ? info.nDoor.ToString() : info.szReaderID,
                        Time = ToDateTime(info.stuTime),
                        Success = info.bStatus
                    };

                    Log(string.Format("{0:yyyy-MM-dd HH:mm:ss} card={1} door={2} reader={3} {4}", ev.Time, ev.CardId, ev.DoorId, ev.ReaderId, ev.Success ? "OK" : "DENY"));
                    AddEventLine(ev);
                    _queue.Enqueue(ev);
                    _queueSignal.Set();
                }
                catch (Exception ex)
                {
                    Log("Dahua event parse error: " + ex.Message);
                }
            }
            return true;
        }

        private void ProcessQueue()
        {
            var batch = new List<AccessEvent>();
            while (_running)
            {
                _queueSignal.WaitOne(TimeSpan.FromSeconds(10));
                while (batch.Count < 50 && _queue.TryDequeue(out var ev)) batch.Add(ev);
                if (batch.Count == 0) continue;

                try
                {
                    InsertEvents(batch);
                    Log("DB inserted batch: " + batch.Count);
                    batch.Clear();
                    SetDbStatus(true);
                }
                catch (Exception ex)
                {
                    SetDbStatus(false);
                    Log("DB insert failed, retry later: " + ex.Message);
                    Thread.Sleep(5000);
                }
            }
        }

        private void InitDatabase()
        {
            try
            {
                using (var connection = OpenDb())
                {
                    foreach (var sql in SchemaSql())
                    {
                        using (var command = new MySqlCommand(sql, connection)) command.ExecuteNonQuery();
                    }
                }
                Log("DB schema ready");
                SetDbStatus(true);
            }
            catch (Exception ex)
            {
                SetDbStatus(false);
                Log("DB init failed: " + ex.Message);
            }
        }

        private void InsertEvents(List<AccessEvent> batch)
        {
            using (var connection = OpenDb())
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var ev in batch)
                {
                    var person = FindPerson(connection, transaction, ev.CardId);
                    InsertGenericEvent(connection, transaction, ev, person);

                    if (person.Table == "Students") InsertStudentEntry(connection, transaction, ev, person);
                    else if (person.Table == "Personell") InsertPersonellEntry(connection, transaction, ev, person);
                }
                transaction.Commit();
            }
        }

        private PersonRow FindPerson(MySqlConnection connection, MySqlTransaction transaction, string cardId)
        {
            var student = FindInTable(connection, transaction, "Students", cardId);
            if (student.Found) return student;
            return FindInTable(connection, transaction, "Personell", cardId);
        }

        private PersonRow FindInTable(MySqlConnection connection, MySqlTransaction transaction, string table, string cardId)
        {
            using (var command = new MySqlCommand("SELECT uid,card_id,name,surname,patronymic FROM " + table + " WHERE card_id=@card LIMIT 1", connection, transaction))
            {
                command.Parameters.AddWithValue("@card", cardId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return new PersonRow { CardId = cardId };
                    return new PersonRow
                    {
                        Found = true,
                        Table = table,
                        Uid = ReadLong(reader, "uid"),
                        CardId = ReadString(reader, "card_id"),
                        Name = ReadString(reader, "name"),
                        Surname = ReadString(reader, "surname"),
                        Patronymic = ReadString(reader, "patronymic")
                    };
                }
            }
        }

        private void InsertGenericEvent(MySqlConnection connection, MySqlTransaction transaction, AccessEvent ev, PersonRow person)
        {
            using (var command = new MySqlCommand(@"INSERT INTO Events(uid,card_id,name,surname,patronymic,datetime,reader_id,count,passback)
VALUES(@uid,@card,@name,@surname,@patronymic,@dt,@reader,1,@passback)", connection, transaction))
            {
                AddPersonParameters(command, ev, person);
                command.Parameters.AddWithValue("@passback", ev.Success ? 0 : 1);
                command.ExecuteNonQuery();
            }
        }

        private void InsertStudentEntry(MySqlConnection connection, MySqlTransaction transaction, AccessEvent ev, PersonRow person)
        {
            using (var command = new MySqlCommand(@"INSERT INTO Students_Entries(uid,card_id,name,surname,patronymic,datetime,reader_id)
VALUES(@uid,@card,@name,@surname,@patronymic,@dt,@reader)", connection, transaction))
            {
                AddPersonParameters(command, ev, person);
                command.ExecuteNonQuery();
            }
        }

        private void InsertPersonellEntry(MySqlConnection connection, MySqlTransaction transaction, AccessEvent ev, PersonRow person)
        {
            using (var command = new MySqlCommand(@"INSERT INTO Personell_Entries(uid,card_id,name,surname,patronymic,datetime,reader_id)
VALUES(@uid,@card,@name,@surname,@patronymic,@dt,@reader)", connection, transaction))
            {
                AddPersonParameters(command, ev, person);
                command.ExecuteNonQuery();
            }
        }

        private void AddStudent()
        {
            try
            {
                using (var connection = OpenDb())
                using (var command = new MySqlCommand(@"INSERT INTO Students(card_id,name,surname,patronymic,age,course,birthdate,allowed_in,card_is_stolen,blocked,signed_out)
VALUES(@card,@name,@surname,@patronymic,@age,@course,CURDATE(),1,0,0,0)
ON DUPLICATE KEY UPDATE name=@name,surname=@surname,patronymic=@patronymic,age=@age,course=@course", connection))
                {
                    AddUiPersonParameters(command);
                    command.Parameters.AddWithValue("@course", (int)_courseBox.Value);
                    command.ExecuteNonQuery();
                }
                Log("Student card saved: " + _cardBox.Text);
            }
            catch (Exception ex) { Log("Add student failed: " + ex.Message); }
        }

        private void AddPersonell()
        {
            try
            {
                using (var connection = OpenDb())
                using (var command = new MySqlCommand(@"INSERT INTO Personell(card_id,name,surname,patronymic,age,birthdate)
VALUES(@card,@name,@surname,@patronymic,@age,CURDATE())
ON DUPLICATE KEY UPDATE name=@name,surname=@surname,patronymic=@patronymic,age=@age", connection))
                {
                    AddUiPersonParameters(command);
                    command.ExecuteNonQuery();
                }
                Log("Personell card saved: " + _cardBox.Text);
            }
            catch (Exception ex) { Log("Add personell failed: " + ex.Message); }
        }

        private void DeleteCard()
        {
            try
            {
                using (var connection = OpenDb())
                {
                    using (var command = new MySqlCommand("DELETE FROM Students WHERE card_id=@card", connection))
                    {
                        command.Parameters.AddWithValue("@card", _cardBox.Text);
                        command.ExecuteNonQuery();
                    }
                    using (var command = new MySqlCommand("DELETE FROM Personell WHERE card_id=@card", connection))
                    {
                        command.Parameters.AddWithValue("@card", _cardBox.Text);
                        command.ExecuteNonQuery();
                    }
                }
                Log("Card deleted: " + _cardBox.Text);
            }
            catch (Exception ex) { Log("Delete card failed: " + ex.Message); }
        }

        private void FireAlarm()
        {
            Task.Run(() =>
            {
                Log("FIRE ALARM: logical open all doors + DB event");
                OpenDoor(0);
                OpenDoor(1);
                try
                {
                    using (var connection = OpenDb())
                    using (var command = new MySqlCommand(@"INSERT INTO Fire_Events(uid,sf_card_injected,special_event_name,card_id,pacs_state,dahua_alarm_state)
VALUES(NULL,0,'FIRE ALARM',@card,'emergency_open','ui_button')", connection))
                    {
                        command.Parameters.AddWithValue("@card", string.IsNullOrWhiteSpace(_cardBox.Text) ? DBNull.Value : (object)_cardBox.Text);
                        command.ExecuteNonQuery();
                    }
                    Log("Fire_Events row inserted");
                }
                catch (Exception ex) { Log("Fire event insert failed: " + ex.Message); }
            });
        }

        private void OpenDoor(int doorId)
        {
            if (_loginId == IntPtr.Zero)
            {
                Log("Dahua is not connected");
                return;
            }

            IntPtr ptr = IntPtr.Zero;
            try
            {
                var cmd = new NET_CTRL_ACCESS_OPEN
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(NET_CTRL_ACCESS_OPEN)),
                    nChannelID = doorId
                };
                ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NET_CTRL_ACCESS_OPEN)));
                Marshal.StructureToPtr(cmd, ptr, true);
                var ok = NETClient.ControlDevice(_loginId, EM_CtrlType.ACCESS_OPEN, ptr, 3000);
                Log(ok ? "Door opened: " + doorId : "Open door failed: " + doorId);
            }
            catch (Exception ex) { Log("Open door exception: " + ex.Message); }
            finally { if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr); }
        }

        private void ChangeDoorMode(int doorId, int mode)
        {
            if (_loginId == IntPtr.Zero)
            {
                Log("Dahua is not connected");
                return;
            }

            try
            {
                object cfgObject = new NET_CFG_ACCESS_EVENT_INFO();
                if (NETClient.GetNewDevConfig(_loginId, doorId, "AccessControl", ref cfgObject, typeof(NET_CFG_ACCESS_EVENT_INFO), 5000))
                {
                    var cfg = (NET_CFG_ACCESS_EVENT_INFO)cfgObject;
                    cfg.emState = (EM_CFG_ACCESS_STATE)mode;
                    NETClient.SetNewDevConfig(_loginId, doorId, "AccessControl", cfg, typeof(NET_CFG_ACCESS_EVENT_INFO), 5000);
                }
                if (mode == 2) OpenDoor(doorId);
                Log("Door mode changed: door=" + doorId + " mode=" + mode);
            }
            catch (Exception ex) { Log("Change mode failed: " + ex.Message); }
        }

        private void LoadLastEvents()
        {
            try
            {
                var lines = new List<string>();
                using (var connection = OpenDb())
                using (var command = new MySqlCommand("SELECT datetime,card_id,reader_id,passback FROM Events ORDER BY datetime DESC LIMIT 100", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lines.Add(string.Format("{0:yyyy-MM-dd HH:mm:ss} card={1} reader={2} passback={3}", reader.GetDateTime("datetime"), reader["card_id"], reader["reader_id"], reader["passback"]));
                    }
                }
                BeginInvoke(new Action(() =>
                {
                    _events.Items.Clear();
                    foreach (var line in lines) _events.Items.Add(line);
                }));
            }
            catch (Exception ex) { Log("Load events failed: " + ex.Message); }
        }

        private MySqlConnection OpenDb()
        {
            var cs = new MySqlConnectionStringBuilder
            {
                Server = DbHost,
                Port = (uint)DbPort,
                Database = DbName,
                UserID = DbUser,
                Password = DbPassword,
                CharacterSet = "utf8mb4",
                Pooling = true,
                ConnectionTimeout = 5,
                DefaultCommandTimeout = 30
            }.ConnectionString;
            var connection = new MySqlConnection(cs);
            connection.Open();
            return connection;
        }

        private IEnumerable<string> SchemaSql()
        {
            return new[]
            {
                @"CREATE TABLE IF NOT EXISTS Personell(
uid BIGINT AUTO_INCREMENT PRIMARY KEY,
card_id VARCHAR(64) UNIQUE,
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
age INT,
birthdate DATE)",
                @"CREATE TABLE IF NOT EXISTS Personell_Entries(
uid BIGINT,
card_id VARCHAR(64),
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
datetime DATETIME,
reader_id VARCHAR(64))",
                @"CREATE TABLE IF NOT EXISTS Students(
uid BIGINT AUTO_INCREMENT PRIMARY KEY,
card_id VARCHAR(64) UNIQUE,
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
age INT,
course INT,
birthdate DATE,
allowed_in TINYINT(1) DEFAULT 1,
card_is_stolen TINYINT(1) DEFAULT 0,
blocked TINYINT(1) DEFAULT 0,
signed_out TINYINT(1) DEFAULT 0)",
                @"CREATE TABLE IF NOT EXISTS Students_Entries(
uid BIGINT,
card_id VARCHAR(64),
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
datetime DATETIME,
reader_id VARCHAR(64))",
                @"CREATE TABLE IF NOT EXISTS Events(
uid BIGINT,
card_id VARCHAR(64),
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
datetime DATETIME,
reader_id VARCHAR(64),
count INT,
passback TINYINT(1))",
                @"CREATE TABLE IF NOT EXISTS Authorized_Administrators(
uid BIGINT AUTO_INCREMENT PRIMARY KEY,
vuid BIGINT,
login VARCHAR(100),
card_id VARCHAR(64),
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
role VARCHAR(50),
password_hash VARCHAR(255),
privileges TEXT,
teacher_flag TINYINT(1))",
                @"CREATE TABLE IF NOT EXISTS Special_Personell(
uid BIGINT AUTO_INCREMENT PRIMARY KEY,
vuid BIGINT,
login VARCHAR(100),
card_id VARCHAR(64),
special_group_name VARCHAR(100),
name VARCHAR(100),
surname VARCHAR(100),
patronymic VARCHAR(100),
role VARCHAR(50),
password_hash VARCHAR(255),
events TEXT)",
                @"CREATE TABLE IF NOT EXISTS Fire_Events(
uid BIGINT,
sf_card_injected TINYINT(1),
special_event_name VARCHAR(100),
card_id VARCHAR(64),
pacs_state VARCHAR(100),
dahua_alarm_state VARCHAR(100))"
            };
        }

        private void AddPersonParameters(MySqlCommand command, AccessEvent ev, PersonRow person)
        {
            command.Parameters.AddWithValue("@uid", person.Found ? (object)person.Uid : DBNull.Value);
            command.Parameters.AddWithValue("@card", ev.CardId);
            command.Parameters.AddWithValue("@name", person.Name ?? string.Empty);
            command.Parameters.AddWithValue("@surname", person.Surname ?? string.Empty);
            command.Parameters.AddWithValue("@patronymic", person.Patronymic ?? string.Empty);
            command.Parameters.AddWithValue("@dt", ev.Time);
            command.Parameters.AddWithValue("@reader", ev.ReaderId);
        }

        private void AddUiPersonParameters(MySqlCommand command)
        {
            command.Parameters.AddWithValue("@card", _cardBox.Text.Trim());
            command.Parameters.AddWithValue("@name", _nameBox.Text.Trim());
            command.Parameters.AddWithValue("@surname", _surnameBox.Text.Trim());
            command.Parameters.AddWithValue("@patronymic", _patronymicBox.Text.Trim());
            command.Parameters.AddWithValue("@age", (int)_ageBox.Value);
        }

        private static DateTime ToDateTime(NET_TIME time)
        {
            if (time.dwYear < 1900 || time.dwMonth < 1 || time.dwDay < 1) return DateTime.Now;
            return new DateTime((int)time.dwYear, (int)time.dwMonth, (int)time.dwDay, (int)time.dwHour, (int)time.dwMinute, (int)time.dwSecond);
        }

        private static long ReadLong(IDataRecord reader, string name)
        {
            var value = reader[name];
            return value == DBNull.Value ? 0 : Convert.ToInt64(value);
        }

        private static string ReadString(IDataRecord reader, string name)
        {
            var value = reader[name];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value);
        }

        private void AddEventLine(AccessEvent ev)
        {
            BeginInvoke(new Action(() =>
            {
                _events.Items.Insert(0, string.Format("{0:HH:mm:ss} card={1} reader={2} door={3} {4}", ev.Time, ev.CardId, ev.ReaderId, ev.DoorId, ev.Success ? "OK" : "DENY"));
                while (_events.Items.Count > 300) _events.Items.RemoveAt(_events.Items.Count - 1);
            }));
        }

        private void Log(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), text);
                return;
            }
            _logs.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + " " + text);
            while (_logs.Items.Count > 500) _logs.Items.RemoveAt(_logs.Items.Count - 1);
        }

        private void SetStatus(bool dahuaOk)
        {
            BeginInvoke(new Action(() => _statusLabel.Text = "DB: ? | Dahua: " + (dahuaOk ? "online" : "offline")));
        }

        private void SetDbStatus(bool dbOk)
        {
            BeginInvoke(new Action(() => _statusLabel.Text = "DB: " + (dbOk ? "online" : "offline") + " | Dahua: " + (_loginId != IntPtr.Zero ? "online" : "offline")));
        }

        private Button Button(string text, Color back, Color fore, Action action, int width)
        {
            var button = new Button { Text = text, Width = width, Height = 38, BackColor = back, ForeColor = fore, Margin = new Padding(5) };
            button.Click += (s, e) => action();
            return button;
        }

        private static Label Label(string text)
        {
            return new Label { Text = text, AutoSize = true, Padding = new Padding(0, 9, 0, 0), Margin = new Padding(8, 3, 3, 3) };
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _running = false;
            _queueSignal.Set();
            if (_loginId != IntPtr.Zero)
            {
                NETClient.StopListen(_loginId);
                NETClient.Logout(_loginId);
            }
            NETClient.Cleanup();
            _queueSignal.Dispose();
        }

        private sealed class PersonRow
        {
            public bool Found;
            public string Table;
            public long Uid;
            public string CardId;
            public string Name;
            public string Surname;
            public string Patronymic;
        }
    }
}
