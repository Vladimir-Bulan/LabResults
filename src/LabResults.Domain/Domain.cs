using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// ── Common
namespace LabResults.Domain.Common
{
    public abstract class ValueObject
    {
        protected abstract IEnumerable<object> GetEqualityComponents();
        public override bool Equals(object obj)
        {
            if (obj is null || obj.GetType() != GetType()) return false;
            return GetEqualityComponents().SequenceEqual(((ValueObject)obj).GetEqualityComponents());
        }
        public override int GetHashCode() =>
            GetEqualityComponents().Aggregate(0, (h, c) => HashCode.Combine(h, c?.GetHashCode() ?? 0));
    }
    public abstract class DomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
    public abstract class AggregateRoot
    {
        private readonly List<DomainEvent> _events = new();
        public IReadOnlyList<DomainEvent> DomainEvents => _events.AsReadOnly();
        public void ClearEvents() => _events.Clear();
        protected void AddEvent(DomainEvent e) => _events.Add(e);
    }
}

// ── Exceptions
namespace LabResults.Domain.Exceptions
{
    public class DomainException : Exception { public DomainException(string m) : base(m) { } }
    public class SampleNotFoundException : DomainException { public SampleNotFoundException(Guid id) : base($"Sample {id} not found.") { } }
    public class ResultAlreadyValidatedException : DomainException { public ResultAlreadyValidatedException() : base("Result already validated.") { } }
    public class ResultNotReadyException : DomainException { public ResultNotReadyException() : base("Result must be completed before this action.") { } }
}

// ── Enums
namespace LabResults.Domain.Enums
{
    public enum SampleStatus { Received, Processing, Completed, Validated, Rejected }
    public enum AnalysisType { BloodCount, Glucose, Cholesterol, Thyroid, Urine, Covid, Hepatitis, HIV }
    public enum ResultStatus { Pending, Completed, Validated, Notified }
}

// ── Value Objects
namespace LabResults.Domain.ValueObjects
{
    using LabResults.Domain.Common;

    public class PatientId : ValueObject
    {
        public Guid Value { get; }
        public PatientId(Guid value) { if (value == Guid.Empty) throw new ArgumentException("PatientId cannot be empty."); Value = value; }
        protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
        public override string ToString() => Value.ToString();
    }

    public class DoctorId : ValueObject
    {
        public Guid Value { get; }
        public DoctorId(Guid value) { if (value == Guid.Empty) throw new ArgumentException("DoctorId cannot be empty."); Value = value; }
        protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
    }

    public class SampleCode : ValueObject
    {
        public string Value { get; }
        public SampleCode(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("SampleCode cannot be empty."); Value = value.ToUpperInvariant(); }
        public static SampleCode Generate() => new($"LAB-{DateTime.UtcNow:yyyy}-{new Random().Next(100000, 999999)}");
        protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
        public override string ToString() => Value;
    }

    public class ResultValue : ValueObject
    {
        public decimal Numeric { get; }
        public string Unit { get; }
        public decimal ReferenceMin { get; }
        public decimal ReferenceMax { get; }
        public bool IsNormal => Numeric >= ReferenceMin && Numeric <= ReferenceMax;
        public string Status => IsNormal ? "Normal" : Numeric < ReferenceMin ? "Low" : "High";

        public ResultValue(decimal numeric, string unit, decimal refMin, decimal refMax)
        {
            if (refMin >= refMax) throw new ArgumentException("ReferenceMin must be less than ReferenceMax.");
            Numeric = numeric; Unit = unit; ReferenceMin = refMin; ReferenceMax = refMax;
        }
        protected override IEnumerable<object> GetEqualityComponents()
        { yield return Numeric; yield return Unit; yield return ReferenceMin; yield return ReferenceMax; }
        public override string ToString() => $"{Numeric} {Unit} ({Status})";
    }

    public class Email : ValueObject
    {
        public string Value { get; }
        public Email(string value) { if (string.IsNullOrWhiteSpace(value) || !value.Contains('@')) throw new ArgumentException("Invalid email."); Value = value.ToLowerInvariant(); }
        protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
        public override string ToString() => Value;
    }
}

// ── Events
namespace LabResults.Domain.Events
{
    using LabResults.Domain.Common;
    using LabResults.Domain.Enums;

    public class SampleReceivedEvent : DomainEvent { public Guid SampleId { get; init; } public Guid PatientId { get; init; } public AnalysisType AnalysisType { get; init; } }
    public class ResultCompletedEvent : DomainEvent { public Guid SampleId { get; init; } public Guid PatientId { get; init; } public string AnalysisType { get; init; } = string.Empty; }
    public class ResultValidatedEvent : DomainEvent { public Guid SampleId { get; init; } public Guid PatientId { get; init; } public Guid DoctorId { get; init; } public bool IsNormal { get; init; } }
    public class PatientNotifiedEvent : DomainEvent { public Guid SampleId { get; init; } public Guid PatientId { get; init; } public string PatientEmail { get; init; } = string.Empty; }
}

