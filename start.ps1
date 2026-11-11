# Documentación técnica: Jorge Gomez.
# Reutiliza el frontend compilado; si cambian sus fuentes, ejecutar npm run build antes de iniciar.
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not (Test-Path -LiteralPath 'src/Portal.Api/wwwroot/index.html')) {
        Push-Location frontend
        try { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci falló' }; npm run build; if ($LASTEXITCODE -ne 0) { throw 'Build frontend falló' } }
        finally { Pop-Location }
    }
    dotnet run --project src/Portal.Api --urls http://127.0.0.1:5080
    if ($LASTEXITCODE -ne 0) { throw 'El backend no pudo iniciar' }
} finally { Pop-Location }
