using System;
using System.Runtime.InteropServices;
using NetSDKCS;
using PacsSoft.Configuration;

namespace PacsSoft.Device
{
    public sealed class DahuaDeviceService : IDahuaDeviceService
    {
        private readonly DahuaSettings _settings;
        private IntPtr _loginId = IntPtr.Zero;
        private static fMessCallBack _alarmCallBack;

        public event Action<string> LogMessage;
        public event Action<string, int, string, DateTime, bool> AccessEventReceived;

        public DahuaDeviceService(DahuaSettings settings)
        {
            _settings = settings;
        }

        public bool IsConnected => _loginId != IntPtr.Zero;

        public void Connect()
        {
            Log("Инициализация Dahua NetSDK...");
            if (!NETClient.Init(null, IntPtr.Zero, null))
            {
                Log("ОШИБКА: Не удалось инициализировать SDK.");
                return;
            }

            var deviceInfo = new NET_DEVICEINFO_Ex();
            Log($"Подключение к контроллеру {_settings.Host}:{_settings.Port}...");
            _loginId = NETClient.LoginWithHighLevelSecurity(
                _settings.Host,
                (ushort)_settings.Port,
                _settings.User,
                _settings.Password,
                EM_LOGIN_SPAC_CAP_TYPE.TCP,
                IntPtr.Zero,
                ref deviceInfo);

            if (_loginId == IntPtr.Zero)
            {
                Log($"ОШИБКА: Не удалось подключиться к СКУД. Код: {NETClient.GetLastError()}");
                return;
            }
            Log("УСПЕХ: Контроллер подключен!");
        }

        public void StartListen()
        {
            if (_loginId == IntPtr.Zero) return;
            _alarmCallBack = OnAlarmEvent;
            NETClient.SetDVRMessCallBack(_alarmCallBack, IntPtr.Zero);
            Log(NETClient.StartListen(_loginId) ? "УСПЕХ: Мониторинг событий запущен." : "ОШИБКА: Не удалось запустить StartListen.");
        }

        private bool OnAlarmEvent(int lCommand, IntPtr lLoginID, IntPtr pBuf, uint dwBufLen, IntPtr pchDVRIP, int nDVRPort, IntPtr dwUser)
        {
            if (lCommand == 0x2213 || lCommand == (int)EM_ALARM_TYPE.ALARM_ACCESS_CTL_EVENT)
            {
                try
                {
                    var info = Marshal.PtrToStructure<NET_ALARM_ACCESS_CTL_EVENT_INFO>(pBuf);
                    var cardNo = info.szCardNo ?? "UNKNOWN";
                    var time = ConvertTime(info.stuTime);
                    var statusText = info.bStatus ? "РАЗРЕШЕН" : "ОТКЛОНЕН";
                    Log($"{{{time:dd.MM.yyyy; HH:mm:ss}}} ID: {cardNo} | Дверь: {info.nDoor} | Статус: {statusText}");
                    AccessEventReceived?.Invoke(cardNo, info.nDoor, info.szReaderID, time, info.bStatus);
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"[PARSE ERROR] {ex.Message}");
                }
            }
            return true;
        }

        public void RemoteOpenDoor(int doorId)
        {
            if (_loginId == IntPtr.Zero) return;
            IntPtr inPtr = IntPtr.Zero;
            try
            {
                var doorOpenCmd = new NET_CTRL_ACCESS_OPEN
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(NET_CTRL_ACCESS_OPEN)),
                    nChannelID = doorId
                };
                inPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NET_CTRL_ACCESS_OPEN)));
                Marshal.StructureToPtr(doorOpenCmd, inPtr, true);
                var result = NETClient.ControlDevice(_loginId, EM_CtrlType.ACCESS_OPEN, inPtr, 3000);
                Log(result ? $"[КОМАНДА] Турникет {doorId} УСПЕШНО ОТКРЫТ!" : $"[ОШИБКА] Не удалось открыть дверь {doorId}.");
            }
            catch (Exception ex) { Log($"[EXCEPTION] {ex.Message}"); }
            finally { if (inPtr != IntPtr.Zero) Marshal.FreeHGlobal(inPtr); }
        }

        public void ChangeDoorMode(int doorId, DoorMode mode)
        {
            if (_loginId == IntPtr.Zero) return;
            try
            {
                object objTemp = new NET_CFG_ACCESS_EVENT_INFO();
                if (NETClient.GetNewDevConfig(_loginId, doorId, "AccessControl", ref objTemp, typeof(NET_CFG_ACCESS_EVENT_INFO), 5000))
                {
                    var cfg = (NET_CFG_ACCESS_EVENT_INFO)objTemp;
                    cfg.emState = (EM_CFG_ACCESS_STATE)(int)mode;
                    NETClient.SetNewDevConfig(_loginId, doorId, "AccessControl", cfg, typeof(NET_CFG_ACCESS_EVENT_INFO), 5000);
                }

                IntPtr inPtr = IntPtr.Zero;
                try
                {
                    var cmd = new NET_CTRL_ACCESS_OPEN
                    {
                        dwSize = (uint)Marshal.SizeOf(typeof(NET_CTRL_ACCESS_OPEN)),
                        nChannelID = doorId
                    };
                    inPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NET_CTRL_ACCESS_OPEN)));
                    Marshal.StructureToPtr(cmd, inPtr, true);
                    NETClient.ControlDevice(_loginId, mode == DoorMode.AlwaysOpen ? EM_CtrlType.ACCESS_OPEN : (EM_CtrlType)260, inPtr, 3000);
                }
                finally { if (inPtr != IntPtr.Zero) Marshal.FreeHGlobal(inPtr); }

                Log($"[УСПЕХ] Режим '{ModeText(mode)}' применен к двери {doorId}!");
            }
            catch (Exception ex) { Log($"[EXCEPTION] {ex.Message}"); }
        }

        public void EmergencyOpenAllDoors()
        {
            Log("=========================================");
            Log("🚨 АВАРИЯ! ОТКРЫТИЕ ВСЕХ ТУРНИКЕТОВ 🚨");
            Log("=========================================");
            RemoteOpenDoor(0);
            RemoteOpenDoor(1);
        }

        private static DateTime ConvertTime(NET_TIME time) => new DateTime((int)time.dwYear, (int)time.dwMonth, (int)time.dwDay, (int)time.dwHour, (int)time.dwMinute, (int)time.dwSecond);
        private static string ModeText(DoorMode mode) => mode == DoorMode.Normal ? "НОРМАЛЬНЫЙ" : (mode == DoorMode.AlwaysOpen ? "ВСЕГДА ОТКРЫТО" : "ВСЕГДА ЗАКРЫТО");
        private void Log(string message) => LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");

        public void Dispose()
        {
            if (_loginId != IntPtr.Zero)
            {
                Log("Отключение от контроллера...");
                NETClient.StopListen(_loginId);
                NETClient.Logout(_loginId);
                _loginId = IntPtr.Zero;
            }
            NETClient.Cleanup();
        }
    }
}
