Quiero autenticación OAuth2 con el flujo client_credentials usando Keycloak como servidor de identidad en Docker; la API NO emite tokens, solo los valida. Debe poder probarse todo desde Swagger.

Keycloak y Docker:
- Servicio keycloak en docker-compose (modo desarrollo) con un realm "inventory" importado automáticamente desde ./keycloak/inventory-realm.json; el secreto del cliente llega por variable de entorno y nunca está escrito en el repositorio.
- Cliente de servicio "inventory-client" (client credentials) con dos client scopes opcionales: inventory.read e inventory.write. El cliente puede pedir uno o ambos.
- El token trae el claim aud con el valor "inventory-api", el claim scope (separado por espacios) y azp con el id del cliente.
- El emisor (iss) es siempre http://localhost:8081/realms/inventory, aunque la API consulte a Keycloak por la red interna (keycloak:8080). Requiere KC_HOSTNAME con la URL completa y KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true.

Validación en la API:
- JwtBearer con MetadataAddress, emisor y audiencia configurados por variables de entorno (Auth__MetadataAddress, Auth__Issuer, Auth__Audience). La API no arranca si falta alguna.
- Desactivar el mapeo automático de claims entrantes. El id del cliente sale del claim azp.
- Todos los endpoints de /api exigen token. Las lecturas piden inventory.read y las escrituras inventory.write (401 sin token o con token inválido, caducado, de otro emisor o de otra audiencia; 403 sin el permiso).

Probar desde Swagger:
- Swagger UI con el botón Authorize con el flujo OAuth2 client credentials contra el endpoint de token de Keycloak, con los scopes inventory.read e inventory.write, y como alternativa pegar un token Bearer.
- GET /api/auth/whoami: con cualquier token válido responde 200 con el id del cliente y los scopes.
- GET /api/auth/check-read (exige inventory.read) y GET /api/auth/check-write (exige inventory.write): endpoints de diagnóstico para ver 401, 403 y 200 antes de tener los de negocio.
- Verificación: con un token solo de lectura, check-read da 200 y check-write da 403; sin token ambos dan 401.

La lógica propia de este cambio es solo ScopeSet (comprobar un scope dentro del claim); se hace con TDD. Lo demás es configuración y se verifica con Docker.

Fuera de alcance: usuarios finales, roles, refresh tokens, HTTPS, Keycloak en modo producción, base de datos externa para Keycloak. Registra en design.md las decisiones sobre hostname, audiencia, scopes opcionales y el destino de los endpoints de diagnóstico.

Sí: Keycloak reemplaza por completo la emisión propia de tokens y todo va dentro de este mismo cambio. Incluye en tasks.md la actualización de: docs/architecture.md, docs/api-contract.md, docs/decisions.md (la decisión de OAuth2), AGENTS.md, openspec/config.yaml, docker-compose.yml y .env.example, quitando SigningKey, ClientSecret propio y el "servicio de tokens".

Decisiones:
1. Audiencia: mapper de audiencia en el cliente inventory-client (no en los scopes, que son opcionales), con included.custom.audience "inventory-api".
2. Cuerpo de 401 y 403: vacío (comportamiento por defecto de JwtBearer). Documéntalo en api-contract.md; sin ProblemDetails para estos dos códigos.
3. ScopeSet va en Inventory.Application/Security/ (lógica pura, sin paquetes web). ScopePolicies queda en Inventory.Api/Authorization/ y usa ScopeSet. Corrige architecture.md en consecuencia.
4. Quita Swashbuckle.AspNetCore.ApiTesting; deja solo Swashbuckle.AspNetCore 6.5.0. No añadas paquetes sin preguntarme.
5. Borra app.UseHttpsRedirection(): el contenedor solo habla HTTP y HTTPS está fuera de alcance.
6. check-read y check-write se mantienen hasta cerrar el entregable 3; se eliminan en la tarea de limpieza de ese cambio, cuando POST /api/categories permita verificar el 403. Regístralo en design.md.

Antes de implementar, actualiza proposal, specs, design y tasks con estas decisiones y muéstramelos para que los revise.