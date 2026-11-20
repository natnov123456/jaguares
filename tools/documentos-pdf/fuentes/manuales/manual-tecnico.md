# Manual técnico

Para el personal técnico que mantenga la plataforma después de la entrega.

## 1. Arquitectura (modelo C4)

### Nivel 1 — Contexto
```
 Visitantes / participantes / voluntarios ──┐
 Personal de la Fundación (5 roles) ────────┼──► Plataforma web Jaguares ──► Servidor de correo (SMTP)
                                            │                           └─► Pasarela de pago (QR/tarjeta)
```

### Nivel 2 — Contenedores
| Contenedor | Tecnología | Responsabilidad |
|---|---|---|
| Sitio web | React 19 + Vite, servido por nginx | Sitio público y paneles por rol |
| API | ASP.NET Core 10 (C#) | Lógica de negocio, autenticación JWT (60 min, bcrypt), reportes PDF/Excel |
| Base de datos | PostgreSQL 16 | Todos los datos; bitácora de auditoría de solo lectura (trigger) |
| Caché | Redis 7 | Panel de indicadores (10 min). Es opcional: sin Redis se usa memoria |
| Proxy | Caddy 2 | HTTPS automático (Let's Encrypt), redirección HTTP→HTTPS, encabezados de seguridad |
| Tarea programada | `RecordatoriosWorker` (dentro de la API) | Recordatorios 24 h antes, cada hora |
| Respaldos | contenedor `postgres` con `respaldo.sh` | `pg_dump` diario, retención de 30 días |

### Nivel 3 — Componentes de la API
```
Controllers (Api/Controllers)          → reciben HTTP, verifican el rol ([Authorize(Roles=...)])
  └─► Services (Application/Services)  → reglas de negocio (CursoService, DonacionService, UsuarioService,
  │                                       AuditoriaService, IndicadorService, RecordatorioService…)
  └─► Reportes (Application/Reportes)  → ReporteService + IProveedorReportes por módulo,
  │                                       PlantillaPdfInstitucional, GeneradorExcel, FiltroCombinado
  └─► Infrastructure                   → AppDbContext (EF Core + Npgsql), repositorios, adaptadores
                                          (correo, WhatsApp, pago, almacenamiento de archivos)
```
- **Frontend:** `src/config/permisos.js` define qué ve cada rol (debe coincidir con el backend). `src/utils/filtros.js`, `useFiltros` y `BarraFiltros` forman el módulo de filtros combinados. `BotonExportar` maneja las exportaciones.
- **Plantilla PDF:** ver `docs/plantilla-pdf-institucional.md`.

## 2. Estructura del repositorio
```
backend/src/…Api | Application | Domain | Infrastructure     código de la API
backend/tests/FundacionJaguares.Tests                      pruebas (unitarias, integración, E2E, snapshots)
backend/tools/FundacionJaguares.Importador                 carga de datos iniciales (JAG-76)
frontend/                                                  sitio React
db/NN_*.sql                                                scripts de base de datos, en orden
deploy/                                                    docker-compose, Caddyfile, scripts de migración/respaldo/monitoreo
docs/                                                      manuales, despliegue, pruebas, UAT, operación
```

## 3. Desarrollo local
```bash
# Base de datos: crear fundacion_jaguares y aplicar los scripts (con datos de demostración)
PGDATABASE=fundacion_jaguares INCLUIR_DATOS_DEMO=true deploy/scripts/aplicar-scripts.sh
cd backend && dotnet run --project src/FundacionJaguares.Api      # http://localhost:5055 (Swagger en /swagger)
cd frontend && npm install && npm run dev                          # http://localhost:5173
```
Pruebas: `cd backend && dotnet test tests/FundacionJaguares.Tests` (crea y borra su propia base) · `cd frontend && npm test`.

## 4. Despliegue, respaldos y restauración
Todo el procedimiento está en **`docs/despliegue.md`**. En resumen:
- **Desplegar:** push a `main` → GitHub Actions corre las pruebas, publica las imágenes y actualiza el servidor.
- **Respaldo:** automático todos los días a las 03:00 en `/opt/jaguares/deploy/respaldos` (30 días).
- **Restaurar:** `bash /scripts/restaurar.sh <archivo.dump>` dentro del servicio `respaldos` (pide confirmación).
- **Probar la restauración (mensual):** `bash /scripts/probar-restauracion.sh`.
- **Volver a una versión anterior:** cambiar `IMAGEN_API`/`IMAGEN_WEB` al tag del commit anterior en `deploy/.env` y `docker compose up -d`.

## 5. Monitoreo y disponibilidad
- `GET /health/live` y `GET /health/ready`. Verificación cada minuto con `deploy/monitoreo/verificar-salud.sh`.
- Reporte mensual: `deploy/monitoreo/reporte-disponibilidad.sh AAAA-MM` (meta ≥ 99,5 %). Ver `docs/operacion/disponibilidad-rnf04.md`.
- Logs: `docker compose logs api`. Las exportaciones quedan en la tabla `exportaciones`, y los errores de render con `[EXPORTACION]`.

## 6. Tareas frecuentes
| Tarea | Cómo |
|---|---|
| Agregar un script de base de datos | Crear `db/NN_descripcion.sql` idempotente (`IF NOT EXISTS`); se aplica solo en el siguiente despliegue |
| Agregar un reporte exportable | Ver "Cómo agregar un reporte nuevo" en `docs/plantilla-pdf-institucional.md` |
| Agregar un rol | Valor nuevo en `rol_enum` (script SQL), `Rol` (Domain), tabla `roles`, `permisos.js` y `[Authorize]` |
| Cambiar la clave JWT | Editar `JWT_SECRET` en `deploy/.env` y reiniciar la API (todas las sesiones se cierran) |
| Configurar el correo | `SMTP_*` en `deploy/.env` |
