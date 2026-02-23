# LabResults

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet) ![Tests](https://img.shields.io/badge/tests-11%20passing-brightgreen) ![Architecture](https://img.shields.io/badge/architecture-hexagonal-blue) ![License](https://img.shields.io/badge/license-MIT-green)

> Medical Laboratory Results System built with pure Hexagonal Architecture (Ports & Adapters), DDD, CQRS, gRPC and .NET 8

## Architecture

```
[Driving Adapters]        [Domain Core]              [Driven Adapters]

REST API ─────────────┐
                      │  ┌──────────────────────┐  ┌─ PostgreSQL  (ISampleRepository)
gRPC Service ─────────┼─▶│  Application Layer   │─▶│─ Redis       (ICachePort)
                      │  │  Domain Layer        │  │─ Email       (INotificationPort)
                      ┘  └──────────────────────┘  └─ PDF         (IPdfPort)
```

The **Domain layer has zero NuGet dependencies**. All Ports (interfaces) are defined inside the Domain, and Adapters implement them in the Infrastructure layer — true Dependency Inversion.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 |
| REST API | ASP.NET Core Minimal APIs + Swagger |
| gRPC | Grpc.AspNetCore |
| CQRS | MediatR 12 + ValidationBehavior pipeline |
| Validation | FluentValidation 12 |
| Database | PostgreSQL 16 + EF Core 8 + Npgsql |
| Cache | Redis 7 + StackExchange.Redis |
| Tests | xUnit + FluentAssertions + NSubstitute |
| Containers | Docker Compose |

## Projects

| Project | Type | Responsibility |
|---------|------|--------------|
| **LabResults.Domain** | Class Library | Aggregates, Value Objects, Domain Events, Ports |
| **LabResults.Application** | Class Library | CQRS Commands/Queries, MediatR Handlers, Validators |
| **LabResults.Infrastructure** | Class Library | EF Core, Redis, Email, PDF adapters |
| **LabResults.API** | Web API | REST driving adapter — Minimal APIs + Swagger |
| **LabResults.GrpcService** | gRPC | gRPC driving adapter for lab technicians |
| **LabResults.Tests** | xUnit | Domain unit tests (11 passing) |

## Domain — Sample Lifecycle

```
Received ──[AddResult]──▶ Completed ──[Validate]──▶ Validated ──[Notify]──▶ Notified
    └─────────────────────────────────────────────────────[Reject]──▶ Rejected
```

### Domain Events
| Event | Raised When |
|-------|------------|
| SampleReceivedEvent | Sample.Create() |
| ResultCompletedEvent | Sample.AddResult() |
| ResultValidatedEvent | Sample.Validate() |
| PatientNotifiedEvent | Sample.MarkNotified() |

## API Endpoints

| Method | Route | Description | Actor |
|--------|-------|-------------|-------|
| POST | /api/samples | Submit new sample | Lab Technician |
| POST | /api/samples/{id}/result | Add analysis result | Lab Technician |
| POST | /api/samples/{id}/validate | Doctor validates result | Doctor |
| POST | /api/samples/{id}/reject | Reject sample | Supervisor |
| POST | /api/samples/{id}/notify | Notify patient | System |
| GET | /api/samples/{id} | Get sample by ID | Any |
| GET | /api/samples/code/{code} | Get by LAB-YYYY-XXXXXX | Any |
| GET | /api/patients/{id}/samples | Get patient history | Patient/Doctor |
| GET | /api/samples/pending-validation | Doctor dashboard | Doctor |
| GET | /api/samples/{id}/pdf | Download result PDF | Patient/Doctor |

## Getting Started

### Prerequisites
- .NET 8 SDK
- Docker Desktop

### Run with Docker
```bash
git clone https://github.com/Vladimir-Bulan/LabResults.git
cd LabResults
docker compose up -d

# API:     http://localhost:5010
# Swagger: http://localhost:5010/swagger
```

### Run locally
```bash
# Start dependencies
docker compose up postgres redis -d

# Apply migrations
dotnet ef database update --project src/LabResults.Infrastructure --startup-project src/LabResults.API

# Run API
dotnet run --project src/LabResults.API
```

### Run Tests
```bash
dotnet test tests/LabResults.Tests
# Passed: 11, Failed: 0
```

## Key Design Decisions

- **Domain has zero NuGet dependencies** — pure C# business logic, fully testable in isolation
- **Ports defined in Domain** — true Dependency Inversion, inner layers never depend on outer
- **Adapters are swappable** — replace PostgreSQL with MongoDB, SendGrid with Twilio, independently
- **MediatR ValidationBehavior** — FluentValidation runs as cross-cutting pipeline concern
- **Value Objects enforce invariants** — SampleCode (LAB-YYYY-XXXXXX), ResultValue (Normal/Low/High)
- **Two driving adapters** — REST API for patients/doctors, gRPC for lab technician systems

## License
MIT
