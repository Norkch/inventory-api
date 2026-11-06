---
name: tdd-cycle
description: Implementa una sola tarea de tasks.md con TDD estricto (rojo, verde, refactorizar). Usar al empezar una tarea de implementación concreta, una por sesión.
argument-hint: "[número de tarea, p. ej. 2.3]"
---
# Ciclo TDD

Trabaja una sola tarea por sesión. No toques archivos fuera de la tarea. No hagas commit.

Tarea solicitada: $ARGUMENTS

1. Elige la tarea: si se indicó un número arriba, usa esa tarea. Si no, toma la primera tarea sin marcar en `openspec/changes/<cambio>/tasks.md`. Si hay más de un cambio activo, pregunta cuál.
2. Lee solo lo necesario: la tarea, el escenario de la especificación que implementa y los archivos que vas a tocar o de los que dependes directamente (entidades, puertos, fakes existentes).
3. ROJO: escribe la prueba con el nombre del escenario (`Rejects_...`, `Creates_...`).
   - Usa fakes escritos a mano. No uses librerías de mocks.
   - Si el tipo o método bajo prueba aún no existe, crea un esqueleto mínimo (por ejemplo, un método que lance `NotImplementedException`) para que la prueba compile.
   - Ejecuta `dotnet test --filter <NombrePrueba>` y confirma que **falla en la aserción por la razón esperada**. Si falla por otra causa (por ejemplo, un error de compilación ajeno), corrígelo antes de seguir.
4. VERDE: escribe el código mínimo que hace pasar la prueba, nada más. Respeta CQRS: los comandos usan puertos de escritura (Dapper) y las consultas usan puertos de lectura (EF Core). Ejecuta `dotnet test --filter <NombrePrueba>`.
5. REFACTORIZAR: mejora nombres y estructura sin cambiar el comportamiento. Las pruebas siguen en verde.
6. Verifica todo: ejecuta `dotnet build` y `dotnet test` (suite completa) para detectar regresiones.
7. Si tuviste que asumir algo, regístralo en `docs/assumptions.md`.
8. Marca la tarea como hecha en `tasks.md` y resume en tres líneas: qué prueba escribiste, qué código añadiste y qué comandos ejecutaste con su resultado.
9. Recomienda ejecutar `/clean-code-review` en una sesión nueva antes del commit.

Si el escenario es ambiguo o contradice otra regla, detente y pregunta. No inventes requisitos.
