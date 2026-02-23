using LabResults.Application;
using LabResults.Application.Commands;
using LabResults.Application.Queries;
using LabResults.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/api/samples", async (SubmitSampleCommand cmd, IMediator m) =>
{
    var result = await m.Send(cmd);
    return Results.Created("/api/samples/" + result.Id, result);
});
app.MapPost("/api/samples/{id}/result", async (Guid id, AddResultCommand cmd, IMediator m) => Results.Ok(await m.Send(cmd with { SampleId = id })));
app.MapPost("/api/samples/{id}/validate", async (Guid id, ValidateResultCommand cmd, IMediator m) => Results.Ok(await m.Send(cmd with { SampleId = id })));
app.MapPost("/api/samples/{id}/reject", async (Guid id, [FromBody] string reason, IMediator m) => Results.Ok(await m.Send(new RejectSampleCommand(id, reason))));
app.MapPost("/api/samples/{id}/notify", async (Guid id, NotifyPatientCommand cmd, IMediator m) => Results.Ok(await m.Send(cmd with { SampleId = id })));
app.MapGet("/api/samples/{id}", async (Guid id, IMediator m) => Results.Ok(await m.Send(new GetSampleByIdQuery(id))));
app.MapGet("/api/samples/code/{code}", async (string code, IMediator m) => Results.Ok(await m.Send(new GetSampleByCodeQuery(code))));
app.MapGet("/api/patients/{patientId}/samples", async (Guid patientId, IMediator m) => Results.Ok(await m.Send(new GetPatientSamplesQuery(patientId))));
app.MapGet("/api/samples/pending-validation", async (IMediator m) => Results.Ok(await m.Send(new GetPendingValidationQuery())));
app.MapGet("/api/samples/{id}/pdf", async (Guid id, IMediator m) =>
{
    var pdf = await m.Send(new GenerateResultPdfQuery(id));
    return Results.File(pdf, "application/pdf", "result.pdf");
});
app.Run();