// ── Aggregates
namespace LabResults.Domain.Aggregates
{
    using LabResults.Domain.Common;
    using LabResults.Domain.Enums;
    using LabResults.Domain.Events;
    using LabResults.Domain.Exceptions;
    using LabResults.Domain.ValueObjects;

    public class AnalysisResult
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public AnalysisType Type { get; private set; }
        public ResultValue Value { get; private set; } = null!;
        public string Notes { get; private set; } = string.Empty;
        public DateTime CompletedAt { get; private set; }
        private AnalysisResult() { }
        public static AnalysisResult Create(AnalysisType type, ResultValue value, string notes = "") =>
            new() { Type = type, Value = value, Notes = notes, CompletedAt = DateTime.UtcNow };
    }

    public class Sample : AggregateRoot
    {
        public Guid Id { get; private set; }
        public SampleCode Code { get; private set; } = null!;
        public PatientId PatientId { get; private set; } = null!;
        public AnalysisType AnalysisType { get; private set; }
        public SampleStatus Status { get; private set; }
        public ResultStatus ResultStatus { get; private set; }
        public AnalysisResult? Result { get; private set; }
        public DoctorId? ValidatedBy { get; private set; }
        public string? ValidationNotes { get; private set; }
        public DateTime ReceivedAt { get; private set; }
        public DateTime? ValidatedAt { get; private set; }
        public DateTime? NotifiedAt { get; private set; }
        private Sample() { }

        public static Sample Create(Guid patientId, AnalysisType analysisType)
        {
            var sample = new Sample
            {
                Id = Guid.NewGuid(), Code = SampleCode.Generate(),
                PatientId = new PatientId(patientId), AnalysisType = analysisType,
                Status = SampleStatus.Received, ResultStatus = ResultStatus.Pending,
                ReceivedAt = DateTime.UtcNow
            };
            sample.AddEvent(new SampleReceivedEvent { SampleId = sample.Id, PatientId = patientId, AnalysisType = analysisType });
            return sample;
        }

        public void AddResult(ResultValue value, string notes = "")
        {
            Result = AnalysisResult.Create(AnalysisType, value, notes);
            Status = SampleStatus.Completed;
            ResultStatus = ResultStatus.Completed;
            AddEvent(new ResultCompletedEvent { SampleId = Id, PatientId = PatientId.Value, AnalysisType = AnalysisType.ToString() });
        }

        public void Validate(Guid doctorId, string notes = "")
        {
            if (ResultStatus == ResultStatus.Validated) throw new ResultAlreadyValidatedException();
            if (ResultStatus != ResultStatus.Completed) throw new ResultNotReadyException();
            ValidatedBy = new DoctorId(doctorId);
            ValidationNotes = notes;
            Status = SampleStatus.Validated;
            ResultStatus = ResultStatus.Validated;
            ValidatedAt = DateTime.UtcNow;
            AddEvent(new ResultValidatedEvent { SampleId = Id, PatientId = PatientId.Value, DoctorId = doctorId, IsNormal = Result!.Value.IsNormal });
        }

        public void MarkNotified(string patientEmail)
        {
            if (ResultStatus != ResultStatus.Validated) throw new ResultNotReadyException();
            ResultStatus = ResultStatus.Notified;
            NotifiedAt = DateTime.UtcNow;
            AddEvent(new PatientNotifiedEvent { SampleId = Id, PatientId = PatientId.Value, PatientEmail = patientEmail });
        }

        public void Reject(string reason) => Status = SampleStatus.Rejected;
    }
}

// ── Ports (Hexagonal - Output)
namespace LabResults.Domain.Ports
{
    using LabResults.Domain.Aggregates;

    public interface ISampleRepository
    {
        Task<Sample?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Sample?> GetByCodeAsync(string code, CancellationToken ct = default);
        Task<IEnumerable<Sample>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
        Task<IEnumerable<Sample>> GetPendingValidationAsync(CancellationToken ct = default);
        Task AddAsync(Sample sample, CancellationToken ct = default);
        Task UpdateAsync(Sample sample, CancellationToken ct = default);
    }

    public interface INotificationPort
    {
        Task SendResultReadyEmailAsync(string patientEmail, string patientName, string sampleCode, CancellationToken ct = default);
        Task SendAbnormalResultAlertAsync(string doctorEmail, string sampleCode, string analysisType, CancellationToken ct = default);
    }

    public interface IPdfPort
    {
        Task<byte[]> GenerateResultPdfAsync(Guid sampleId, CancellationToken ct = default);
    }

    public interface ICachePort
    {
        Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
        Task RemoveAsync(string key, CancellationToken ct = default);
    }
}
