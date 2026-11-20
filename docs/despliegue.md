# Despliegue en producción (JAG-77)

Arquitectura en el servidor (un VPS Linux con Docker, por ejemplo 2 vCPU y 4 GB de RAM):

```
Internet ──HTTPS──► Caddy (certificado TLS automático, HTTP→HTTPS)
                      ├── /api/*, /uploads/*, /health/*  ──► api (ASP.NET Core) ──► db (PostgreSQL 16)
                      │                                                         └──► redis (caché de indicadores)
                      └── todo lo demás                  ──► web (React servido por nginx)
migraciones (al iniciar) · respaldos (diario 03:00, 30 días) · monitor (Uptime Kuma, opcional)
```

Archivos: `deploy/docker-compose.prod.yml`, `deploy/Caddyfile`, `deploy/.env.example`, `deploy/scripts/*.sh`, `backend/Dockerfile`, `frontend/Dockerfile`, `.github/workflows/despliegue.yml`.

> El documento del proyecto mencionaba SQL Server. La plataforma usa **PostgreSQL 16** desde el Sprint 1, así que el despliegue es con PostgreSQL.

## 1. Preparar el servidor (una sola vez)

1. Instalar Docker y el plugin Compose (`curl -fsSL https://get.docker.com | sh`).
2. Apuntar el **dominio oficial** de la Fundación (registro A/AAAA, y `www` como CNAME) a la IP del servidor. Abrir los puertos 80 y 443.
3. Clonar el repositorio en `/opt/jaguares` y crear el archivo de secretos:
   ```bash
   git clone https://github.com/natnov123456/jaguares.git /opt/jaguares
   cd /opt/jaguares/deploy
   cp .env.example .env && chmod 600 .env
   nano .env        # dominio, claves de BD, JWT_SECRET (openssl rand -base64 48)...
   ```
   Los secretos viven **solo** en `deploy/.env`, nunca en el código: cadena de conexión, clave JWT, **correo (SMTP)** y pasarela de pago. Si `JWT_SECRET` es el valor de desarrollo o tiene menos de 32 caracteres, la API **no arranca** en producción.
4. Primer arranque:
   ```bash
   docker compose -f docker-compose.prod.yml --env-file .env up -d
   docker compose -f docker-compose.prod.yml logs -f migraciones   # aplica db/*.sql
   ```
   Caddy obtiene el certificado HTTPS automáticamente en el primer acceso y lo renueva solo.
5. Configurar el **correo (SMTP)** en `.env` (`SMTP_HOST`, `SMTP_PUERTO`, `SMTP_USUARIO`, `SMTP_CLAVE`, `SMTP_REMITENTE`, `SMTP_SSL` y `CONTACTO_DESTINO`). Con Gmail: `smtp.gmail.com`, puerto 587 y una *contraseña de aplicación*. Sin SMTP la plataforma funciona, pero no salen las confirmaciones, los recordatorios, los enlaces de contraseña ni las consultas de contacto.
6. Cargar los datos iniciales y crear las cuentas del personal (ver `docs/datos-iniciales.md`, JAG-76). La planilla `cuentas_internas.csv` debe incluir a la **Directora**: le llega por correo el enlace para definir su contraseña (si venció, usa *¿Olvidaste tu contraseña?* en el inicio de sesión).
7. Programar el monitoreo de disponibilidad (ver `docs/operacion/disponibilidad-rnf04.md`).

## 2. CI/CD (despliegue automático desde `main`)

`.github/workflows/despliegue.yml` corre en cada push a `main`:

1. **Pruebas del backend** (las mismas de `backend-tests.yml`, con cobertura ≥ 70 %, RNF-05) y **del frontend** (`npm test` + build).
2. Si pasan, construye las imágenes `jaguares-api` y `jaguares-web` y las publica en GitHub Container Registry.
3. Entra por SSH al servidor, hace `docker compose pull && up -d` y verifica `https://DOMINIO/health/ready`.

Si una prueba falla, no se publica ni se despliega nada. No hay pasos manuales.

Configuración en GitHub (*Settings → Secrets and variables → Actions*): `SSH_HOST`, `SSH_USUARIO`, `SSH_CLAVE`. Además, crear el entorno `produccion` (*Settings → Environments*) si se quiere exigir una aprobación antes de desplegar. Mientras esos secretos no estén cargados, el workflow corre las pruebas y genera las imágenes, pero **omite** el paso de despliegue con un aviso (no falla).

