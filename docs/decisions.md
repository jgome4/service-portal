# Decisiones, supuestos y riesgos

## Persistencia SQL y concurrencia

Problema: los reenvíos y múltiples workers pueden duplicar solicitudes o efectos. Se consideró una cola en memoria (pierde datos) y SQL Server (exige infraestructura). SQLite ofrece SQL transaccional real, restricción única por referencia y despliegue local sencillo. Se inicializa con DDL idempotente; cambios futuros requieren migraciones versionadas.

Create usa transacción inmediata y `ON CONFLICT DO NOTHING`; lee y compara el payload antes de confirmar. Igual referencia y distinto payload es 409. La comparación usa cadenas recortadas, sensibles a mayúsculas; la unicidad es global, supuesto documentado porque el anexo no define ámbito por cliente.

Take reclama una fila dentro de una transacción de escritura, incrementa intentos y entrega un token con lease de 30 segundos. Otro proceso no puede reclamarla hasta expirar. Finish exige el token vigente: un worker antiguo no puede sobrescribir al nuevo. Processing abandonados se recuperan tras reinicio. Completed es terminal tanto en la aplicación como en un trigger de BD.

## Efecto del proveedor

El simulador registra en `provider_effects` una referencia única y un recibo. Repetir la invocación devuelve el éxito previamente persistido. La actualización de la solicitud ocurre después, en una transacción distinta: las pruebas reproducen la caída entre el efecto y esa actualización. No se afirma exactamente una llamada: puede repetirse la llamada, pero el simulador no repite el efecto.

Un proveedor real debe ofrecer una clave de idempotencia persistente o consulta/reconciliación por referencia. Un registro local por sí solo no puede garantizar ausencia de duplicados en un sistema remoto. En producción aislar el simulador detrás de una interfaz/adaptador, exigir ese contrato, añadir timeout, cancelación y telemetría; no sustituirlo por HTTP sin esa garantía.

## Reintentos y proceso

Máximo tres respuestas transitorias, con esperas de 2 y 4 segundos antes de los siguientes intentos. Después, Failed es terminal; la prueba no pide reproceso manual. Las interrupciones de infraestructura recuperan el lease y pueden incrementar intentos más allá de tres: se privilegia reconciliar un posible efecto persistido. Una indisponibilidad sostenida de infraestructura necesita alertas y límites operativos adicionales.

Un BackgroundService alojado con la API reduce proyectos y facilita demostrar el flujo. La tarea se ejecuta asincrónicamente; ninguna transacción queda abierta durante la llamada al simulador. El retraso simulado es de 150 ms, inferior al lease. Para llamadas remotas largas se debe renovar el lease o imponer timeouts inferiores a él. SQLite serializa escrituras y no está orientado a alta concurrencia ni a archivos compartidos en red.

## Alternativa NoSQL (4 líneas)

Se usaría una clave única de documento basada en externalReference para preservar idempotencia.
El claim y Finish requerirían compare-and-swap/ETag y condición sobre estado y token.
El recibo del proveedor mantendría una clave de idempotencia propia y persistente.
Cambiarían las consultas, los índices, la inicialización y las pruebas; las reglas del dominio se conservan.

## Seguridad y alcance

No se exige autenticación: se limita el ejemplo a loopback y no se habilita CORS. No publicar en una red compartida sin autenticación, autorización por cliente, HTTPS, rate limiting y retención definida. Se usan parámetros SQL, límites de entradas, errores públicos genéricos y cabeceras de seguridad. React escapa el texto; no se inyecta HTML. No hay secretos en el repositorio. La interfaz usa estilos locales, soporte de teclado, mensajes accesibles y adaptación móvil.

La separación mínima es dominio/validación, almacenamiento transaccional, worker y endpoints. No se agregaron microservicios, repositorios genéricos, Docker ni infraestructura que desplace el objetivo. Las dependencias de producción son React y SQLite; Vite compila los archivos estáticos.
