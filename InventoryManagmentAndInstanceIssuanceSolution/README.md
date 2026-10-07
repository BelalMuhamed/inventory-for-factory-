# Inventory Management & Instance Issuance

A multi-tenant inventory platform API built with ASP.NET Core, following **Onion Architecture** and strict Clean Code / SOLID principles.

> **Target framework:** .NET 8 (`net8.0`). The endpoint specification references .NET 10; the codebase currently targets net8.0 and has not been retargeted.

---

## Architecture

The solution follows Onion Architecture. Dependencies point **inward only** — outer layers depend on inner layers, never the reverse.

```
Presentation  ─────►  Application  ─────►  Domain
     │                     ▲                  ▲
     └──────► Infrastructure ──────────────────┘
```

| Layer | Project | Responsibility | Depends on |
|-------|---------|----------------|------------|
| **Domain** | `DomainLayer` | Entities, value objects, outcome primitives (`Result`, `Error`). Zero dependencies. | — |
| **Application** | `ApplicationLayer` | Use cases / services, DTOs, repository & service contracts, `PaginatedResponse<T>`. | Domain |
| **Infrastructure** | `InfrastructureLayer` | EF Core `DbContext`, repository implementations, Unit of Work, external integrations. | Application, Domain |
| **Presentation** | `InventoryManagmentAndInstanceIssuancePresentationLayer` | API controllers, middleware, response envelope, DI composition root. | Application |

**Rule:** Infrastructure is never referenced from Domain or Application. The Presentation layer composes everything at startup.

---

## Core patterns

- **Result pattern** — operations return `Result` / `Result<T>` instead of throwing for business outcomes. A failure carries an `Error` (stable `Code` + human `Message` + `ErrorCategory`). Exceptions are reserved for unexpected faults.
- **Error → HTTP mapping** — `ErrorCategory` is the single source of truth for status codes:

  | Category | HTTP | Used for |
  |----------|------|----------|
  | `Validation` | 422 | Request body fails domain rules |
  | `Conflict` | 409 | Business-rule violation / concurrent update conflict |
  | `NotFound` | 404 | Resource missing (or belongs to another tenant) |
  | `Unauthorized` | 401 | Missing / expired / invalid credentials |
  | `Forbidden` | 403 | Authenticated but tenant mismatch |

- **Repository + Unit of Work** — all data access goes through named repository methods behind `IUnitOfWork`; raw query predicates never appear in the service layer.
- **Pagination** — list endpoints return `PaginatedResponse<T>`: `{ data, pageNumber, pageSize, totalCount, totalPages }`.
- **Soft delete** — most entities use `DeletedAt` / `IsDeleted` with global query filters and a `POST /{resource}/{id}/restore` endpoint. Sessions are hard-deleted.
- **Audit logging** — all write endpoints log to `AuditLogs` via an EF Core `SaveChanges` interceptor plus service-layer hooks for non-CRUD actions.

---

## Solution structure

```
InventoryManagmentAndInstanceIssuanceSolution.sln
├── DomainLayer/
│   └── Common/                 # Result, Result<T>, Error, ErrorCategory
├── ApplicationLayer/
│   ├── Common/                 # PaginatedResponse<T>
│   ├── Contracts/              # IUnitOfWork (+ repository contracts)
│   └── ServicesContracts/      # IServiceManager (service façade)
├── InfrastructureLayer/
│   └── Data/                   # AppDbContext, UnitOfWork
└── InventoryManagmentAndInstanceIssuancePresentationLayer/
    └── Program.cs              # composition root
```

---

## Getting started

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB, Express, or full)
- Visual Studio 2022 (17.14+) or `dotnet` CLI

### Configuration
Set the connection string in `appsettings.json` (or user secrets — never commit secrets):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=InventoryDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### Build & run
```bash
dotnet restore
dotnet build
dotnet run --project InventoryManagmentAndInstanceIssuancePresentationLayer
```

### Database migrations
Migrations are **source code** and are committed to the repository. From the solution root:

```bash
# Create a migration (DbContext lives in Infrastructure; startup project is Presentation)
dotnet ef migrations add <Name> \
  --project InfrastructureLayer \
  --startup-project InventoryManagmentAndInstanceIssuancePresentationLayer

# Apply migrations to the database
dotnet ef database update \
  --project InfrastructureLayer \
  --startup-project InventoryManagmentAndInstanceIssuancePresentationLayer
```

---

## Conventions

- **DI:** constructor injection only.
- **Async:** `async`/`await` for all I/O.
- **DTOs:** domain entities never cross into the Presentation layer.
- **XML docs:** on all public interfaces and methods; requirement IDs referenced where applicable (e.g. `REQ-STU-007`).
- **EF config:** Fluent API in `OnModelCreating`.

---

## Current state

Foundation primitives are in place: `Result` / `Result<T>`, `Error`, `ErrorCategory`, and `PaginatedResponse<T>`. The cross-cutting presentation concerns (response envelope, global exception middleware, structured logging with correlation IDs, and `422` model-validation handling) are the next milestone. Entities, the EF Core `DbContext`, repositories, services, and controllers are not yet implemented.
