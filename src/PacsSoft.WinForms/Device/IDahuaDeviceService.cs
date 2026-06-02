using System;

namespace PacsSoft.Device
{
    public interface IDahuaDeviceService : IDisposable
    {
        event Action<string> LogMessage;
        event Action<string, int, string, DateTime, bool> AccessEventReceived;
        bool IsConnected { get; }
        void Connect();
        void StartListen();
        void RemoteOpenDoor(int doorId);
        void ChangeDoorMode(int doorId, DoorMode mode);
        void EmergencyOpenAllDoors();
    }

    public enum DoorMode
    {
        Normal = 1,
        AlwaysOpen = 2,
        AlwaysClosed = 3
    }
}
