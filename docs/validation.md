# Validación de la entrega

Fecha: 10 de noviembre de 2026.

| Comprobación | Resultado observado |
| --- | --- |
| Restauración .NET | Correcta utilizando los paquetes de la caché local |
| Compilación backend/pruebas | Correcta; última compilación: 0 errores, 0 advertencias |
| Runner directo de pruebas | 10 casos aprobados, 0 fallidos |
| Build React/Vite | Correcto; 15 módulos, JS 226,17 kB (71,08 kB gzip), CSS 5,51 kB |
| Inicio backend | Escucha en 127.0.0.1:5080, entorno Production |
| Navegador con API simulada | Render, envío, confirmación, filtro, consola sin errores, escritorio 1440 px y móvil 375 px sin desbordamiento horizontal |
| HTTP de extremo a extremo | Bloqueado por el entorno: conexión loopback denegada (EACCES) |
| VSTest/testhost | No completó en este entorno; se ejecutaron las mismas pruebas con runner directo |
| Windows Service real | Documentado; no instalado |

Los 10 casos cubren reenvío idéntico/conflicto, creación concurrente, claim concurrente, caída después del efecto, rechazo de worker antiguo, reintentos exitosos, límite de fallos, persistencia tras reapertura, trigger de estado terminal, filtros/paginación y validación (varios escenarios pertenecen al mismo caso). El total de 10 incluye dos conjuntos de datos de la prueba de reintentos.

El test HTTP reproducible se incluye en `tests/api-smoke.mjs`; ejecutar con una base de datos de prueba y la aplicación activa. No se afirma que haya pasado. Las verificaciones visuales usaron respuestas interceptadas en el navegador, no el backend real, y no demuestran integración de extremo a extremo.

La descarga desde NuGet/npm está restringida. .NET se restauró desde caché; para compilar React se reutilizaron paquetes locales de las versiones fijadas y sus archivos de integridad del lockfile de origen. El lockfile entregado contiene solo el cierre de dependencias de React, React DOM y Vite. Una instalación limpia con `npm ci` desde Internet queda pendiente de ejecutar en un entorno con acceso al registro.

Siguiente validación concreta: en un equipo sin las restricciones de red local, ejecutar los comandos del README, `dotnet test` y `node tests/api-smoke.mjs`; después registrar desde la interfaz y verificar que pasa a Completed. No se realizaron llamadas reales a proveedores ni despliegues externos.
