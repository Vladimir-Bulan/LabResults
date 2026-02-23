using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FluentValidation;
using LabResults.Domain.Aggregates;
using LabResults.Domain.Enums;
using LabResults.Domain.Ports;
using LabResults.Domain.ValueObjects;
using LabResults.Domain.Exceptions;

// ── DTOs
namespace LabResults.Application.DTOs
{
    public record SubmitSampleRequest(Guid PatientId, string AnalysisType);
    public record AddResultRequest(Guid SampleId, decimal Numeric, string Unit, decimal ReferenceMin, decimal ReferenceMax, string Notes);
    public record ValidateResultRequest(Guid SampleId, Guid DoctorId, string Notes);
    public record SampleDto(Guid Id, string Code, Guid PatientId, string AnalysisType, string Status, string ResultStatus, ResultDto? Result, DateTime ReceivedAt);
    public record ResultDto(decimal Numeric, string Unit, string ResultStatus, bool IsNormal, string Notes, DateTime CompletedAt);
}

// ── Commands
namespace LabResults.Application.Commands
{
    using LabResults.Application.DTOs;

    public record SubmitSampleCommand(Guid PatientId, string AnalysisType) : IRequest<SampleDto>;
    public record AddResultCommand(Guid SampleId, decimal Numeric, string Unit, decimal ReferenceMin, decimal ReferenceMax, string Notes) : IRequest<SampleDto>;
    public record ValidateResultCommand(Guid SampleId, Guid DoctorId, string Notes) : IRequest<SampleDto>;
    public record RejectSampleCommand(Guid SampleId, string Reason) : IRequest<SampleDto>;
    public record NotifyPatientCommand(Guid SampleId, string PatientEmail, string PatientName) : IRequest<bool>;
}

// ── Queries
namespace LabResults.Application.Queries
{
    using LabResults.Application.DTOs;

    public record GetSampleByIdQuery(Guid SampleId) : IRequest<SampleDto>;
    public record GetSampleByCodeQuery(string Code) : IRequest<SampleDto>;
    public record GetPatientSamplesQuery(Guid PatientId) : IRequest<IEnumerable<SampleDto>>;
    public record GetPendingValidationQuery() : IRequest<IEnumerable<SampleDto>>;
    public record GenerateResultPdfQuery(Guid SampleId) : IRequest<byte[]>;
}

// ── Validators
namespace LabResults.Application.Validators
{
    using LabResults.Application.Commands;

    public class SubmitSampleCommandValidator : AbstractValidator<SubmitSampleCommand>
    {
        private static readonly string[] ValidTypes = Enum.GetNames(typeof(AnalysisType));
        public SubmitSampleCommandValidator()
        {
            RuleFor(x => x.PatientId).NotEmpty().WithMessage("PatientId is required.");
            RuleFor(x => x.AnalysisType).NotEmpty().Must(t => Array.Exists(ValidTypes, v => v.Equals(t, StringComparison.OrdinalIgnoreCase)))
                .WithMessage($"AnalysisType must be one of: {string.Join(", ", ValidTypes)}");
        }
    }

    public class AddResultCommandValidator : AbstractValidator<AddResultCommand>
    {
        public AddResultCommandValidator()
        {
            RuleFor(x => x.SampleId).NotEmpty();
            RuleFor(x => x.Numeric).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
            RuleFor(x => x.ReferenceMin).GreaterThanOrEqualTo(0);
            RuleFor(x => x.ReferenceMax).GreaterThan(x => x.ReferenceMin).WithMessage("ReferenceMax must be greater than ReferenceMin.");
        }
    }

    public class ValidateResultCommandValidator : AbstractValidator<ValidateResultCommand>
    {
        public ValidateResultCommandValidator()
        {
            RuleFor(x => x.SampleId).NotEmpty();
            RuleFor(x => x.DoctorId).NotEmpty();
        }
    }
}

// ── Mappers
namespace LabResults.Application.Mappers
{
    using LabResults.Application.DTOs;

    public static class SampleMapper
    {
        public static SampleDto ToDto(Sample sample) => new(
            sample.Id,
            sample.Code.Value,
            sample.PatientId.Value,
            sample.AnalysisType.ToString(),
            sample.Status.ToString(),
            sample.ResultStatus.ToString(),
            sample.Result != null ? new ResultDto(
                sample.Result.Value.Numeric,
                sample.Result.Value.Unit,
                sample.Result.Value.Status,
                sample.Result.Value.IsNormal,
                sample.Result.Notes,
                sample.Result.CompletedAt) : null,
            sample.ReceivedAt);
    }
}

// ── Behaviors
namespace LabResults.Application.Behaviors
{
    using FluentValidation;
    using MediatR;

    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            var failures = _validators
                .Select(v => v.Validate(request))
                .SelectMany(r => r.Errors)
                .Where(e => e != null)
                .ToList();

            if (failures.Any())
                throw new ValidationException(failures);

            return await next();
        }
    }
}

// ── Handlers
namespace LabResults.Application.Handlers
{
    using LabResults.Application.Commands;
    using LabResults.Application.DTOs;
    using LabResults.Application.Mappers;
    using LabResults.Application.Queries;
    using MediatR;

    public class SubmitSampleHandler : IRequestHandler<SubmitSampleCommand, SampleDto>
    {
        private readonly ISampleRepository _repo;
        public SubmitSampleHandler(ISampleRepository repo) => _repo = repo;
        public async Task<SampleDto> Handle(SubmitSampleCommand cmd, CancellationToken ct)
        {
            var analysisType = Enum.Parse<AnalysisType>(cmd.AnalysisType, true);
            var sample = Sample.Create(cmd.PatientId, analysisType);
            await _repo.AddAsync(sample, ct);
            return SampleMapper.ToDto(sample);
        }
    }

