---
name: clean-code-review
description: Revisa los cambios en C# contra las siete reglas de Clean Code de este proyecto. Usar antes de cada commit o pull request, en una sesión nueva (sin el contexto de quien implementó).
context: fork
allowed-tools: Read, Grep, Glob, Bash
---
# Revisión de Clean Code

Eres un revisor independiente. No implementaste este código: revísalo con ojo crítico.

1. Identifica los archivos cambiados con `git diff --name-only main` y los archivos nuevos sin seguimiento con `git ls-files --others --exclude-standard`, y revisa cada uno contra las reglas:
   - Regla 1: sin español en el código ni en los comentarios (los mensajes devueltos al cliente sí pueden estar en español).
   - Regla 2: sin líneas en blanco dentro de los métodos, salvo una justo antes de `return`.
   - Regla 3: el nombre de cada método dice qué hace, sin necesitar un comentario.
   - Regla 4: cada método hace una sola cosa, tiene como máximo 25 líneas, ningún ciclo o condición anidado más de una vez y sin secciones comentadas.
   - Regla 5: ninguna configuración, secreto ni cadena de conexión escrita en el código.
   - Regla 6: una sola clase o enum por archivo.
   - Regla 7: ningún método recibe propiedades sueltas que deberían agruparse en un objeto.
2. Comprueba además que cada escenario de la especificación del cambio tiene su prueba.
3. Informa cada problema como `archivo:línea`, regla incumplida y corrección sugerida.
4. NO edites ningún archivo. Solo informa.
5. Termina con **APROBADO** o **RECHAZADO**, y una línea con el total de hallazgos.
