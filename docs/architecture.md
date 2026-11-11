# Arquitectura de ServicePortal

Tres vistas del sistema implementado. La arquitectura muestra dónde se ejecuta cada responsabilidad. El frontend muestra cómo se organiza la pantalla de seguimiento. El modelo de datos describe el esquema SQLite.

- [Arquitectura general](#arquitectura-general)
- [Componentes del frontend](#componentes-del-frontend)
- [Modelo de datos](#modelo-de-datos)

## Arquitectura general

~~~mermaid
flowchart TB
    Browser["Navegador · Portal React · registro y seguimiento"]
    subgraph Host["PROCESO ASP.NET CORE 8"]
        direction LR
        API["API REST · Validación · alta idempotente · consulta"]
        Worker["Worker .NET · Reclama pendientes · reintenta · finaliza"]
        Sim["Proveedor simulado · Éxito o fallo transitorio"]
        Worker -->|Ejecuta| Sim
    end
    DB[("SQLite local · Solicitudes y recibos")]
    Browser -->|HTTP · mismo origen| API
    API -->|Registra y consulta| DB
    Worker -->|Reclama y actualiza| DB
    Sim -->|Consulta y registra el efecto| DB

    classDef browser fill:#E0F2FE,stroke:#0284C7,color:#082F49,stroke-width:2px
    classDef api fill:#EDE9FE,stroke:#7C3AED,color:#2E1065,stroke-width:2px
    classDef worker fill:#DBEAFE,stroke:#2563EB,color:#172554,stroke-width:2px
    classDef sim fill:#FEF3C7,stroke:#D97706,color:#451A03,stroke-width:2px
    classDef data fill:#D1FAE5,stroke:#059669,color:#022C22,stroke-width:2px
    class Browser browser
    class API api
    class Worker worker
    class Sim sim
    class DB data
~~~

ASP.NET Core sirve los archivos estáticos de React y aloja el worker. El navegador registra y consulta solicitudes por HTTP. El worker usa SQLite para recuperar trabajo pendiente o abandonado y el simulador registra un recibo por referencia.

[Entrada del host](../src/Portal.Api/Program.cs) · [Worker](../src/Portal.Api/Processor.cs) · [Persistencia y simulador](../src/Portal.Api/Store.cs)

## Componentes del frontend

~~~mermaid
flowchart TB
    App["App React · Estado compartido y coordinación"]
    Form["Nueva solicitud · Formulario · validación del envío"]
    List["Seguimiento · Tabla · estado · filtros · paginación"]
    Poll["Actualización del listado · Al cambiar filtros y cada 2,5 s"]
    Client["Función api() · Fetch · JSON · errores"]
    Api["API del portal · POST y GET /api/requests"]

    App --> Form
    App --> List
    Form -->|Envía solicitud| Client
    List -->|Página y estado| Poll
    Poll -->|Consulta con filtros| Client
    Client --> Api

    classDef app fill:#172554,stroke:#60A5FA,color:#FFFFFF,stroke-width:2px
    classDef ui fill:#E0F2FE,stroke:#0284C7,color:#082F49,stroke-width:2px
    classDef flow fill:#EDE9FE,stroke:#7C3AED,color:#2E1065,stroke-width:2px
    classDef boundary fill:#D1FAE5,stroke:#059669,color:#022C22,stroke-width:2px
    class App app
    class Form,List ui
    class Poll,Client flow
    class Api boundary
~~~

El formulario, la tabla y los filtros son regiones de `App` en la implementación actual. Una vez registrado el envío, `App` reinicia el formulario y refresca el listado. Al cambiar los filtros o desmontar la pantalla, cancela la consulta anterior.

[Código del frontend](../frontend/src/main.jsx) · [Proxy de desarrollo](../frontend/vite.config.js)

## Modelo de datos

~~~mermaid
erDiagram
    REQUESTS |o..o| PROVIDER_EFFECTS : "external_reference (vínculo lógico)"

    REQUESTS {
        TEXT id PK
        TEXT external_reference UK
        TEXT customer_id
        TEXT service_code
        TEXT description
        TEXT status
        INTEGER attempts
        INTEGER created_at
        INTEGER next_attempt
        INTEGER lease_until "admite nulo"
        TEXT claim_token "admite nulo"
        TEXT last_error "admite nulo"
    }

    PROVIDER_EFFECTS {
        TEXT external_reference PK
        TEXT receipt
        INTEGER created_at
    }
~~~

| Elemento | Garantía |
| --- | --- |
| Referencia única en `requests` | Los reenvíos con datos idénticos recuperan la solicitud existente. Los datos distintos para la misma referencia producen HTTP 409. |
| Recibo por referencia | El simulador reconoce un efecto ya guardado si el worker cae antes de registrar el resultado. |
| Índice `ix_requests_queue` | Apoya la búsqueda por estado, próximo intento y vencimiento de la reserva. |
| Trigger `completed_is_terminal` | Impide que una solicitud completada vuelva a otro estado. |

La referencia vincula ambas tablas en la lógica de la aplicación. El esquema SQLite no declara una clave foránea entre ellas. Los valores de tiempo se guardan en milisegundos Unix UTC.

[Esquema SQLite](../src/Portal.Api/Store.cs) · [Decisiones técnicas](decisions.md)
