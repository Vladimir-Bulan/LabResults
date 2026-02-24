# LabResults

![CI](https://github.com/Vladimir-Bulan/LabResults/actions/workflows/ci.yml/badge.svg) ![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet) ![Tests](https://img.shields.io/badge/tests-16%20passing-brightgreen) ![Architecture](https://img.shields.io/badge/arquitectura-hexagonal-blue)

> Sistema de gestión de resultados de laboratorio clínico, construido con Arquitectura Hexagonal pura (Ports & Adapters), DDD, CQRS, gRPC y .NET 8.

---

## ¿Qué hace este sistema?

LabResults gestiona el ciclo de vida completo de una muestra de laboratorio clínico:

1. El **técnico de laboratorio** ingresa una muestra (sangre, orina, etc.)
2. Se procesa y se carga el resultado con valores numéricos y rangos de referencia
3. Un **médico** valida el resultado (o lo rechaza)
4. El sistema notifica al **paciente** por email que su resultado está disponible
5. Se puede generar un **PDF** del informe

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
- El dominio se puede testear sin levantar base de datos ni servicios externos

---

## Estructura de proyectos

| Proyecto | Tipo | Qué contiene |
|----------|------|-------------|
| **LabResults.Domain** | Biblioteca | Las reglas de negocio puras: Sample, ValueObjects, Eventos de Dominio, interfaces de Puertos |
| **LabResults.Application** | Biblioteca | Casos de uso: Comandos, Queries, Handlers de MediatR, Validaciones |
| **LabResults.Infrastructure** | Biblioteca | Implementaciones concretas: EF Core + PostgreSQL, Redis, Email (stub), PDF (stub) |
| **LabResults.API** | Web API | Adaptador REST: endpoints con Minimal APIs + Swagger |
| **LabResults.GrpcService** | gRPC | Adaptador gRPC para técnicos de laboratorio |
| **LabResults.Tests** | xUnit | 16 tests unitarios: Domain + Application handlers |

---

## Ciclo de vida de una muestra

```
Recibida ──[CargarResultado]──▶ Completada ──[Validar]──▶ Validada ──[Notificar]──▶ Notificada
   └──────────────────────────────────────────────────[Rechazar]──▶ Rechazada
```

Cada transición dispara un **Evento de Dominio**:

| Evento | Cuándo se dispara |
|--------|------------------|
| SampleReceivedEvent | Al crear la muestra |
| ResultCompletedEvent | Al cargar el resultado |
| ResultValidatedEvent | Al validar el médico |
| PatientNotifiedEvent | Al notificar al paciente |

---

## Patrones aplicados

| Patrón | Dónde | Para qué |
|--------|-------|---------|
| Arquitectura Hexagonal | Toda la solución | Desacoplar negocio de infraestructura |
| DDD (Domain-Driven Design) | Domain layer | Modelar el negocio con lenguaje ubicuo |
| CQRS | Application layer | Separar escrituras (Commands) de lecturas (Queries) |
| Value Objects | PatientId, SampleCode, ResultValue, Email | Encapsular validaciones e invariantes |
| Aggregate Root | Sample | Garantizar consistencia dentro del agregado |
| Repository Pattern | ISampleRepository | Abstraer la persistencia del dominio |
| Pipeline Behavior | ValidationBehavior | Validación automática antes de cada comando |
| Factory Method | Sample.Create(), SampleCode.Generate() | Forzar invariantes en la construcción |

---

## Endpoints REST

| Método | Ruta | Descripción | Actor |
|--------|------|-------------|-------|
| POST | \/api/samples\ | Crear nueva muestra | Técnico |
| POST | \/api/samples/{id}/result\ | Cargar resultado de análisis | Técnico |
| POST | \/api/samples/{id}/validate\ | Médico valida el resultado | Médico |
| POST | \/api/samples/{id}/reject\ | Rechazar muestra | Supervisor |
| POST | \/api/samples/{id}/notify\ | Notificar al paciente | Sistema |
| GET | \/api/samples/{id}\ | Obtener muestra por ID | Cualquiera |
| GET | \/api/samples/code/{code}\ | Buscar por código LAB-YYYY-XXXXXX | Cualquiera |
| GET | \/api/patients/{id}/samples\ | Historial de un paciente | Paciente/Médico |
| GET | \/api/samples/pending-validation\ | Lista para el médico | Médico |
| GET | \/api/samples/{id}/pdf\ | Descargar informe PDF | Paciente/Médico |

---

## Cómo ejecutarlo

### Requisitos
- .NET 8 SDK
- Docker Desktop

### Con Docker (recomendado)
```bash
git clone https://github.com/Vladimir-Bulan/LabResults.git
cd LabResults
docker compose up -d

# API:     http://localhost:5010
# Swagger: http://localhost:5010/swagger
```

### Solo la base de datos + correr local
```bash
docker compose up postgres redis -d
dotnet ef database update --project src/LabResults.Infrastructure --startup-project src/LabResults.API
dotnet run --project src/LabResults.API
```

### Correr los tests
```bash
dotnet test tests/LabResults.Tests
# Resultado: 16 tests pasando
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

---

## Licencia
MIT
