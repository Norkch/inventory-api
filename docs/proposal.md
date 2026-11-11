Quiero proveedores, su asociación con productos y el proveedor en las entradas de inventario.

Proveedores en /api/suppliers: POST, GET {id}, GET paginado, PUT y DELETE (desactiva).
- Nombre obligatorio, se recorta, máximo 200, no único.
- NIT obligatorio, se recorta, máximo 30; único entre todos los proveedores, también los
  desactivados, sin distinguir mayúsculas (BR-08). No se edita: el PUT lo ignora.
- Correo opcional, máximo 200, con formato algo@dominio.ext (por ejemplo juan@mail.com).
- Teléfono opcional, máximo 30: "+" inicial opcional, dígitos, espacios y guiones, de 7 a 15 dígitos.
- Correo y teléfono se recortan, una cadena vacía es null y se guardan tal cual.
- POST responde 201 con Location. PUT reemplaza nombre, correo y teléfono y fija updatedAtUtc.
- El listado devuelve solo activos, ordenados por nombre e id, paginado (BR-18), sin búsqueda.
- Un proveedor desactivado es invisible: GET {id}, PUT y DELETE responden 404. No se reactiva (BR-09).
  Desactivarlo conserva sus asociaciones.

Asociación producto-proveedor (muchos a muchos), identificada por la pareja:
- POST /api/suppliers/{productId}/suppliers con supplierId. Y supplierSku opcional (máximo 50).
  Sin FK, se comprueba en este orden: formato 400, producto de la ruta existente y activo 404,
  proveedor existente y activo 400 (BR-09), pareja repetida 409 (BR-10).
  Responde 201 con Location /api/products/{productId}/suppliers/{supplierId}.
- GET /api/products/{productId}/suppliers/{supplierId}.
- GET /api/products/{productId}/suppliers paginado, ordenado por nombre del proveedor.
- GET /api/suppliers/{supplierId}/products paginado, ordenado por nombre del producto (vista inversa).
- Solo se ven asociaciones con producto y proveedor activos. Un producto o proveedor no activo
  en la ruta de un listado da 404.
- La asociación no se edita ni se borra. El alta del producto no asocia proveedores.

Además MODIFICA la capacidad inventory-movements (BR-16):
- Una entrada puede indicar supplierId; el proveedor debe estar activo y asociado al producto (400).
- Una salida con supplierId responde 400 (hoy se ignora).
- Orden: cuerpo 400, producto 404, proveedor 400, stock 409, dentro de la misma transacción.
- El movimiento devuelve supplierId (o null).
