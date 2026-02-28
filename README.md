# LabResults

![CI](https://github.com/Vladimir-Bulan/LabResults/actions/workflows/ci.yml/badge.svg) ![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet) ![Tests](https://img.shields.io/badge/tests-16%20passing-brightgreen) ![Architecture](https://img.shields.io/badge/arquitectura-hexagonal-blue)

> Sistema de gestión de resultados de laboratorio clínico, construido con Arquitectura Hexagonal pura (Ports & Adapters), DDD, CQRS y .NET 8.

---

## ¿Qué hace este sistema?

LabResults gestiona el ciclo de vida completo de una muestra de laboratorio clínico:

1. El **técnico de laboratorio** ingresa una muestra (sangre, orina, etc.)
2. Se procesa y se carga el resultado con valores numéricos y rangos de referencia
3. Un **médico** valida el resultado (o lo rechaza si hay anomalías)
4. El sistema **notifica al paciente** por email que su resultado está disponible
5. Se puede generar un **PDF** del informe médico

---

## Demo — Flujo completo en Swagger

### 1. Técnico crea una muestra → `POST /api/samples`

```json
// Request
{
  "patientId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "analysisType": "Glucose"
}

// Response 201
{
  "id": "f1419e06-3a76-411d-be64-d0f38d1ce00b",
  "code": "LAB-2026-699191",
  "status": "Received",
  "resultStatus": "Pending"
}
```

### 2. Técnico carga el resultado → `POST /api/samples/{id}/result`

```json
// Request
{
  "sampleId": "f1419e06-3a76-411d-be64-d0f38d1ce00b",
  "numeric": 5.5,
  "unit": "mmol/L",
  "referenceMin": 3.9,
  "referenceMax": 6.1,
  "notes": "Análisis completado"
}

// Response 200
{
  "code": "LAB-2026-699191",
  "status": "Completed",
  "result": {
    "numeric": 5.5,
    "unit": "mmol/L",
    "resultStatus": "Normal",
    "isNormal": true
  }
}
```

> El Value Object `ResultValue` calcula automáticamente si el valor es **Normal**, **High** o **Low** comparando con los rangos de referencia. FluentValidation rechaza la request si `referenceMax <= referenceMin`.

### 3. Médico valida → `POST /api/samples/{id}/validate`

```json
// Request
{
  "sampleId": "f1419e06-3a76-411d-be64-d0f38d1ce00b",
  "doctorId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "notes": "Resultado dentro de parámetros normales, sin observaciones."
}

// Response 200
{
  "status": "Validated",
  "resultStatus": "Validated"
}
```

### 4. Sistema notifica al paciente → `POST /api/samples/{id}/notify`

```json
// Request
{
  "sampleId": "f1419e06-3a76-411d-be64-d0f38d1ce00b",
  "patientName": "Juan Pérez",
  "patientEmail": "juan.perez@email.com"
}

// Response 200
true
```

### 5. Consultar resultado → `GET /api/samples/{id}`

```json
// Response 200 — estado final completo
{
  "id": "f1419e06-3a76-411d-be64-d0f38d1ce00b",
  "code": "LAB-2026-699191",
  "analysisType": "Glucose",
  "status": "Validated",
  "resultStatus": "Notified",
  "result": {
    "numeric": 5.5,
    "unit": "mmol/L",
    "resultStatus": "Normal",
    "isNormal": true,
    "notes": "Análisis completado",
    "completedAt": "2026-02-28T22:23:05.676503Z"
  },
  "receivedAt": "2026-02-28T22:21:20.199893Z"
}
```

---

## Arquitectura Hexagonal (Ports & Adapters)

La idea central es que el **dominio del negocio no depende de nada externo** — ni de la base de datos, ni del framework web, ni de ningún servicio. Todo lo externo se conecta a través de interfaces llamadas Puertos (Ports).

```
[Adaptadores Entrantes]      [Núcleo del Sistema]        [Adaptadores Salientes]

REST API ─────────────┐
                      │  ┌────────────────────┐  ┌─ PostgreSQL  (ISampleRepository)
gRPC ─────────────────┼─▶│  Application       │─▶│─ Redis       (ICachePort)
                      │  │  Domain            │  │─ Email       (INotificationPort)
                      ┘  └────────────────────┘  └─ PDF         (IPdfPort)
```

**¿Por qué esto importa?**
- Podés cambiar PostgreSQL por MongoDB sin tocar una línea de lógica de negocio
- Podés cambiar el email por SMS sin tocar los handlers
- El dominio se puede testear completamente sin levantar base de datos ni servicios externos

---

## Estructura de proyectos

| Proyecto | Tipo | Qué contiene |
|----------|------|-------------|
| **LabResults.Domain** | Biblioteca | Reglas de negocio puras: Sample aggregate, Value Objects, Domain Events, interfaces de Puertos |
| **LabResults.Application** | Biblioteca | Casos de uso: Commands, Queries, Handlers MediatR, ValidationBehavior |
| **LabResults.Infrastructure** | Biblioteca | Implementaciones concretas: EF Core + PostgreSQL, Redis, Email (stub), PDF (stub) |
| **LabResults.API** | Web API | Adaptador REST: 10 endpoints Minimal APIs + Swagger |
| **LabResults.GrpcService** | gRPC | Adaptador gRPC para técnicos de laboratorio |
| **LabResults.Tests** | xUnit | 16 tests unitarios: Domain (11) + Application handlers (5) |

---

## Ciclo de vida de una muestra

```
Recibida ──[CargarResultado]──▶ Completada ──[Validar]──▶ Validada ──[Notificar]──▶ Notificada
   └──────────────────────────────────────────────────[Rechazar]──▶ Rechazada
```

Cada transición dispara un **Evento de Dominio**:

| Evento | Cuándo se dispara |
|--------|------------------|
| `SampleReceivedEvent` | Al crear la muestra |
| `ResultCompletedEvent` | Al cargar el resultado |
| `ResultValidatedEvent` | Al validar el médico |
| `PatientNotifiedEvent` | Al notificar al paciente |

---

## Patrones aplicados

| Patrón | Dónde | Para qué |
|--------|-------|---------|
| Arquitectura Hexagonal | Toda la solución | Desacoplar negocio de infraestructura |
| DDD (Domain-Driven Design) | Domain layer | Modelar el negocio con lenguaje ubicuo |
| CQRS | Application layer | Separar escrituras (Commands) de lecturas (Queries) |
| Value Objects | `PatientId`, `SampleCode`, `ResultValue`, `Email` | Encapsular validaciones e invariantes |
| Aggregate Root | `Sample` | Garantizar consistencia dentro del agregado |
| Repository Pattern | `ISampleRepository` | Abstraer la persistencia del dominio |
| Pipeline Behavior | `ValidationBehavior` | Validación automática antes de cada comando |
| Factory Method | `Sample.Create()`, `SampleCode.Generate()` | Forzar invariantes en la construcción |

---

## Value Objects destacados

**`SampleCode`** — no es un simple string. Fuerza el formato `LAB-YYYY-XXXXXX`:
```csharp
// SampleCode.Generate() produce automáticamente:
"LAB-2026-699191"

// SampleCode.Create("invalido") lanza excepción
// porque el formato no cumple el patrón del dominio
```

**`ResultValue`** — calcula el status automáticamente:
```csharp
// Con numeric=5.5, referenceMin=3.9, referenceMax=6.1
resultValue.ResultStatus // → "Normal"
resultValue.IsNormal     // → true

// Con numeric=7.0, referenceMax=6.1
resultValue.ResultStatus // → "High"
resultValue.IsNormal     // → false
```

---

## Endpoints REST

| Método | Ruta | Descripción | Actor |
|--------|------|-------------|-------|
| `POST` | `/api/samples` | Crear nueva muestra | Técnico |
| `POST` | `/api/samples/{id}/result` | Cargar resultado de análisis | Técnico |
| `POST` | `/api/samples/{id}/validate` | Médico valida el resultado | Médico |
| `POST` | `/api/samples/{id}/reject` | Rechazar muestra | Supervisor |
| `POST` | `/api/samples/{id}/notify` | Notificar al paciente | Sistema |
| `GET` | `/api/samples/{id}` | Obtener muestra por ID | Cualquiera |
| `GET` | `/api/samples/code/{code}` | Buscar por código `LAB-YYYY-XXXXXX` | Cualquiera |
| `GET` | `/api/patients/{id}/samples` | Historial de un paciente | Paciente/Médico |
| `GET` | `/api/samples/pending-validation` | Lista para el médico | Médico |
| `GET` | `/api/samples/{id}/pdf` | Descargar informe PDF | Paciente/Médico |

---

## Tests

```bash
dotnet test tests/LabResults.Tests
```

```
Test run for LabResults.Tests
  Passed: 16 / 16

Domain Tests (11):
  ✓ Sample_Create_ShouldGenerateCode
  ✓ Sample_Create_ShouldSetStatusReceived
  ✓ Sample_AddResult_ShouldTransitionToCompleted
  ✓ Sample_AddResult_ShouldSetResultStatus
  ✓ Sample_Validate_ShouldTransitionToValidated
  ✓ ResultValue_Normal_WhenInRange
  ✓ ResultValue_High_WhenAboveRange
  ✓ ResultValue_Low_WhenBelowRange
  ✓ SampleCode_ShouldMatchFormat
  ✓ PatientId_ShouldRejectEmptyGuid
  ✓ Sample_DomainEvents_ShouldBeRaised

Application Handler Tests (5):
  ✓ SubmitSample_ShouldAddToRepository
  ✓ AddResult_ShouldUpdateRepository
  ✓ AddResult_SampleNotFound_ShouldThrow
  ✓ ValidateResult_ShouldUpdateRepository
  ✓ NotifyPatient_ShouldCallNotificationPort
```

Los tests de dominio corren en **memoria pura** — sin base de datos, sin servicios externos. Los de Application usan mocks con NSubstitute. Todo el suite corre en menos de 100ms.

---

## Cómo ejecutarlo

### Requisitos
- .NET 8 SDK
- Docker Desktop

### Con Docker (recomendado)
```bash
git clone https://github.com/Vladimir-Bulan/LabResults.git
cd LabResults
docker compose up postgres redis -d
dotnet ef database update --project src/LabResults.Infrastructure --startup-project src/LabResults.API
dotnet run --project src/LabResults.API
# Swagger: http://localhost:5151/swagger
```

### Correr los tests
```bash
dotnet test tests/LabResults.Tests
# Resultado: 16/16 tests pasando
```

---

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| Runtime | .NET 8 |
| API REST | ASP.NET Core Minimal APIs + Swashbuckle |
| gRPC | Grpc.AspNetCore |
| CQRS | MediatR 12 + ValidationBehavior |
| Validación | FluentValidation 12 |
| Base de datos | PostgreSQL 16 + Entity Framework Core 8 + Npgsql |
| Caché | Redis 7 + StackExchange.Redis |
| Tests | xUnit + FluentAssertions + NSubstitute |
| Contenedores | Docker Compose |
| CI | GitHub Actions |