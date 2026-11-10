# Contrato HTTP

Base local: `http://127.0.0.1:5080`. JSON camelCase, estados en inglés. Fechas `createdAt` en milisegundos Unix UTC. No hay endpoint público para modificar estados.

| Método/ruta | Comportamiento |
| --- | --- |
| POST /api/requests | Crea solicitud: 201 y Location; reenvío idéntico: 200; misma referencia con datos distintos: 409 |
| GET /api/requests?status=Pending&limit=20&offset=0 | Lista por estado opcional. limit entre 1 y 100, offset >= 0; orden descendente por creación e ID |
| GET /api/requests/{id} | Detalle, o 404 |
| GET /api/health | Disponibilidad del proceso; no es prueba de escritura de la BD |

POST requiere `externalReference` (100), `customerId` (100), `serviceCode` (50), `description` (2000). Entre paréntesis está el máximo de caracteres. No admite valores vacíos ni caracteres de control. Se recortan espacios en extremos. Referencias sensibles a mayúsculas, únicas globalmente para esta demostración.

```json
{"externalReference":"RETRY-001","customerId":"CLIENTE-42","serviceCode":"VISITA","description":"Revisar instalación"}
```

Estados: Pending, Processing, Completed y Failed. Respuesta: `id`, los cuatro campos enviados, `status`, `attempts`, `createdAt`, `lastError` nullable. El listado devuelve un array. Filtros o paginación inválidos: 400. Validación: 400 con ProblemDetails/errors. JSON malformado: 400; cuerpos mayores de 16 KiB: 413. Fallos inesperados: 500 sin detalles internos en Production. No registrar descripciones o identificadores del cliente en logs.
