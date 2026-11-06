# Inventory API

API REST de inventario (categorías, productos , movimientos y proveedores.

## Stack
.NET 6 · ASP.NET Core · SQL Server (Docker) · Dapper (escrituras) · EF Core (lecturas) · OAuth2 client_credentials · xUnit

## Cómo ejecutarlo

## Método de trabajo
Desarrollo guiado por especificaciones con OpenSpec: cada cambio tiene propuesta, especificación, diseño y tareas en `openspec/changes/`. Cada entregable usa un issue, una rama, un pull request y un tag.

## Declaración de uso de IA
Usé Claude Code como asistente de desarrollo. Las especificaciones se redactaron en español con OpenSpec y las revisé antes de implementar. Cada tarea se hizo con TDD y se verificó con compilación, pruebas y una auditoría de Clean Code. La revisión de calidad la hizo una sesión o subagente distinto del que escribió el código. El detalle por entregable está en [`docs/ai-usage.md`](docs/ai-usage.md). Soy responsable del código entregado.

## Decisiones y supuestos
Ver `docs/decisions.md` y `docs/assumptions.md`. 
