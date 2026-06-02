using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PacsSoft.Data;
using PacsSoft.Models;

namespace PacsSoft.Services
{
    public sealed class EventProcessingService : IDisposable
    {
        private readonly ConcurrentQueue<AccessEventRecord> _queue = new ConcurrentQueue<AccessEventRecord>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly AccessEventRepository _events;
        private readonly PersonRepository _persons;
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly string _httpEndpoint;
        private Thread _workerThread;
        private volatile bool _running;

        public event Action<string> LogMessage;
        public event Action<AccessEventRecord> EventReceived;
        public event Action<bool> DbStatusChanged;

        public EventProcessingService(AccessEventRepository events, PersonRepository persons, string httpEndpoint)
        {
            _events = events;
            _persons = persons;
            _httpEndpoint = httpEndpoint;
        }

        public void Start()
        {
            _running = true;
            _workerThread = new Thread(ProcessQueue) { IsBackground = true, Name = "PACS DB/HTTP batch worker" };
            _workerThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _signal.Set();
            _workerThread?.Join(TimeSpan.FromSeconds(3));
        }

        public void EnqueueFromDahua(string cardNo, int doorId, string readerId, DateTime eventTime, bool success)
        {
            var eventType = success ? InferDirection(readerId, doorId) : AccessEventType.DENY;
            var record = new AccessEventRecord
            {
                CardId = string.IsNullOrWhiteSpace(cardNo) ? "UNKNOWN" : cardNo,
                DoorId = doorId,
                ReaderId = string.IsNullOrWhiteSpace(readerId) ? doorId.ToString() : readerId,
                EventTime = eventTime,
                EventType = eventType,
                Result = success ? AccessResult.granted : AccessResult.denied,
                Reason = success ? null : "Dahua controller denied access"
            };
            record.EventHash = ComputeHash(record);
            _queue.Enqueue(record);
            EventReceived?.Invoke(record);
            _signal.Set();
        }

        private void ProcessQueue()
        {
            const int maxBatchSize = 50;
            const int minBatchSize = 10;
            var waitTimeout = TimeSpan.FromSeconds(10);
            var currentBatch = new List<AccessEventRecord>();

            while (_running)
            {
                _signal.WaitOne(waitTimeout);
                while (currentBatch.Count < maxBatchSize && _queue.TryDequeue(out var dto)) currentBatch.Add(dto);
                if (currentBatch.Count == 0 || !_running) continue;
                if (currentBatch.Count < minBatchSize) DrainForMinimumBatch(currentBatch, minBatchSize, TimeSpan.FromSeconds(2));

                try
                {
                    ResolvePersonsAsync(currentBatch).GetAwaiter().GetResult();
                    Log($"[DB] Batch insert: {currentBatch.Count} access events.");
                    _events.InsertBatchAsync(currentBatch).GetAwaiter().GetResult();
                    DbStatusChanged?.Invoke(true);
                    _ = SendBatchToHttpAsync(new List<AccessEventRecord>(currentBatch));
                    currentBatch.Clear();
                }
                catch (Exception ex)
                {
                    DbStatusChanged?.Invoke(false);
                    Log($"[DB ERROR] {ex.Message}. Batch retained; retry in {waitTimeout.TotalSeconds:n0}s.");
                    Thread.Sleep(waitTimeout);
                }
            }
        }

        private void DrainForMinimumBatch(List<AccessEventRecord> currentBatch, int minBatchSize, TimeSpan extraWait)
        {
            var until = DateTime.UtcNow.Add(extraWait);
            while (currentBatch.Count < minBatchSize && DateTime.UtcNow < until)
            {
                if (_queue.TryDequeue(out var item)) currentBatch.Add(item);
                else Thread.Sleep(100);
            }
        }

        private async Task ResolvePersonsAsync(IEnumerable<AccessEventRecord> batch)
        {
            foreach (var record in batch)
            {
                var person = await _persons.FindByCardIdAsync(record.CardId).ConfigureAwait(false);
                record.Uid = person?.Uid;
                if (person == null && record.Result == AccessResult.granted)
                {
                    record.Result = AccessResult.denied;
                    record.EventType = AccessEventType.DENY;
                    record.Reason = "Unknown card";
                }
            }
        }

        private async Task SendBatchToHttpAsync(IReadOnlyList<AccessEventRecord> batch)
        {
            try
            {
                var payload = JsonSerializer.Serialize(batch);
                using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(_httpEndpoint, content).ConfigureAwait(false);
                    Log(response.IsSuccessStatusCode ? "[1C] Batch delivered." : $"[1C] HTTP {(int)response.StatusCode}: batch not accepted.");
                }
            }
            catch (Exception ex)
            {
                Log($"[HTTP EXCEPTION] {ex.Message}");
            }
        }

        private static AccessEventType InferDirection(string readerId, int doorId)
        {
            if (!string.IsNullOrEmpty(readerId) && readerId.ToUpperInvariant().Contains("OUT")) return AccessEventType.OUT;
            return doorId % 2 == 0 ? AccessEventType.IN : AccessEventType.OUT;
        }

        private static string ComputeHash(AccessEventRecord record)
        {
            var raw = $"{record.CardId}|{record.ReaderId}|{record.EventTime:O}|{record.Result}";
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", string.Empty);
        }

        private void Log(string text) => LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {text}");

        public void Dispose()
        {
            Stop();
            _signal.Dispose();
            _httpClient.Dispose();
        }
    }
}
