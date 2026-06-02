using System;
using System.Threading.Tasks;
using PacsSoft.Data;
using PacsSoft.Models;

namespace PacsSoft.Services
{
    public sealed class AccessControlService
    {
        private readonly PersonRepository _persons;
        private readonly AccessEventRepository _events;
        private readonly SecurityRepository _security;
        private readonly Device.IDahuaDeviceService _device;
        private bool _emergency;

        public AccessControlService(PersonRepository persons, AccessEventRepository events, SecurityRepository security, Device.IDahuaDeviceService device)
        {
            _persons = persons;
            _events = events;
            _security = security;
            _device = device;
        }

        public Task<long> CreatePersonAsync(Person person) => _persons.CreatePersonAsync(person);
        public Task DeletePersonAsync(long uid) => _persons.DeletePersonAsync(uid);
        public Task<Person> FindByCardAsync(string cardId) => _persons.FindByCardIdAsync(cardId);
        public Task AssignCardAsync(long uid, string cardId) => _persons.AssignCardAsync(uid, cardId);
        public Task SetStudentBlockedAsync(long uid, bool blocked) => _persons.SetBlockedAsync(uid, blocked);

        public async Task CreateStudentAsync(Person person, int course)
        {
            if (!string.IsNullOrWhiteSpace(person.CardId) && await _persons.CardExistsAsync(person.CardId).ConfigureAwait(false))
                throw new InvalidOperationException("Duplicate card_id is not allowed.");
            person.Type = PersonType.student;
            var uid = await _persons.CreatePersonAsync(person).ConfigureAwait(false);
            await _persons.UpsertStudentAsync(new Student { Uid = uid, Course = course, AllowedIn = true }).ConfigureAwait(false);
        }

        public async Task FireAlarmAsync(bool openAllDoors)
        {
            _emergency = true;
            await _security.InsertSecurityEventAsync(new SecurityEventRecord
            {
                EventTime = DateTime.Now,
                EventType = SecurityEventType.fire,
                SystemState = "emergency",
                DahuaAlertState = "UI simulation button"
            }).ConfigureAwait(false);
            if (openAllDoors) _device.EmergencyOpenAllDoors();
        }

        public void ResetEmergency() => _emergency = false;
        public bool IsEmergency => _emergency;
        public Task<int> EntriesTodayAsync() => _events.CountEntriesTodayAsync();
        public Task<int> DeniedLastMinutesAsync(int minutes) => _events.CountDeniedSinceAsync(DateTime.Now.AddMinutes(-minutes));
    }
}
