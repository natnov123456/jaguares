# RNF-05: ejecuta las pruebas del backend y muestra la cobertura de líneas (meta: >= 70 %).
#
# Uso (desde la carpeta backend):
#   .\tests\cobertura.ps1
#   .\tests\cobertura.ps1 -Servidor "Host=localhost;Port=5432;Username=postgres;Password=TU_CONTRASEÑA"
#
# Las pruebas crean una base temporal (jaguares_test_xxxx) en ese PostgreSQL y la borran al terminar:
# tu base fundacion_jaguares no se toca.
param(
    [string]$Servidor = $env:JAGUARES_TEST_PG,
    [double]$Minimo = 70
)

$ErrorActionPreference = 'Stop'
if ($Servidor) { $env:JAGUARES_TEST_PG = $Servidor }

$raiz = Split-Path -Parent $PSScriptRoot           # carpeta backend
$resultados = Join-Path $PSScriptRoot 'TestResults'
if (Test-Path $resultados) { Remove-Item $resultados -Recurse -Force }

dotnet test (Join-Path $PSScriptRoot 'FundacionJaguares.Tests') `
    --settings (Join-Path $PSScriptRoot 'FundacionJaguares.Tests\coverage.runsettings') `
    --results-directory $resultados
if ($LASTEXITCODE -ne 0) { Write-Host "Hay pruebas que fallaron." -ForegroundColor Red; exit $LASTEXITCODE }

$xml = Get-ChildItem $resultados -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
[xml]$cob = Get-Content $xml.FullName
$lineas = [math]::Round([double]$cob.coverage.'line-rate' * 100, 1)
$ramas = [math]::Round([double]$cob.coverage.'branch-rate' * 100, 1)

Write-Host ""
Write-Host "Cobertura de líneas: $lineas %  ($($cob.coverage.'lines-covered') de $($cob.coverage.'lines-valid'))"
Write-Host "Cobertura de ramas:  $ramas %"
foreach ($p in $cob.coverage.packages.package) {
    Write-Host ("  {0,-34} {1,6} %" -f $p.name, [math]::Round([double]$p.'line-rate' * 100, 1))
}
Write-Host "Reporte: $($xml.FullName)"

if ($lineas -lt $Minimo) { Write-Host "Por debajo del mínimo de $Minimo %" -ForegroundColor Red; exit 1 }
Write-Host "Cumple RNF-05 (>= $Minimo %)" -ForegroundColor Green
