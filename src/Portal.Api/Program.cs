// Documentación técnica: Jorge Gomez.
using Portal.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16_384);
builder.Services.AddProblemDetails();
// ContentRoot evita que la ruta por defecto dependa del directorio de la consola o del servicio.
var database = builder.Configuration["DatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "portal.db");
var store = new Store(database); store.Initialize();
builder.Services.AddSingleton(store);
if (builder.Configuration.GetValue("Worker:Enabled", true)) builder.Services.AddHostedService<Processor>();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'";
    await next();
});
// React compilado y la API comparten origen; el despliegue local no requiere CORS.
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/requests", (CreateRequest input, Store db) => {
    var errors = Input.Validate(input);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    try {
        var (request, created) = db.Create(Input.Normalize(input));
        return created ? Results.Created($"/api/requests/{request.Id}", request) : Results.Ok(request);
    } catch (ReferenceConflictException) {
        // Reenviar datos idénticos es válido; reutilizar la referencia con otros datos es un conflicto.
        return Results.Problem(statusCode: 409, title: "Referencia ya utilizada", detail: "La referencia pertenece a una solicitud con datos diferentes.");
    }
});
app.MapGet("/api/requests", (string? status, int? limit, int? offset, Store db) => {
    if (status is not null && !Input.States.Contains(status))
        return Results.Problem(statusCode: 400, title: "Estado inválido");
    if (limit is < 1 or > 100 || offset is < 0)
        return Results.Problem(statusCode: 400, title: "Paginación inválida");
    return Results.Ok(db.List(status, limit ?? 100, offset ?? 0));
});
app.MapGet("/api/requests/{id}", (string id, Store db) => db.Get(id) is { } request ? Results.Ok(request) : Results.NotFound());
app.Run();
