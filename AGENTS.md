# Inventory API

API REST para gestionar productos, categorías, proveedores y movimientos de inventario.

## Stack
- .NET 6 (net6.0), controladores de ASP.NET Core, C# 10 (no usar funciones de C# 11+: miembros `required`, cadenas raw, patrones de lista, constructores primarios, expresiones de colección)
- Los paquetes NuGet deben ser compatibles con la versión 6.x (EF Core 6, JwtBearer 6, Swashbuckle 6). Preguntar antes de añadir cualquier paquete
- SQL Server 2022 en Docker. SIN llaves foráneas: la integridad se garantiza en la aplicación
- Escrituras: Dapper. Lecturas: Entity Framework Core (AsNoTracking)
- Autenticación: OAuth2 client_credentials. Keycloak (Docker) emite los JWT; la API solo los valida (JwtBearer). Scopes inventory.read e inventory.write
- Pruebas: xUnit y fakes escritos a mano. Cobertura con Coverlet
- Docker y docker-compose

## Arquitectura
Capas: Domain <- Application <- Infrastructure <- Api. Las dependencias apuntan solo hacia adentro.
- Domain: entidades, enums, excepciones de dominio, Guard. Sin paquetes.
- Los movimientos de inventario son el libro inmutable del stock: nunca se actualizan ni se borran. 
- Application: comandos, consultas, handlers, puertos (interfaces), DTOs.
- Infrastructure: repositorios de escritura con Dapper, repositorios de lectura con EF Core, opciones.
- Api: controladores, manejo de excepciones, Swagger, validación de JWT contra Keycloak y políticas de scope, raíz de composición.
CQRS: los handlers de comandos usan puertos de escritura (Dapper). Los handlers de consultas usan puertos de lectura (EF Core). Nunca se mezclan.
Sin MediatR: los handlers son clases explícitas.

## Reglas de Clean Code (obligatorias)
1. Todo el código y los comentarios en inglés. El español solo en los mensajes devueltos al cliente.
2. Sin líneas en blanco dentro de los métodos, salvo una justo antes de `return`.
3. Nombres de métodos autoexplicativos. Ningún comentario debe hacer falta para explicar qué hace un método.
4. Responsabilidad única: los métodos tienen como máximo 25 líneas, ningún ciclo o condición anidado más de una vez, sin secciones comentadas.
5. La configuración se lee solo de variables de entorno. Nunca poner secretos ni cadenas de conexión en el código.
6. Una clase o enum por archivo.
7. Pasar objetos, no propiedades sueltas, como parámetros.

## Flujo de trabajo (guiado por especificaciones)
1. Trabajar solo desde un cambio de OpenSpec en openspec/changes/. No inventar requisitos. Los artefactos de OpenSpec se escriben en español (los encabezados estructurales y SHALL/WHEN/THEN se mantienen en inglés).
2. Si algo es ambiguo, preguntar. Registrar los supuestos en docs/assumptions.md.
3. Escribir primero la prueba que falla, luego el código mínimo para que pase y después refactorizar.
4. Hacer una tarea a la vez de tasks.md. No tocar archivos fuera de la tarea.
5. Tras cada tarea ejecutar: dotnet build, dotnet test.
6. No añadir paquetes NuGet sin preguntar.

## Comandos
- Compilar: dotnet build
- Probar: dotnet test
- Levantar todo: docker compose up -d --build

## Límites
- Nunca subir .env ni secretos. Actualizar .env.example al añadir una variable.
- Los movimientos de inventario son inmutables: sin actualizar, sin borrar.
- Terminar cada respuesta con un breve resumen de lo que cambió y de lo que se verificó.

## Documentación actualizada
- Antes de usar una API de EF Core 6, Dapper, JwtBearer 6 o Swashbuckle 6, consultar Context7 con la versión indicada en el stack. No asumir la API de memoria.
- Si Context7 devuelve documentación de otra versión mayor, ignorarla y avisar.