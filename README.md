# LabResults

> Medical Laboratory Results System — Hexagonal Architecture · DDD · CQRS · gRPC · .NET 8

## Architecture

```
[Driving Adapters]        [Domain Core]         [Driven Adapters]

REST API ─────────────┐
                      │  ┌──────────────────┐  ┌─ PostgreSQL  (ISampleRepository)
gRPC Service ─────────┼─▶│  Application     │─▶│─ Redis       (ICachePort)
                      │  │  Domain          │  │─ Email       (INotificationPort)
                      ┘  └──────────────────┘  └─ PDF         (IPdfPort)
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 |
| REST API | ASP.NET Core Minimal APIs + Swagger |
| gRPC | Grpc.AspNetCore |
| CQRS | MediatR 12 + ValidationBehavior |
| Validation | FluentValidation 12 |
| Database | PostgreSQL 16 + EF Core 8 + Npgsql |
| Cache | Redis 7 + StackExchange.Redis |
| Tests | xUnit + FluentAssertions + NSubstitute |
| Containers | Docker Compose |

## Projects

| Project | Responsibility |
|---------|--------------|
| **LabResults.Domain** | Aggregates, Value Objects, Domain Events, Ports |
| **LabResults.Application** | CQRS Commands/Queries, MediatR Handlers, Validators |
| **LabResults.Infrastructure** | EF Core, Redis, Email, PDF adapters |
| **LabResults.API** | REST driving adapter — Minimal APIs |
| **LabResults.GrpcService** | gRPC driving adapter for lab technicians |
| **LabResults.Tests** | Domain unit tests (11 passing) |

## Domain — Sample Lifecycle

```
Received → [AddResult] → Completed → [Validate] → Validated → [Notify] → Notified
    └──────────────────────────────────────────────────────── [Reject] → Rejected
```

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/samples | Submit new sample |
| POST | /api/samples/{id}/result | Add analysis result |
| POST | /api/samples/{id}/validate | Doctor validates result |
| POST | /api/samples/{id}/reject | Reject sample |
| POST | /api/samples/{id}/notify | Notify patient |
| GET | /api/samples/{id} | Get sample by ID |
| GET | /api/samples/code/{code} | Get by LAB-YYYY-XXXXXX code |
| GET | /api/patients/{id}/samples | Get patient samples |
| GET | /api/samples/pending-validation | Doctor dashboard |
| GET | /api/samples/{id}/pdf | Download result PDF |

## Run with Docker

```bash
docker compose up -d
# API: http://localhost:5010
# Swagger: http://localhost:5010/swagger
```

## Run Tests

```bash
dotnet test tests/LabResults.Tests
# 11 tests passing
```

## Key Design Decisions

- **Domain has zero NuGet dependencies** — pure C# business logic
- **Ports defined in Domain** — true Dependency Inversion
- **Adapters are swappable** — replace PostgreSQL, SendGrid, QuestPDF independently
- **MediatR ValidationBehavior** — cross-cutting validation pipeline
- **Value Objects enforce invariants** — SampleCode, ResultValue, Email, PatientId
- **Domain Events** — SampleReceived, ResultCompleted, ResultValidated, PatientNotified
