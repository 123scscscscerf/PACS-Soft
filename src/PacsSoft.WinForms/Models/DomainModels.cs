using System;

namespace PacsSoft.Models
{
    public enum PersonType { student, staff, admin, special }
    public enum AccessEventType { IN, OUT, DENY }
    public enum AccessResult { granted, denied }
    public enum PersonState { inside, outside }
    public enum SecurityEventType { fire, tamper, forced_entry }
    public enum AdminRole { superadmin, security, teacher }

    public sealed class Person
    {
        public long Uid { get; set; }
        public string CardId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Patronymic { get; set; }
        public DateTime? Birthdate { get; set; }
        public PersonType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FullName => $"{Surname} {Name} {Patronymic}".Trim();
    }

    public sealed class Student
    {
        public long Uid { get; set; }
        public int Course { get; set; }
        public bool AllowedIn { get; set; } = true;
        public bool IsBlocked { get; set; }
        public bool IsCardStolen { get; set; }
    }

    public sealed class Personnel
    {
        public long Uid { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
    }

    public sealed class AccessEventRecord
    {
        public long? Uid { get; set; }
        public string CardId { get; set; }
        public int DoorId { get; set; }
        public string ReaderId { get; set; }
        public DateTime EventTime { get; set; }
        public AccessEventType EventType { get; set; }
        public AccessResult Result { get; set; }
        public string Reason { get; set; }
        public string EventHash { get; set; }
        public bool Success => Result == AccessResult.granted;
    }

    public sealed class FailedAttempt
    {
        public string CardId { get; set; }
        public string ReaderId { get; set; }
        public DateTime AttemptTime { get; set; }
        public string Reason { get; set; }
    }

    public sealed class SecurityEventRecord
    {
        public long? Uid { get; set; }
        public string CardId { get; set; }
        public string ReaderId { get; set; }
        public DateTime EventTime { get; set; }
        public SecurityEventType EventType { get; set; }
        public string SystemState { get; set; }
        public string DahuaAlertState { get; set; }
    }
}
