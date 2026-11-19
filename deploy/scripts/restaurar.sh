#!/usr/bin/env bash
# JAG-77: restaura un respaldo en una base de datos.
# Uso: ./restaurar.sh /respaldos/jaguares-20261119-030000.dump [base_destino]
#   Sin base_destino restaura sobre $PGDATABASE (¡reemplaza los datos actuales! pide confirmación).
set -euo pipefail
ARCHIVO="${1:?Indica el archivo .dump}"
DESTINO="${2:-$PGDATABASE}"
[ -f "$ARCHIVO" ] || { echo "No existe $ARCHIVO"; exit 1; }
if [ -f "$ARCHIVO.sha256" ]; then sha256sum -c "$ARCHIVO.sha256" >/dev/null && echo "Integridad verificada (sha256)"; fi
if [ "$DESTINO" = "${PGDATABASE:-}" ] && [ "${CONFIRMAR:-}" != "si" ]; then
  read -r -p "Esto reemplaza los datos de '$DESTINO'. Escribe 'si' para continuar: " r
  [ "$r" = "si" ] || { echo "Cancelado"; exit 1; }
fi
psql -X -q -d postgres -c "SELECT 1" >/dev/null
psql -X -q -d postgres -tc "SELECT 1 FROM pg_database WHERE datname = '$DESTINO'" | grep -q 1 \
  || psql -X -q -d postgres -c "CREATE DATABASE \"$DESTINO\""
pg_restore --clean --if-exists --no-owner --dbname="$DESTINO" "$ARCHIVO"
echo "Restaurado $ARCHIVO en $DESTINO"
