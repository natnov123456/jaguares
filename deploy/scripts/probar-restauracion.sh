#!/usr/bin/env bash
# JAG-77: prueba de restauración (definición de terminado). Restaura el respaldo más reciente en una base
# temporal, compara la cantidad de filas de las tablas principales con la base en uso y borra la temporal.
# Uso: ./probar-restauracion.sh [archivo.dump]   (por defecto, el último de $CARPETA_RESPALDOS)
set -euo pipefail
CARPETA="${CARPETA_RESPALDOS:-/respaldos}"
ARCHIVO="${1:-$(ls -t "$CARPETA"/jaguares-*.dump 2>/dev/null | head -1)}"
[ -n "$ARCHIVO" ] && [ -f "$ARCHIVO" ] || { echo "No hay respaldos en $CARPETA"; exit 1; }
TEMP="jaguares_prueba_restauracion_$(date +%s)"
trap 'psql -X -q -d postgres -c "DROP DATABASE IF EXISTS \"$TEMP\" WITH (FORCE)" >/dev/null 2>&1 || true' EXIT

CONFIRMAR=si "$(dirname "$0")/restaurar.sh" "$ARCHIVO" "$TEMP" >/dev/null
fallas=0
for tabla in usuarios cursos inscripciones eventos donaciones auditoria_donaciones certificados voluntarios; do
  origen=$(psql -X -tA -c "SELECT COUNT(*) FROM $tabla" 2>/dev/null || echo "-")
  copia=$(psql -X -tA -d "$TEMP" -c "SELECT COUNT(*) FROM $tabla" 2>/dev/null || echo "-")
  marca="OK"; [ "$origen" = "$copia" ] || { marca="DIFERENTE (puede haber datos nuevos después del respaldo)"; fallas=$((fallas + 1)); }
  printf "  %-22s en uso: %-8s respaldo: %-8s %s\n" "$tabla" "$origen" "$copia" "$marca"
done
echo "Respaldo probado: $ARCHIVO"
[ "$fallas" -eq 0 ] && echo "RESTAURACIÓN EXITOSA" || echo "Restauración completada con $fallas tabla(s) con diferencias: revisar si son datos posteriores al respaldo"
