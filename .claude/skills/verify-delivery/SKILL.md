---
name: verify-delivery
description: Verifica que el repositorio funciona desde un clon limpio, siguiendo solo el README. Usar antes de la entrega y al cerrar cada entregable.
context: fork
---
# Verificación de la entrega

Actúa como un evaluador que ve el proyecto por primera vez. No uses conocimiento previo del código: solo el README.

No modifiques nada en la carpeta de trabajo. Solo puedes escribir dentro del clon temporal (por ejemplo, su `.env`).

1. Antes de clonar, revisa la carpeta de trabajo con `git status` y `git log origin/main..` (si existe un remoto). Reporta los cambios sin commit o sin push: no estarán en el clon.
2. Clona el repositorio en una carpeta temporal vacía, fuera de la carpeta de trabajo. Si existe el remoto `origin`, clona desde él. Si no, clona desde la ruta local.
3. Comprueba con `git ls-files` que no haya ningún `.env` versionado.
4. Sigue el `README.md` del clon **al pie de la letra**, paso a paso, y anota cada paso que obligue a adivinar algo. Crea el `.env` únicamente a partir de `.env.example` y de lo que indique el README, sin copiar nada de la carpeta de trabajo. Comprueba que `.env.example` incluya todas las variables que usan `docker-compose.yml` y la aplicación.
5. Ejecuta `dotnet build` y `dotnet test --collect:"XPlat Code Coverage"`. Informa el resultado y el porcentaje de cobertura.
6. Antes de levantar los contenedores, revisa si los puertos que usa `docker-compose.yml` ya están ocupados y repórtalo. Ejecuta `docker compose -p verify-delivery up -d --build --wait` con un límite de 3 minutos. Si falla, incluye `docker compose -p verify-delivery ps` y las últimas líneas de `docker compose -p verify-delivery logs`.
7. Prueba la API:
   - Swagger responde.
   - `/connect/token` con credenciales válidas devuelve un token, y con credenciales inválidas se rechaza.
   - Un endpoint protegido sin token devuelve 401 y con un token válido devuelve 200.
   - Un token con solo `inventory.read` en un endpoint de escritura devuelve 403.
   - Flujo de punta a punta: crea un recurso (escritura) y luego léelo (lectura) para confirmar que el esquema de la base se inicializa y que ambos caminos funcionan.
8. Limpia: `docker compose -p verify-delivery down -v` y borra la carpeta temporal.
9. Informa con una tabla de paso, resultado (OK / FALLA / NO APLICA) y evidencia (comando y salida resumida). Si el entregable todavía no tiene README, docker-compose o algún endpoint, márcalo como NO APLICA en lugar de fallar sin explicación. Después de la tabla:
   - Lista los pasos donde hubo que adivinar.
   - Para cada fallo, da la corrección exacta del README.
10. Termina con **APROBADO** o **RECHAZADO**, y una línea con el total de fallos.