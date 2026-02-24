using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LabResults.Application.Commands;
using LabResults.Application.Handlers;
using LabResults.Domain.Aggregates;
using LabResults.Domain.Enums;
using LabResults.Domain.Exceptions;
using LabResults.Domain.Ports;
using LabResults.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace LabResults.Tests;

public class HandlersTests
{
    private readonly ISampleRepository _repo = Substitute.For<ISampleRepository>();
    private readonly INotificationPort _notify = Substitute.For<INotificationPort>();
    private readonly IPdfPort _pdf = Substitute.For<IPdfPort>();
    private readonly ICachePort _cache = Substitute.For<ICachePort>();

    [Fact]
    public async Task SubmitSample_ShouldAddToRepository()
    {
        var handler = new SubmitSampleHandler(_repo);
        var cmd = new SubmitSampleCommand(Guid.NewGuid(), "Glucose");
        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Id.Should().NotBeEmpty();
        await _repo.Received(1).AddAsync(Arg.Any<Sample>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddResult_ShouldUpdateRepository()
    {
        var sample = Sample.Create(Guid.NewGuid(), AnalysisType.Glucose);
        _repo.GetByIdAsync(sample.Id, Arg.Any<CancellationToken>()).Returns(sample);
        var handler = new AddResultHandler(_repo);
        var cmd = new AddResultCommand(sample.Id, 5.5m, "mmol/L", 3.9m, 6.1m, "ok");
        await handler.Handle(cmd, CancellationToken.None);
        await _repo.Received(1).UpdateAsync(sample, Arg.Any<CancellationToken>());
        sample.Status.Should().Be(SampleStatus.Completed);
    }

    [Fact]
    public async Task AddResult_SampleNotFound_ShouldThrow()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Sample?)null);
        var handler = new AddResultHandler(_repo);
        var cmd = new AddResultCommand(Guid.NewGuid(), 5.5m, "mmol/L", 3.9m, 6.1m, "");
        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<SampleNotFoundException>();
    }

    [Fact]
    public async Task ValidateResult_ShouldUpdateRepository()
    {
        var sample = Sample.Create(Guid.NewGuid(), AnalysisType.Glucose);
        sample.AddResult(new ResultValue(5.5m, "mmol/L", 3.9m, 6.1m));
        _repo.GetByIdAsync(sample.Id, Arg.Any<CancellationToken>()).Returns(sample);
        var handler = new ValidateResultHandler(_repo, _notify);
        var cmd = new ValidateResultCommand(sample.Id, Guid.NewGuid(), "All good");
        await handler.Handle(cmd, CancellationToken.None);
        sample.Status.Should().Be(SampleStatus.Validated);
        await _repo.Received(1).UpdateAsync(sample, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyPatient_ShouldCallNotificationPort()
    {
        var sample = Sample.Create(Guid.NewGuid(), AnalysisType.Glucose);
        sample.AddResult(new ResultValue(5.5m, "mmol/L", 3.9m, 6.1m));
        sample.Validate(Guid.NewGuid());
        _repo.GetByIdAsync(sample.Id, Arg.Any<CancellationToken>()).Returns(sample);
        var handler = new NotifyPatientHandler(_repo, _notify);
        var cmd = new NotifyPatientCommand(sample.Id, "John Doe", "john@email.com");
        await handler.Handle(cmd, CancellationToken.None);
        await _notify.Received(1).SendResultReadyEmailAsync("John Doe", "john@email.com", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
