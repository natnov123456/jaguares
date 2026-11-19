#!/usr/bin/env bash
# JAG-77: aplica en orden los scripts de db/ que todavía no se aplicaron (migraciones).
# Cada script aplicado queda en la tabla scripts_aplicados (nombre + hash), así un despliegue nuevo
# solo ejecuta los scripts nuevos. Si un script ya aplicado cambió, se avisa y se detiene.
#   PGHOST, PGPORT, PGUSER, PGPASSWORD, PGDATABASE  conexión (variables estándar de PostgreSQL)
#   CARPETA_DB           carpeta con los .sql (por defecto ../../db)
#   INCLUIR_DATOS_DEMO   "true" solo en desarrollo: aplica 02_seed_usuarios.sql (cuentas de prueba con claves conocidas).
#                        En producción queda en false: las cuentas reales se crean con la carga inicial (JAG-76).
set -euo pipefail
CARPETA_DB="${CARPETA_DB:-$(cd "$(dirname "$0")/../../db" && pwd)}"
INCLUIR_DATOS_DEMO="${INCLUIR_DATOS_DEMO:-false}"
PSQL=(psql -v ON_ERROR_STOP=1 -q -X)

"${PSQL[@]}" -c "CREATE TABLE IF NOT EXISTS scripts_aplicados (
  nombre VARCHAR(200) PRIMARY KEY, sha256 CHAR(64) NOT NULL, aplicado_en TIMESTAMPTZ NOT NULL DEFAULT NOW());"

aplicados=0
for archivo in $(ls "$CARPETA_DB"/*.sql | sort); do
  nombre=$(basename "$archivo")
  if [[ "$nombre" == 02_seed_usuarios.sql && "$INCLUIR_DATOS_DEMO" != "true" ]]; then
    echo "· $nombre omitido (datos de demostración; INCLUIR_DATOS_DEMO=false)"
    continue
  fi
  hash=$(sha256sum "$archivo" | cut -d' ' -f1)
  previo=$("${PSQL[@]}" -tA -c "SELECT sha256 FROM scripts_aplicados WHERE nombre = '$nombre'")
  if [[ -n "$previo" ]]; then
    if [[ "$previo" != "$hash" ]]; then
      # Los scripts de este proyecto son idempotentes: se pueden volver a ejecutar sin perder datos
      echo "! $nombre cambió desde que se aplicó: se vuelve a ejecutar"
      "${PSQL[@]}" --single-transaction -f "$archivo"
      "${PSQL[@]}" -c "UPDATE scripts_aplicados SET sha256 = '$hash', aplicado_en = NOW() WHERE nombre = '$nombre'"
      aplicados=$((aplicados + 1))
    fi
    continue
  fi
  echo "→ aplicando $nombre"
  "${PSQL[@]}" --single-transaction -f "$archivo"
  "${PSQL[@]}" -c "INSERT INTO scripts_aplicados (nombre, sha256) VALUES ('$nombre', '$hash')"
  aplicados=$((aplicados + 1))
done
echo "Listo: $aplicados script(s) aplicado(s)."
