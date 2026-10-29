# Shop

Modular monolith on .NET 10.0. One deployable (`Shop.Host`), many
independently-owned modules with hard boundaries enforced by architecture tests.

## Layout

```
.
├── docs/
│   ├── business/          BRD
│   ├── requirements/      SRD, use cases
│   ├── architecture/      C4, DFDs, ERDs (Mermaid), one page per module
│   └── adr/               Architecture Decision Records
├── src/
│   ├── Modules/<Module>/  Domain · Application · Infrastructure · Presentation · IntegrationEvents
│   ├── Shared/            Kernel · Application · Infrastructure (building blocks)
│   └── Host/              API host / composition root
├── tests/
│   ├── Modules/<Module>/  per-module unit tests
│   ├── Shop.ArchitectureTests
│   └── Shop.IntegrationTests
├── deploy/                docker, compose, k8s
└── .github/               path-filtered CI per module, CODEOWNERS
```

## Module rules

Inside a module:

| Layer | May reference |
|---|---|
| Domain | Shared.Kernel |
| Application | Domain, own IntegrationEvents, Shared.Application |
| Presentation | Application |
| Infrastructure | Application, Presentation, IntegrationEvents, Shared.Infrastructure (registers the module via `IModule`) |
| IntegrationEvents | Shared.Kernel only — this is the module's **public contract** |

Between modules: a module may reference **only another module's `IntegrationEvents`
project**. Anything else fails `Shop.ArchitectureTests`.

Each module owns its own database schema (`catalog`, `orders`, …) and its own `DbContext`
and migrations history table.

## Adding a module

```bash
./build/scripts/modmono.sh add-module Shipping
```

This creates the five module projects, a unit-test project, a docs page, a path-filtered
CI workflow and a CODEOWNERS entry; adds everything to the solution; and registers the
module in `src/Host/Shop.Host/ModuleCatalog.cs`.

## Commands

```bash
dotnet build
dotnet test
dotnet run --project src/Host/Shop.Host          # http://localhost:5080/swagger
docker compose -f deploy/docker-compose.yml up --build

# Vulnerable packages (pin fixes in the "Security pins" group of Directory.Packages.props)
dotnet list package --vulnerable --include-transitive

# Migrations for one module
dotnet ef migrations add Initial \
  --project src/Modules/Catalog/Shop.Modules.Catalog.Infrastructure \
  --startup-project src/Host/Shop.Host \
  --output-dir Database/Migrations
```
