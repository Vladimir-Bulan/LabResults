using System;
using FluentAssertions;
using LabResults.Domain.Aggregates;
using LabResults.Domain.Enums;
using LabResults.Domain.Events;
using LabResults.Domain.Exceptions;
using LabResults.Domain.ValueObjects;
using Xunit;

namespace LabResults.Tests;

public class SampleAggregateTests
{
    private static readonly Guid PatientGuid = Guid.NewGuid();
    private static readonly Guid DoctorGuid = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetStatusReceivedAndRaiseEvent()
    {
        var sample = Sample.Create(PatientGuid, AnalysisType.Glucose);
        sample.Status.Should().Be(SampleStatus.Received);
        sample.ResultStatus.Should().Be(ResultStatus.Pending);
        sample.Code.Value.Should().StartWith("LAB-");
        sample.DomainEvents.Should().ContainSingle(e => e is SampleReceivedEvent);
    }

    [Fact]
    public void Create_WithEmptyPatientId_ShouldThrow()
    {
        var act = () => Sample.Create(Guid.Empty, AnalysisType.Glucose);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddResult_ShouldSetStatusCompletedAndRaiseEvent()
    {
        var sample = Sample.Create(PatientGuid, AnalysisType.Glucose);
        sample.ClearEvents();
        var value = new ResultValue(5.5m, "mmol/L", 3.9m, 6.1m);
        sample.AddResult(value, "Normal glucose");
        sample.Status.Should().Be(SampleStatus.Completed);
        sample.Result.Should().NotBeNull();
        sample.Result!.Value.IsNormal.Should().BeTrue();
        sample.DomainEvents.Should().ContainSingle(e => e is ResultCompletedEvent);
    }

    [Fact]
    public void Validate_ShouldSetStatusValidatedAndRaiseEvent()
    {
        var sample = Sample.Create(PatientGuid, AnalysisType.Glucose);
        sample.AddResult(new ResultValue(5.5m, "mmol/L", 3.9m, 6.1m));
        sample.ClearEvents();
        sample.Validate(DoctorGuid, "Looks good");
        sample.Status.Should().Be(SampleStatus.Validated);
        sample.ValidatedBy!.Value.Should().Be(DoctorGuid);
        sample.DomainEvents.Should().ContainSingle(e => e is ResultValidatedEvent);
    }

    [Fact]
    public void Validate_AlreadyValidated_ShouldThrow()
    {
        var sample = Sample.Create(PatientGuid, AnalysisType.Glucose);
        sample.AddResult(new ResultValue(5.5m, "mmol/L", 3.9m, 6.1m));
        sample.Validate(DoctorGuid);
        var act = () => sample.Validate(DoctorGuid);
        act.Should().Throw<ResultAlreadyValidatedException>();
    }

    [Fact]
    public void Validate_WithoutResult_ShouldThrow()
    {
        var sample = Sample.Create(PatientGuid, AnalysisType.Glucose);
        var act = () => sample.Validate(DoctorGuid);
        act.Should().Throw<ResultNotReadyException>();
    }

    [Fact]
    public void MarkNotified_ShouldSetStatusNotifiedAndRaiseEvent()
    {
        var sample = Sample.Create(PatientGuid, AnalysisType.Glucose);
        sample.AddResult(new ResultValue(5.5m, "mmol/L", 3.9m, 6.1m));
        sample.Validate(DoctorGuid);
        sample.ClearEvents();
        sample.MarkNotified("patient@email.com");
        sample.ResultStatus.Should().Be(ResultStatus.Notified);
        sample.DomainEvents.Should().ContainSingle(e => e is PatientNotifiedEvent);
    }

    [Fact]
    public void ResultValue_AboveRange_ShouldBeHigh()
    {
        var value = new ResultValue(9.0m, "mmol/L", 3.9m, 6.1m);
        value.IsNormal.Should().BeFalse();
        value.Status.Should().Be("High");
    }

    [Fact]
    public void ResultValue_BelowRange_ShouldBeLow()
    {
        var value = new ResultValue(2.0m, "mmol/L", 3.9m, 6.1m);
        value.IsNormal.Should().BeFalse();
        value.Status.Should().Be("Low");
    }

    [Fact]
    public void SampleCode_Generate_ShouldHaveCorrectFormat()
    {
        var code = SampleCode.Generate();
        code.Value.Should().StartWith("LAB-");
        code.Value.Length.Should().BeGreaterThan(8);
    }
}
