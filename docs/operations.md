# Operación y despliegue

Configuración mediante argumentos de .NET o variables de entorno:

| Clave | Valor por defecto | Uso |
| --- | --- | --- |
| DatabasePath | data/portal.db bajo ContentRoot | Ruta local del archivo SQLite |
| Worker__Enabled | true | false deja la API sin worker |
| ASPNETCORE_URLS | Pasar `--urls` | Usar http://127.0.0.1:5080 para la demostración |

El backend admite `--Worker:Enabled=false`. Un segundo host puede ejecutar el worker compartiendo la misma BD local; cada host reclama filas transaccionalmente. No compartir SQLite mediante SMB. Los logs incluyen ID de solicitud, intento y resultado, sin payload.

## Windows Service

La demostración se ejecuta manualmente con `dotnet run`. Para convertirla en Windows Service en un entorno real, agregar `Microsoft.Extensions.Hosting.WindowsServices` versión compatible con .NET 8 y registrar antes de Build:

```csharp
builder.Services.AddWindowsService(o => o.ServiceName = "NexoPortal");
```

Publicar `dotnet publish src/Portal.Api -c Release -r win-x64 --self-contained false -o publish`. Instalar con `New-Service` apuntando a la ruta absoluta de `Portal.Api.exe`, argumentos `--contentRoot` y `--urls`, con una cuenta de servicio de privilegios mínimos. Establecer `DatabasePath` absoluto en un directorio de datos con permiso de escritura para esa cuenta; no usar System32 ni la carpeta de binarios. Verificar permisos, política de reinicios, logs y recuperación de leases antes de ponerlo en servicio. No basta con registrar el ejecutable actual sin añadir AddWindowsService.

La integración con SCM está documentada, no instalada ni ejecutada en esta entrega. Fuente: https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service y https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services

## Recuperación y respaldo

Al detener el proceso durante Processing, esperar hasta 30 segundos desde el claim: el siguiente host lo recupera. El token evita escrituras tardías y el recibo evita repetir efectos. Para respaldar, detener todos los hosts y copiar el directorio data completo (incluidos archivos WAL/SHM si existen), o usar la API de backup de SQLite. No copiar solo el .db mientras hay escrituras activas. Failed no se reprocesa automáticamente; investigar antes de definir un mecanismo administrativo.
