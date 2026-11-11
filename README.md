# Nexo — Portal de solicitudes de servicio

Nexo registra solicitudes de servicio, muestra su estado y las procesa en segundo plano. Es una prueba técnica Full Stack .NET centrada en persistencia, concurrencia, idempotencia y recuperación ante fallos.

## Funcionalidades

- Registro por referencia externa, cliente, código de servicio y descripción.
- Consulta por estado, paginación y actualización del listado cada 2,5 segundos.
- Reenvío seguro. La misma referencia con datos idénticos devuelve la solicitud existente. Si los datos cambian, la API devuelve HTTP 409.
- Procesamiento en segundo plano con reintentos y recuperación tras interrupciones.

## Arquitectura

| Componente | Tecnología | Responsabilidad |
| --- | --- | --- |
| Interfaz | React y Vite | Registro, seguimiento y filtros. ASP.NET Core sirve el build estático. |
| API | ASP.NET Core 8 | Validación, registro y consulta de solicitudes. |
| Worker | BackgroundService de .NET | Reclama solicitudes, ejecuta el proveedor simulado y guarda el resultado. |
| Persistencia | SQLite | Conserva solicitudes, intentos y recibos. Se inicializa al arrancar. |

El worker reclama cada solicitud en una transacción. La reserva caduca a los 30 segundos para permitir la recuperación y su token evita escrituras tardías. El simulador conserva un recibo por referencia para reconocer un efecto ya registrado si se interrumpe el procesamiento. Consulta las [decisiones técnicas](docs/decisions.md).

## Ejecutar en local

Instala el SDK .NET 8 indicado en [global.json](global.json). Para recompilar la interfaz, usa Node.js 24 y npm. La primera restauración requiere acceso a los registros de paquetes o a una caché con las dependencias.

~~~powershell
git clone https://github.com/jgome4/service-portal.git
cd service-portal
dotnet run --project src/Portal.Api --urls http://127.0.0.1:5080
~~~

Abre [el portal local](http://127.0.0.1:5080). El frontend compilado está incluido. La interfaz, la API y el worker se ejecutan en el mismo proceso. SQLite crea el archivo de datos en src/Portal.Api/data/portal.db.

Para recompilar el frontend después de editarlo:

~~~powershell
Push-Location frontend
npm ci
npm run build
Pop-Location
~~~

Para desarrollo con recarga automática, deja el backend en el puerto 5080 y ejecuta npm run dev en frontend desde otra terminal. Vite redirige /api al backend.

## Demostración

| Referencia | Resultado esperado |
| --- | --- |
| ORDEN-001 | Completa en el primer intento. |
| RETRY-001 | Falla dos veces y completa en el tercer intento. |
| FAIL-001 | Agota tres intentos y termina en Failed. |

RETRY- y FAIL- activan escenarios del simulador. Los estados son Pending, Processing, Completed y Failed. Ante fallos transitorios, el worker espera 2 y 4 segundos antes de los siguientes intentos.

## API REST

| Método y ruta | Uso |
| --- | --- |
| POST /api/requests | Registra una solicitud. Devuelve 201 al crear, 200 al reenviar datos idénticos y 409 ante un conflicto de referencia. |
| GET /api/requests | Lista solicitudes con los parámetros opcionales status, limit y offset. |
| GET /api/requests/{id} | Consulta una solicitud por identificador. |
| GET /api/health | Comprueba que la API responde. |

~~~json
{
  "externalReference": "ORDEN-001",
  "customerId": "CLIENTE-42",
  "serviceCode": "VISITA",
  "description": "Revisar instalación"
}
~~~

Consulta el [contrato HTTP](docs/api.md) para ver validaciones y códigos de respuesta.

## Pruebas

~~~powershell
dotnet test tests/Portal.Tests/Portal.Tests.csproj
~~~

Si VSTest no puede iniciar su proceso de pruebas en tu entorno, ejecuta los mismos casos directamente con dotnet run --project tests/Portal.Tests/Portal.Tests.csproj -p:UseSharedCompilation=false. Con el portal activo y una base de datos de prueba, node tests/api-smoke.mjs comprueba la API, concurrencia, validación, reintentos y archivos estáticos. El script crea solicitudes de prueba. Consulta los [resultados y límites de validación](docs/validation.md).

## Estructura y operación

- src/Portal.Api/ contiene la API, SQLite, el worker y la interfaz compilada en wwwroot/.
- frontend/ contiene el código React.
- tests/ contiene las pruebas automatizadas y la comprobación HTTP.
- docs/ incluye [operación y despliegue](docs/operations.md), [decisiones técnicas](docs/decisions.md) y [revisión de código legado](docs/legacy-review.md).

La demostración se ejecuta en local con un proveedor simulado. Antes de desplegarla en una red compartida, incorpora autenticación, autorización y HTTPS. La documentación de operación cubre configuración, respaldo y adaptación a Windows Service.

## Uso de IA

Se utilizó Codex para interpretar la prueba técnica e implementar el portal, sus pruebas y su documentación. Los resultados verificados y las comprobaciones pendientes están en [validación](docs/validation.md).

### Arnés de desarrollo

El arnés local organizó las instrucciones de trabajo y la revisión de los cambios.

| Componente | Función |
| --- | --- |
| `AGENTS.md` | Define las reglas del agente principal, el alcance de los cambios y las comprobaciones antes de entregar. |
| Skills | Proporcionan instrucciones reutilizables para tareas como documentar código, revisar cambios y preparar verificaciones. El agente carga las que corresponden al trabajo. |
| Subagentes | El arnés define perfiles para inventario, implementación y revisión. Cuando se delega, cada perfil recibe una tarea delimitada y permisos acotados. El agente principal revisa e integra los resultados. |
| Selección de modelos | El enrutamiento considera el tipo de tarea, la complejidad, el riesgo y la disponibilidad. La configuración contempla GLM para inventario y backend delimitado, DeepSeek para documentación, pruebas y planificación, Kimi para frontend y revisión de navegador, y GPT‑6.1 Sol para arquitectura e integración compleja. |

Para esta entrega se utilizó Codex como agente principal. Los perfiles de la tabla describen la configuración del arnés. La entrega no registra despachos a subagentes externos ni llamadas adicionales a esos proveedores. El arnés y sus credenciales se gestionan localmente, fuera del repositorio público.

### Verificación y ajustes

Se comprobó el resultado mediante la compilación del backend y el frontend y la ejecución de diez pruebas del almacenamiento. Durante la validación se ajustaron las versiones de paquetes a las disponibles en la caché local y se añadió la inicialización explícita del motor nativo de SQLite. Consulta los resultados y las comprobaciones pendientes en [validación](docs/validation.md).
