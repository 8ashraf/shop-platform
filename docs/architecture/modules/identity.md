# Identity module

| | |
|---|---|
| Code | `src/Modules/Identity` |
| Schema | `identity` |
| Route prefix | `/api/identity` |
| Owners | `@8ashraf/identity-team` |

## Responsibility
What this module owns — and what it explicitly does not.

## Public contract
Integration events published (`Shop.Modules.Identity.IntegrationEvents`):

| Event | When | Consumers |
|---|---|---|

Integration events consumed:

| Event | From module | Reaction |
|---|---|---|

## Data model

```mermaid
erDiagram
    EXAMPLE_ENTITY {
        uniqueidentifier Id PK
        nvarchar Name
        datetime2 CreatedOnUtc
    }
```
