#!/usr/bin/env bash
# JAG-77: respaldo de la base de datos con retención de 30 días.
# Lo ejecuta el servicio "respaldos" del docker-compose una vez al día (o cron en el servidor).
#   PG* variables de conexión · CARPETA_RESPALDOS (por defecto /respaldos) · DIAS_RETENCION (por defecto 30)
# Formato custom de pg_dump (comprimido, se restaura con pg_restore). El nombre lleva fecha y hora UTC.
set -euo pipefail
CARPETA="${CARPETA_RESPALDOS:-/respaldos}"
DIAS="${DIAS_RETENCION:-30}"
mkdir -p "$CARPETA"
archivo="$CARPETA/jaguares-$(date -u +%Y%m%d-%H%M%S).dump"
pg_dump --format=custom --no-owner --file="$archivo.parcial"
mv "$archivo.parcial" "$archivo"   # solo queda con nombre final si el respaldo terminó bien
sha256sum "$archivo" > "$archivo.sha256"
echo "Respaldo creado: $archivo ($(du -h "$archivo" | cut -f1))"
# Retención: borra los respaldos con más de $DIAS días
find "$CARPETA" -name 'jaguares-*.dump*' -type f -mtime +"$DIAS" -print -delete | sed 's/^/Eliminado por retención: /'
