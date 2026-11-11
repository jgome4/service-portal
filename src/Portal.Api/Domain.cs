// Documentación técnica: Jorge Gomez.
namespace Portal.Api;

public record CreateRequest(string ExternalReference, string CustomerId, string ServiceCode, string Description);
/// <summary>Solicitud persistida; CreatedAt se expresa en milisegundos Unix UTC.</summary>
/// <remarks>LastError es nulo cuando no hay un fallo registrado del proveedor.</remarks>
public record ServiceRequest(string Id, string ExternalReference, string CustomerId, string ServiceCode,
    string Description, string Status, int Attempts, long CreatedAt, string? LastError);
/// <summary>Vincula la solicitud reclamada con el token que permite confirmar ese intento.</summary>
public record Claim(ServiceRequest Request, string Token);
public sealed class ReferenceConflictException : Exception;

public static class Input
{
    public static readonly string[] States = ["Pending", "Processing", "Completed", "Failed"];
    public static Dictionary<string, string[]> Validate(CreateRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (name, value, max) in new[] {
            ("externalReference", input.ExternalReference, 100), ("customerId", input.CustomerId, 100),
            ("serviceCode", input.ServiceCode, 50), ("description", input.Description, 2000) })
            if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl))
                errors[name] = [$"Campo requerido, máximo {max} caracteres, sin caracteres de control."];
        return errors;
    }
    /// <summary>Recorta espacios sin cambiar las mayúsculas de los identificadores.</summary>
    /// <remarks>El llamador debe validar primero para descartar valores nulos o vacíos.</remarks>
    public static CreateRequest Normalize(CreateRequest input) => new(input.ExternalReference.Trim(),
        input.CustomerId.Trim(), input.ServiceCode.Trim(), input.Description.Trim());
}
