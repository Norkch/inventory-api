# Uso de IA en este proyecto

## Herramientas
- Claude Code (modelo: Opus 5.5) para planificación, redacción de especificaciones, implementación y revisión.
- Claude (chat) para estudiar el enunciado y preparar la guía de trabajo.
- OpenSpec para el flujo guiado por especificaciones.
- Context7 (MCP), para documentación xterna actualizada. 

## Principios
- Yo decido; la IA propone. Cada diff lo reviso antes de hacer commit.
- Ninguna especificación se implementa sin que yo la haya revisado.
- La revisión la hace una sesión o subagente distinto del que implementó.
- Soy responsable del 100 % del código entregado.

## Bitácora por entregable
| Entregable | Qué hizo la IA | Cómo lo validé | Qué corregí o rechacé | Estrategia de tokens |
|---|---|---|---|---|
| Configuración de Claude Code | Propuso AGENTS.md, 3 skills y 1 subagentes | Probé con `/memory`, `/agents` y preguntas de comprobación | Ajuste el llamado de AGENTS.md desde CLAUDE.md | Una tarea por sesión |
| Productos (`add-products`) | En `/opsx:explore` contrastó `docs/proposal.md` con el esquema, las reglas y el código de categorías, y planteó los puntos abiertos: SKU de un producto borrado, carrera con la desactivación de la categoría, `PUT` con `sku`/`stock`, `search` y borrado con stock. Con `/opsx:propose` generó la propuesta, la spec (14 requisitos), el diseño (11 decisiones) y las tareas (22). Con `/opsx:apply` implementó con TDD, verificó en Docker (incluida la concurrencia con `sqlcmd`) y documentó. El subagente `code-reviewer` revisó el diff | Tomé cada decisión abierta en la exploración (SKU reservado para siempre, collation explícita, ignorar `sku` y `stockQuantity` en el `PUT`, `search` por nombre o SKU, borrado con stock congelado) y acepté los valores por defecto propuestos. Revisé los artefactos antes de implementar. Validé los endpoints desde Swagger UI. `dotnet test` 213/213 y verificación manual de cada escenario en `docs/bitacora.md` | Pedí quitar los precios del alcance del proyecto (propuesta, `config.yaml`, `architecture.md` y BR-09). La revisión corrigió 4 hallazgos menores, uno de ellos un fallo real en el mensaje de parámetro inválido; los otros 2 quedaron registrados en `docs/assumptions.md` | Exploración, propuesta e implementación en una sola sesión, con preguntas de una en una. La revisión, en un subagente con contexto nuevo. Context7 solo para las APIs de EF Core 6 y Dapper que se usaron |

## Decisiones sobre la IA
- Escribí mis propias skills con ayuda de claude en lugar de descargar skills ajenas.
- Los artefactos de OpenSpec están en español; el código, en inglés.