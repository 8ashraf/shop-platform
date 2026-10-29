# 0003. Schema per module, in-process integration events

- Status: Accepted

## Context
Shared tables create hidden coupling between modules.

## Decision
Each module owns a SQL Server schema named after it, its own `DbContext` and its own
migrations history table. No module queries another module's schema. Cross-module
communication uses `IEventBus` with in-process delivery for now.

## Consequences
- Modules can evolve their data independently.
- Reporting across modules needs read models or a reporting store.
- Delivery is not durable yet; adopt an outbox before relying on events for money movement.
