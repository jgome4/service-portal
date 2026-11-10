# Nexo — Portal de solicitudes

Solución a `Prueba_Tecnica_FullStack_DotNet_Candidato.pdf`: ASP.NET Core 8, React y SQLite. Diseño sobrio, sin temática navideña. No requiere Docker, servidor SQL ni proveedores externos.

## Ejecutar

Requisitos: SDK .NET 8 (8.0.427 o parche posterior de la misma línea), Node.js 24 y npm. La primera instalación requiere acceso a NuGet/npm o sus cachés locales.

Desde esta carpeta, en PowerShell:

```powershell
dotnet restore tests/Portal.Tests/Portal.Tests.csproj
Push-Location frontend
npm ci
npm run build
Pop-Location
dotnet run --project src/Portal.Api --urls http://127.0.0.1:5080
```

Abrir http://127.0.0.1:5080. API, interfaz y worker se ejecutan en el mismo proceso y origen. SQLite se crea automáticamente en `src/Portal.Api/data/portal.db`; los datos sobreviven a reinicios. No borrar ese directorio para conservarlos.

Para desarrollo, ejecutar el backend y, en otra terminal, `cd frontend; npm run dev`. Vite publica su URL local y redirige `/api` al backend en el puerto 5080.

## Demostración

Registrar una referencia `ORDEN-001` completa normalmente. Una referencia que comience exactamente con `RETRY-` falla dos veces y completa en el tercer intento. `FAIL-` falla tres veces y termina en Failed. El código de servicio es libre. Son convenciones explícitas del simulador, no reglas del negocio.

Reenviar la misma referencia y los mismos datos devuelve la solicitud existente sin crear otra fila. Cambiar datos con esa referencia devuelve 409. Los errores no limpian el formulario para permitir corregir o reintentar. El listado consulta cada 2,5 segundos, filtra en servidor y pagina de 20 en 20.

```powershell
dotnet test tests/Portal.Tests/Portal.Tests.csproj
```

Si el entorno impide la comunicación local del testhost, las mismas pruebas se ejecutan sin VSTest:

```powershell
dotnet run --project tests/Portal.Tests/Portal.Tests.csproj -p:UseSharedCompilation=false
```

Con la aplicación en ejecución, `node tests/api-smoke.mjs` comprueba API, concurrencia, errores, reintentos y archivos estáticos. Este smoke test crea registros con referencias únicas en la BD seleccionada; usar una BD de prueba.

También se incluyen los archivos estáticos ya compilados; `./start.ps1` permite iniciar directamente sin recompilar React. Recompilar frontend después de modificar sus fuentes.

Ver [contrato API](docs/api.md), [decisiones y límites](docs/decisions.md), [revisión de legado](docs/legacy-review.md) y [operación/Windows Service](docs/operations.md).

## Uso de IA

Se utilizó Codex para interpretar el anexo, implementar API, almacenamiento, worker, interfaz, pruebas y documentación. Se verificó mediante compilación, pruebas del almacenamiento y escenarios HTTP. Durante la validación se sustituyeron versiones de paquetes no disponibles por versiones existentes en la caché local y se añadió la inicialización explícita del motor nativo SQLite. Las salidas de IA no se consideran evidencia de pruebas; los resultados reales se registran en `docs/validation.md`. No se utilizaron credenciales ni llamadas a proveedores de IA adicionales.
