#!/usr/bin/env bash
# JAG-28 / RNF-04 · CP-14: disponibilidad mensual a partir del registro de verificar-salud.sh.
# Uso:  ./reporte-disponibilidad.sh 2026-11 [/var/log/jaguares/disponibilidad.csv]
# Disponibilidad = minutos con respuesta OK / minutos medidos. Meta: ≥ 99,5 % (≈ 3,6 h de caída máximo al mes).
# También lista los periodos de caída (minutos seguidos sin respuesta) para el informe del mes.
set -eu
MES="${1:?Indica el mes: AAAA-MM}"
REGISTRO="${2:-/var/log/jaguares/disponibilidad.csv}"
[ -f "$REGISTRO" ] || { echo "No existe el registro $REGISTRO"; exit 1; }

awk -F, -v mes="$MES" '
  NR == 1 { next }
  substr($1, 1, 7) != mes { next }
  {
    total++
    if ($4 == "OK") { ok++; if (inicio != "") { printf "  Caída: %s → %s (%d min)\n", inicio, $1, minutos; inicio = ""; minutos = 0 } }
    else { caidas++; if (inicio == "") inicio = $1; minutos++ }
  }
  END {
    if (inicio != "") printf "  Caída: %s → (sigue al cierre del registro) (%d min)\n", inicio, minutos
    if (total == 0) { print "Sin mediciones para " mes; exit 2 }
    pct = ok * 100 / total
    printf "Mes %s: %d mediciones, %d OK, %d sin respuesta\n", mes, total, ok, caidas
    printf "Disponibilidad: %.3f %%  (meta RNF-04: >= 99.5 %%)\n", pct
    if (pct >= 99.5) print "CUMPLE RNF-04"; else { print "NO CUMPLE RNF-04"; exit 1 }
  }' "$REGISTRO"
