// Documentación técnica: Jorge Gomez.
using Microsoft.Data.Sqlite;

namespace Portal.Api;

/// <summary>Persiste solicitudes, coordina claims y conserva recibos del proveedor simulado.</summary>
/// <remarks>Cada operación abre su conexión; las conexiones no se comparten entre workers.</remarks>
public sealed class Store
{
    private readonly string connectionString;
    public Store(string databasePath)
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, DefaultTimeout = 10 }.ToString();
    }
    private SqliteConnection Open()
    {
        var cn = new SqliteConnection(connectionString); cn.Open(); return cn;
    }
    private static SqliteCommand Command(SqliteConnection cn, string sql, params (string, object?)[] args)
    {
        var cmd = cn.CreateCommand(); cmd.CommandText = sql;
        foreach (var (key, value) in args) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
    /// <summary>Crea el esquema inicial y protege Completed como estado terminal en la BD.</summary>
    /// <remarks>La creación idempotente no sustituye las migraciones de futuros cambios de esquema.</remarks>
    public void Initialize()
    {
        using var cn = Open();
        using var cmd = Command(cn, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS requests (
              id TEXT PRIMARY KEY, external_reference TEXT NOT NULL UNIQUE,
              customer_id TEXT NOT NULL, service_code TEXT NOT NULL, description TEXT NOT NULL,
              status TEXT NOT NULL CHECK(status IN ('Pending','Processing','Completed','Failed')),
              attempts INTEGER NOT NULL DEFAULT 0, created_at INTEGER NOT NULL,
              next_attempt INTEGER NOT NULL DEFAULT 0, lease_until INTEGER, claim_token TEXT, last_error TEXT);
            CREATE INDEX IF NOT EXISTS ix_requests_queue ON requests(status, next_attempt, lease_until);
            CREATE TABLE IF NOT EXISTS provider_effects (
              external_reference TEXT PRIMARY KEY, receipt TEXT NOT NULL, created_at INTEGER NOT NULL);
            CREATE TRIGGER IF NOT EXISTS completed_is_terminal BEFORE UPDATE OF status ON requests
              WHEN OLD.status = 'Completed' AND NEW.status != 'Completed'
              BEGIN SELECT RAISE(ABORT, 'Completed is terminal'); END;
            """);
        cmd.ExecuteNonQuery();
    }
    private static ServiceRequest Read(SqliteDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2),
        r.GetString(3), r.GetString(4), r.GetString(5), r.GetInt32(6), r.GetInt64(7), r.IsDBNull(8) ? null : r.GetString(8));
    private const string Columns = "id, external_reference, customer_id, service_code, description, status, attempts, created_at, last_error";
    /// <summary>Inserta una operación o devuelve la existente si la referencia y sus datos coinciden.</summary>
    /// <returns>La solicitud persistida y un indicador de si se insertó una fila nueva.</returns>
    /// <exception cref="ReferenceConflictException">La referencia existe con datos diferentes.</exception>
    /// <remarks>Recibe datos validados y normalizados; UNIQUE resuelve los reenvíos concurrentes.</remarks>
    public (ServiceRequest Request, bool Created) Create(CreateRequest input)
    {
        using var cn = Open(); using var tx = cn.BeginTransaction();
        using var insert = Command(cn, """
            INSERT INTO requests(id,external_reference,customer_id,service_code,description,status,created_at)
            VALUES($id,$ref,$customer,$service,$description,'Pending',$now)
            ON CONFLICT(external_reference) DO NOTHING
            """, ("$id", Guid.NewGuid().ToString()), ("$ref", input.ExternalReference), ("$customer", input.CustomerId),
            ("$service", input.ServiceCode), ("$description", input.Description), ("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        insert.Transaction = tx; var created = insert.ExecuteNonQuery() == 1;
        using var select = Command(cn, $"SELECT {Columns} FROM requests WHERE external_reference=$ref", ("$ref", input.ExternalReference));
        select.Transaction = tx;
        ServiceRequest result;
        using (var reader = select.ExecuteReader()) { reader.Read(); result = Read(reader); }
        if (result.CustomerId != input.CustomerId || result.ServiceCode != input.ServiceCode || result.Description != input.Description)
            throw new ReferenceConflictException();
        tx.Commit(); return (result, created);
    }
    public ServiceRequest? Get(string id)
    {
        using var cn = Open(); using var cmd = Command(cn, $"SELECT {Columns} FROM requests WHERE id=$id", ("$id", id));
        using var reader = cmd.ExecuteReader(); return reader.Read() ? Read(reader) : null;
    }
    /// <summary>Consulta por estado opcional con orden estable de fecha e identificador.</summary>
    /// <remarks>La API valida la paginación antes de invocar este método.</remarks>
    public IReadOnlyList<ServiceRequest> List(string? status, int limit = 100, int offset = 0)
    {
        using var cn = Open(); using var cmd = Command(cn,
            $"SELECT {Columns} FROM requests WHERE ($status IS NULL OR status=$status) ORDER BY created_at DESC,id LIMIT $limit OFFSET $offset",
            ("$status", status), ("$limit", limit), ("$offset", offset));
        using var reader = cmd.ExecuteReader(); var result = new List<ServiceRequest>();
        while (reader.Read()) result.Add(Read(reader)); return result;
    }
    public Claim? Take(long now)
    {
        // La transacción de escritura serializa selección y claim; now usa milisegundos Unix UTC.
        // El lease de 30 segundos permite recuperar Processing tras una caída del worker.
        using var cn = Open(); using var tx = cn.BeginTransaction();
        using var select = Command(cn, $"""
            SELECT {Columns} FROM requests WHERE (status='Pending' AND next_attempt <= $now)
              OR (status='Processing' AND lease_until <= $now) ORDER BY created_at,id LIMIT 1
            """, ("$now", now));
        select.Transaction = tx; ServiceRequest request;
        using (var reader = select.ExecuteReader()) { if (!reader.Read()) return null; request = Read(reader); }
        var token = Guid.NewGuid().ToString();
        using var update = Command(cn, """
            UPDATE requests SET status='Processing', attempts=attempts+1, claim_token=$token,lease_until=$lease
            WHERE id=$id
            """, ("$token", token), ("$lease", now + 30_000), ("$id", request.Id));
        update.Transaction = tx; update.ExecuteNonQuery(); tx.Commit();
        return new Claim(request with { Status = "Processing", Attempts = request.Attempts + 1 }, token);
    }
    public bool Finish(Claim claim, bool success, long now)
    {
        // El token impide que un worker rezagado sobrescriba el resultado de un nuevo claim.
        // Un fallo reprograma con espera progresiva; desde el tercer intento termina en Failed.
        using var cn = Open(); using var cmd = Command(cn, """
            UPDATE requests SET status=$status, next_attempt=$next, last_error=$error, lease_until=NULL,claim_token=NULL
            WHERE id=$id AND status='Processing' AND claim_token=$token
            """, ("$status", success ? "Completed" : claim.Request.Attempts >= 3 ? "Failed" : "Pending"),
            ("$next", now + (long)Math.Pow(2, Math.Min(claim.Request.Attempts, 10)) * 1000),
            ("$error", success ? null : "El proveedor simulado devolvió un fallo transitorio."),
            ("$id", claim.Request.Id), ("$token", claim.Token));
        return cmd.ExecuteNonQuery() == 1;
    }
    // El proveedor simulado conserva su efecto idempotente incluso si cae el worker antes de Finish.
    public bool InvokeProvider(Claim claim, long now)
    {
        // Un proveedor remoto real también debe garantizar idempotencia: la tabla local no basta.
        // FAIL- y RETRY- son convenciones del simulador, no reglas del negocio.
        using var cn = Open();
        using var existing = Command(cn, "SELECT COUNT(*) FROM provider_effects WHERE external_reference=$ref", ("$ref", claim.Request.ExternalReference));
        if ((long)existing.ExecuteScalar()! > 0) return true;
        var reference = claim.Request.ExternalReference;
        if (reference.StartsWith("FAIL-", StringComparison.Ordinal) ||
            (reference.StartsWith("RETRY-", StringComparison.Ordinal) && claim.Request.Attempts < 3)) return false;
        using var insert = Command(cn, """
            INSERT INTO provider_effects(external_reference, receipt, created_at) VALUES($ref,$receipt,$now)
            ON CONFLICT(external_reference) DO NOTHING
            """, ("$ref", reference), ("$receipt", Guid.NewGuid().ToString()), ("$now", now));
        insert.ExecuteNonQuery(); return true;
    }
    /// <summary>Cuenta efectos persistidos para verificar la idempotencia del simulador.</summary>
    public long EffectCount()
    {
        using var cn = Open(); using var cmd = Command(cn, "SELECT COUNT(*) FROM provider_effects");
        return (long)cmd.ExecuteScalar()!;
    }
}
