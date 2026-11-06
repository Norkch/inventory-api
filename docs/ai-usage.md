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

## Decisiones sobre la IA
- Escribí mis propias skills con ayuda de claude en lugar de descargar skills ajenas.
- Los artefactos de OpenSpec están en español; el código, en inglés.