    public class AddResultHandler : IRequestHandler<AddResultCommand, SampleDto>
    {
        private readonly ISampleRepository _repo;
        public AddResultHandler(ISampleRepository repo) => _repo = repo;
        public async Task<SampleDto> Handle(AddResultCommand cmd, CancellationToken ct)
        {
            var sample = await _repo.GetByIdAsync(cmd.SampleId, ct)
                ?? throw new SampleNotFoundException(cmd.SampleId);
            var value = new ResultValue(cmd.Numeric, cmd.Unit, cmd.ReferenceMin, cmd.ReferenceMax);
            sample.AddResult(value, cmd.Notes);
            await _repo.UpdateAsync(sample, ct);
            return SampleMapper.ToDto(sample);
        }
    }

    public class ValidateResultHandler : IRequestHandler<ValidateResultCommand, SampleDto>
    {
        private readonly ISampleRepository _repo;
        private readonly INotificationPort _notification;
        public ValidateResultHandler(ISampleRepository repo, INotificationPort notification)
        { _repo = repo; _notification = notification; }
        public async Task<SampleDto> Handle(ValidateResultCommand cmd, CancellationToken ct)
        {
            var sample = await _repo.GetByIdAsync(cmd.SampleId, ct)
                ?? throw new SampleNotFoundException(cmd.SampleId);
            sample.Validate(cmd.DoctorId, cmd.Notes);
            await _repo.UpdateAsync(sample, ct);
            return SampleMapper.ToDto(sample);
        }
    }

    public class RejectSampleHandler : IRequestHandler<RejectSampleCommand, SampleDto>
    {
        private readonly ISampleRepository _repo;
        public RejectSampleHandler(ISampleRepository repo) => _repo = repo;
        public async Task<SampleDto> Handle(RejectSampleCommand cmd, CancellationToken ct)
        {
            var sample = await _repo.GetByIdAsync(cmd.SampleId, ct)
                ?? throw new SampleNotFoundException(cmd.SampleId);
            sample.Reject(cmd.Reason);
            await _repo.UpdateAsync(sample, ct);
            return SampleMapper.ToDto(sample);
        }
    }

    public class NotifyPatientHandler : IRequestHandler<NotifyPatientCommand, bool>
    {
        private readonly ISampleRepository _repo;
        private readonly INotificationPort _notification;
        public NotifyPatientHandler(ISampleRepository repo, INotificationPort notification)
        { _repo = repo; _notification = notification; }
        public async Task<bool> Handle(NotifyPatientCommand cmd, CancellationToken ct)
        {
            var sample = await _repo.GetByIdAsync(cmd.SampleId, ct)
                ?? throw new SampleNotFoundException(cmd.SampleId);
            await _notification.SendResultReadyEmailAsync(cmd.PatientEmail, cmd.PatientName, sample.Code.Value, ct);
            sample.MarkNotified(cmd.PatientEmail);
            await _repo.UpdateAsync(sample, ct);
            return true;
        }
    }

    public class GetSampleByIdHandler : IRequestHandler<GetSampleByIdQuery, SampleDto>
    {
        private readonly ISampleRepository _repo;
        public GetSampleByIdHandler(ISampleRepository repo) => _repo = repo;
        public async Task<SampleDto> Handle(GetSampleByIdQuery query, CancellationToken ct)
        {
            var sample = await _repo.GetByIdAsync(query.SampleId, ct)
                ?? throw new SampleNotFoundException(query.SampleId);
            return SampleMapper.ToDto(sample);
        }
    }

    public class GetSampleByCodeHandler : IRequestHandler<GetSampleByCodeQuery, SampleDto>
    {
        private readonly ISampleRepository _repo;
        public GetSampleByCodeHandler(ISampleRepository repo) => _repo = repo;
        public async Task<SampleDto> Handle(GetSampleByCodeQuery query, CancellationToken ct)
        {
            var sample = await _repo.GetByCodeAsync(query.Code, ct)
                ?? throw new DomainException($"Sample with code {query.Code} not found.");
            return SampleMapper.ToDto(sample);
        }
    }

    public class GetPatientSamplesHandler : IRequestHandler<GetPatientSamplesQuery, IEnumerable<SampleDto>>
    {
        private readonly ISampleRepository _repo;
        public GetPatientSamplesHandler(ISampleRepository repo) => _repo = repo;
        public async Task<IEnumerable<SampleDto>> Handle(GetPatientSamplesQuery query, CancellationToken ct)
        {
            var samples = await _repo.GetByPatientIdAsync(query.PatientId, ct);
            return samples.Select(SampleMapper.ToDto);
        }
    }

    public class GetPendingValidationHandler : IRequestHandler<GetPendingValidationQuery, IEnumerable<SampleDto>>
    {
        private readonly ISampleRepository _repo;
        public GetPendingValidationHandler(ISampleRepository repo) => _repo = repo;
        public async Task<IEnumerable<SampleDto>> Handle(GetPendingValidationQuery query, CancellationToken ct)
        {
            var samples = await _repo.GetPendingValidationAsync(ct);
            return samples.Select(SampleMapper.ToDto);
        }
    }

    public class GenerateResultPdfHandler : IRequestHandler<GenerateResultPdfQuery, byte[]>
    {
        private readonly IPdfPort _pdf;
        public GenerateResultPdfHandler(IPdfPort pdf) => _pdf = pdf;
        public async Task<byte[]> Handle(GenerateResultPdfQuery query, CancellationToken ct)
            => await _pdf.GenerateResultPdfAsync(query.SampleId, ct);
    }
}

// ── DI
namespace LabResults.Application
{
    using FluentValidation;
    using LabResults.Application.Behaviors;
    using MediatR;
    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            return services;
        }
    }
}
