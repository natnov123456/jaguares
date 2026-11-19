# RNF-04 · Disponibilidad mensual ≥ 99,5 %

Meta: la plataforma responde al menos el 99,5 % del mes (máximo ≈ 3,6 horas sin servicio al mes), y eso se mide de forma continua.

## Qué se hizo (JAG-28)

| Pieza | Para qué |
|---|---|
| `GET /health/live` | Indica que el proceso de la API está vivo. Docker lo usa para **reiniciar automáticamente** el contenedor si deja de responder (`healthcheck` + `restart: unless-stopped` en `deploy/docker-compose.prod.yml`). |
| `GET /health/ready` | Indica que la API **y** PostgreSQL responden. Si solo falla la caché (Redis), responde *Degraded* con código 200: el panel de indicadores pierde velocidad, pero la plataforma sigue disponible. Responde 503 si la base de datos no responde. |
| Manejo global de errores | Un error no controlado se registra en el log y responde 500 con un mensaje genérico, sin detalles internos. La API sigue atendiendo las demás solicitudes. |
| `deploy/monitoreo/verificar-salud.sh` | Se ejecuta **cada minuto** (cron) y anota en `/var/log/jaguares/disponibilidad.csv` la hora, el código HTTP, la duración y si estaba OK. Opcionalmente avisa de una caída (`AVISO_CMD`). |
| `deploy/monitoreo/reporte-disponibilidad.sh AAAA-MM` | Calcula la disponibilidad del mes (minutos OK / minutos medidos) y lista cada caída con su duración. Termina con *CUMPLE* o *NO CUMPLE RNF-04*. |
| Uptime Kuma (opcional, en el `docker-compose`) | Panel web de monitoreo externo con avisos por correo o Telegram; sirve como segunda fuente de medición. |

Las respuestas de salud no exponen cadenas de conexión ni mensajes internos (hay una prueba automática que lo verifica).

## Puesta en marcha en el servidor

```bash
# 1. Programar la verificación cada minuto
crontab -e
* * * * * URL=https://jaguares.org.bo/health/ready /opt/jaguares/deploy/monitoreo/verificar-salud.sh

# 2. (Opcional) aviso por correo cuando no responde
* * * * * AVISO_CMD='mail -s "Jaguares caído" soporte@fundacion.org' URL=... /opt/jaguares/deploy/monitoreo/verificar-salud.sh
```

La verificación debe correr **fuera** del contenedor de la API (en el servidor o desde Uptime Kuma). Si corriera adentro, no podría registrar justamente las caídas del contenedor.

## CP-14 · Revisión al cierre de cada mes

```bash
./deploy/monitoreo/reporte-disponibilidad.sh 2026-11
#   Caída: 2026-11-10T12:01:00Z → 2026-11-10T12:04:00Z (3 min)
#   Mes 2026-11: 43200 mediciones, 43197 OK, 3 sin respuesta
#   Disponibilidad: 99.993 %  (meta RNF-04: >= 99.5 %)
#   CUMPLE RNF-04
```

El resultado (y la lista de caídas con su causa) se adjunta al informe mensual. Si no se cumple, se registra un Bug en Jira con la causa raíz.

## Medidas que sostienen la disponibilidad

- Reinicio automático de contenedores (`restart: unless-stopped`) y chequeos de salud en cada servicio.
- La base de datos con volumen persistente y **respaldo diario** con restauración probada (ver `docs/despliegue.md`).
- Los despliegues desde `main` pasan primero las pruebas automáticas (CI). Si una prueba falla, no se despliega.
- La caché es opcional: si Redis cae, la plataforma sigue funcionando.
