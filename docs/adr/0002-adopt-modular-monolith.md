# 0002. Adopt a modular monolith

- Status: Accepted

## Context
We need clear business boundaries and team ownership without the operational cost of
microservices at this stage.

## Decision
Ship one deployable (`Shop.Host`) composed of modules under `src/Modules`.
Each module has Domain, Application, Infrastructure, Presentation and IntegrationEvents
projects. Modules talk to each other only through IntegrationEvents. Boundaries are
enforced by `Shop.ArchitectureTests`.

## Consequences
- Single deployment and transaction boundary; simple local development.
- A module can be extracted into a service later by replacing the in-memory event bus.
- Discipline is required: the architecture tests must stay green.
