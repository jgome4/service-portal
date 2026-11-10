# Revisión de los fragmentos legados

## A — WebForms

Riesgo: concatenar `txtCustomer.Text` permite inyección SQL. Corrección mínima: parametrizar con el tipo y longitud reales de la columna, y disponer comando y lector. Validar longitud antes de abrir la conexión y comprobar que el usuario pueda consultar ese cliente. El nombre de la conexión permanece en configuración; no trasladar su secreto al código.

```csharp
var customer = txtCustomer.Text.Trim();
if (customer.Length == 0 || customer.Length > 100) return; // Ajustar 100 al esquema real.
using var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["Main"].ConnectionString);
using var cmd = new SqlCommand("SELECT Id, Customer, Status FROM ServiceRequests WHERE Customer = @customer", cn);
cmd.Parameters.Add("@customer", SqlDbType.NVarChar, 100).Value = customer;
cn.Open();
using var reader = cmd.ExecuteReader();
GridView1.DataSource = reader;
GridView1.DataBind();
```

Validar con entradas como `' OR 1=1 --` y confirmar que son tratadas como texto, nunca como SQL. El ejemplo es una propuesta de corrección; no se creó ni ejecutó un proyecto WebForms.

## B — ASP.NET MVC

Riesgos: Find puede devolver null; cualquier cadena puede convertirse en estado; Completed puede retroceder; falta control de concurrencia y de acceso. Si usa autenticación con cookies, también necesita antiforgery. Cambio mínimo: DTO con estado permitido, buscar/verificar permiso, validar la transición y actualizar condicionalmente con RowVersion. Evitar devolver la entidad completa.

```csharp
[HttpPost, Authorize, ValidateAntiForgeryToken]
public ActionResult UpdateStatus(UpdateStatusDto input)
{
    if (!ModelState.IsValid) return new HttpStatusCodeResult(400);
    var request = db.ServiceRequests.Find(input.Id);
    if (request == null) return HttpNotFound();
    if (!CanManage(User, request)) return new HttpStatusCodeResult(403);
    if (!AllowedTransition(request.Status, input.Status))
        return new HttpStatusCodeResult(409);
    db.Entry(request).OriginalValues["RowVersion"] = input.RowVersion;
    request.Status = input.Status;
    try { db.SaveChanges(); }
    catch (DbUpdateConcurrencyException) { return new HttpStatusCodeResult(409); }
    return Json(new { request.Id, request.Status });
}
```

Precondiciones del ejemplo: DTO con Required, lista de estados y RowVersion; columna de concurrencia mapeada como rowversion en SQL Server; `CanManage` basado en permisos reales y `AllowedTransition` explícito (Completed no retrocede). Estos elementos deben implementarse en el proyecto legado antes de aplicar el fragmento. Probar ID inexistente, falta de permiso, estado inválido, Completed y dos actualizaciones concurrentes.