## 3. Migraciones de base de datos

El servicio `migraciones` ejecuta `deploy/scripts/aplicar-scripts.sh` antes de iniciar la API. El script:

- aplica en orden los `db/*.sql` que aún no se aplicaron y registra cada uno en la tabla `scripts_aplicados` (nombre + sha256);
- **no** aplica `02_seed_usuarios.sql` (cuentas de demostración con claves conocidas) salvo que se indique `INCLUIR_DATOS_DEMO=true`, y eso solo en desarrollo.

## 4. Respaldos y restauración

- **Automático:** el servicio `respaldos` ejecuta `respaldo.sh` todos los días a las 03:00. Usa `pg_dump` en formato comprimido con un hash sha256 y guarda en `deploy/respaldos/`. Borra los respaldos de más de 30 días (`DIAS_RETENCION`).
- **Copia fuera del servidor:** se recomienda sincronizar `deploy/respaldos/` a otro lugar (rclone a Google Drive de la Fundación, por ejemplo).
- **Restaurar** (reemplaza los datos actuales, pide confirmación):
  ```bash
  docker compose -f docker-compose.prod.yml run --rm -v ./respaldos:/respaldos respaldos \
    bash /scripts/restaurar.sh /respaldos/jaguares-20261119-030000.dump
  ```
- **Prueba de restauración** (definición de terminado): restaura el último respaldo en una base temporal, compara la cantidad de filas de las tablas principales y la borra:
  ```bash
  docker compose -f docker-compose.prod.yml exec respaldos bash /scripts/probar-restauracion.sh
  ```
  Se recomienda ejecutarla una vez al mes y anotar el resultado en el informe.

**Prueba del despliegue completo con Docker (19 y 20/11/2026):** con `docker-compose.prod.yml`, las imágenes construidas desde `main` y `DOMINIO=localhost` (Caddy emite un certificado local). Resultados: migraciones aplicadas; `/health/ready` en 200 con base de datos y caché sanas; HTTP → HTTPS (308); cabeceras HSTS y `X-Frame-Options`; endpoint protegido sin token en 401; respaldo creado y restauración probada ("RESTAURACIÓN EXITOSA"). Con un servidor SMTP de pruebas (Mailpit): llegó el correo de *¿Olvidaste tu contraseña?*, el enlace definió la contraseña nueva y la anterior dejó de servir, y la consulta del formulario de contacto llegó al correo institucional con "responder a" del visitante. Falta repetirlo en el servidor real con el dominio oficial y el SMTP de la Fundación.

Los scripts de migración, respaldo y prueba de restauración se probaron contra PostgreSQL 16 local. Primera ejecución: aplicó 16 scripts y omitió los datos de demostración. Segunda ejecución: no aplicó nada. El respaldo se restauró con éxito.

## 5. Operación diaria

| Tarea | Comando (en `/opt/jaguares/deploy`) |
|---|---|
| Ver el estado | `docker compose -f docker-compose.prod.yml ps` |
| Ver los logs de la API | `docker compose -f docker-compose.prod.yml logs -f --tail 200 api` |
| Reiniciar la API | `docker compose -f docker-compose.prod.yml restart api` |
| Ver la salud | `curl https://DOMINIO/health/ready` |
| Monitor web (opcional) | `docker compose -f docker-compose.prod.yml --profile monitoreo up -d` → `http://servidor:3001` |
| Volver a una versión anterior | en `.env`, cambiar `IMAGEN_API`/`IMAGEN_WEB` al tag `:<sha>` de la versión buena y ejecutar `up -d` |

## Seguridad aplicada

- HTTPS obligatorio con HSTS y encabezados de seguridad (`nosniff`, `X-Frame-Options`, `Referrer-Policy`) desde Caddy.
- La API corre sin privilegios (`USER app`) y solo se expone a través de Caddy. La base de datos y Redis no tienen puertos publicados.
- La IP real del visitante llega a la API (`X-Forwarded-For`), así los límites por IP de la donación en línea y de las encuestas siguen funcionando detrás del proxy.
