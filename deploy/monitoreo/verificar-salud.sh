#!/usr/bin/env bash
# JAG-28 / RNF-04: consulta /health/ready y deja una línea en el registro de disponibilidad.
# Se programa cada minuto con cron:   * * * * * /opt/jaguares/deploy/monitoreo/verificar-salud.sh
#   URL      dirección del chequeo (por defecto https://$DOMINIO/api-salud/ready o http://localhost:8080/health/ready)
#   REGISTRO archivo CSV (por defecto /var/log/jaguares/disponibilidad.csv)
# Formato: fecha_utc,codigo_http,duracion_ms,estado   (estado = OK | CAIDA)
set -u
URL="${URL:-http://localhost:8080/health/ready}"
REGISTRO="${REGISTRO:-/var/log/jaguares/disponibilidad.csv}"
mkdir -p "$(dirname "$REGISTRO")"
[ -f "$REGISTRO" ] || echo "fecha_utc,codigo_http,duracion_ms,estado" > "$REGISTRO"

salida=$(curl -s -o /dev/null -w "%{http_code} %{time_total}" --max-time 10 "$URL" || echo "000 10")
codigo=${salida% *}
segundos=${salida#* }
ms=$(awk -v s="$segundos" 'BEGIN { printf "%d", s * 1000 }')
estado=CAIDA
[ "$codigo" = "200" ] && estado=OK
echo "$(date -u +%Y-%m-%dT%H:%M:%SZ),$codigo,$ms,$estado" >> "$REGISTRO"

# Aviso opcional por correo/Telegram: si AVISO_CMD está definido, se ejecuta al detectar una caída
if [ "$estado" = "CAIDA" ] && [ -n "${AVISO_CMD:-}" ]; then
  eval "$AVISO_CMD \"Jaguares no responde ($codigo) a las $(date -u +%H:%M) UTC\"" || true
fi
