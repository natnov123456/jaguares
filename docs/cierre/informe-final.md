# Informe final del proyecto (JAG-80)

**Proyecto:** Plataforma web de la Fundación Unión de los Jaguares · **Equipo:** G02 Jaguares (Chuby Boys)
**Periodo:** 23/10/2026 – 25/11/2026 · 3 sprints de 2 semanas

## 1. Historias completadas vs. planificadas

| Sprint | Fechas | Historias planificadas | Completadas | Puntos completados | Tareas (RNF / cierre) |
|---|---|---|---|---|---|
| Sprint 1 | 23/10 – 06/11 | 9 | 9 | **45** | — |
| Sprint 2 | 27/10 – 11/11 | 12 | 12 | **48** | 2 de 2 (RNF-01, RNF-02) |
| Sprint 3 | 11/11 – 25/11 | 6 | 6 | **21** | 9 de 9 (RNF-03, RNF-04, RNF-05, E2E, datos, despliegue, UAT, manuales, cierre) |
| **Total** | | **27** | **27** | **114** | 11 de 11 |

**Velocidad promedio:** 38 puntos por sprint (45 · 48 · 21). La baja del Sprint 3 es planificada: ese sprint concentró las tareas de cierre (despliegue, UAT, manuales, datos), que no se estiman en puntos.

### Cambios de alcance y de equipo en el Sprint 3
- Las subtareas de FUJ-122 (JAG-81, 82 y 83), asignadas inicialmente a un integrante que dejó de participar, se repartieron entre Natalia, Jhonattan y Luzmila.
- **Contraseñas y fechas de inscripción (20/11):** se agregó *¿Olvidaste tu contraseña?* (enlace de 1 hora por correo) y *Cambiar mi contraseña*; solo la propia persona puede cambiar su contraseña y solo la Dirección da de baja cuentas. Ya no se admiten inscripciones a cursos que comenzaron. Los correos (confirmaciones, recordatorios, comprobantes, encuestas y formulario de contacto) funcionan con el SMTP de la Fundación.
- Se incorporaron al Sprint 3 las correcciones del **informe de evaluación de los Sprints 1 y 2** (notas: 60 y 75): filtros combinados reutilizables, exportación con PDF institucional en todas las pantallas, log de cada PDF generado y de los errores de render, pruebas de combinaciones de filtros y de regresión (snapshot) del PDF, y verificación de permisos antes de exportar sin datos sensibles en los metadatos.

## 2. Cumplimiento de los requisitos no funcionales

| RNF | Meta | Evidencia | Estado |
|---|---|---|---|
| RNF-01 Rendimiento | 95 % de solicitudes en ≤ 2 s con 200 usuarios | Prueba de carga del Sprint 2 (JAG-25). El panel de indicadores responde en ≤ 2 s con caché Redis (prueba CP-17). | ✅ |
| RNF-02 Seguridad | JWT ≤ 60 min, bcrypt ≥ 10, OWASP Top 10 | JWT de 60 min, bcrypt con factor 11, roles verificados en cada endpoint y además en cada solicitud (cuenta activa y rol vigente), bitácora inmutable, HTTPS con HSTS, secretos fuera del código | ✅ |
| RNF-03 Usabilidad | Inscripción ≤ 3 min, éxito ≥ 90 % | Flujo de inscripción con un clic tras registrarse (JAG-27). Se mide con usuarios reales en la UAT. | ⏳ Pendiente de la sesión UAT |
| RNF-04 Disponibilidad | ≥ 99,5 % mensual | `/health/live` y `/health/ready`, reinicio automático, monitoreo cada minuto y reporte mensual (JAG-28) | ⏳ Se mide al cierre del primer mes en producción |
| RNF-05 Mantenibilidad | Cobertura ≥ 70 % | **95,6 %** de líneas (172 pruebas del backend, 6 del frontend), umbral exigido en CI | ✅ |

## 3. Calidad
- 172 pruebas automatizadas del backend (unitarias, de integración por módulo, de punta a punta y snapshot del PDF) y 6 del frontend. Todas pasan (`docs/pruebas/reporte-regresion.md`).
- Casos CP-01 a CP-21: todos aprobados, salvo CP-13 (UAT) y CP-14 (primer mes en producción), que dependen de la Fundación y del tiempo.
- Ningún bug crítico ni alto abierto.

## 4. Entregables
| Entregable | Ubicación |
|---|---|
| Código fuente (API, sitio, pruebas, importador) | Repositorio `natnov123456/jaguares`, rama `main` |
| Despliegue (Docker, Caddy HTTPS, CI/CD, respaldos) | `deploy/`, `.github/workflows/`, `docs/despliegue.md` |
| Carga de datos iniciales | `backend/tools/FundacionJaguares.Importador`, `docs/datos-iniciales.md` |
| Manuales por rol, guía de participantes, manual técnico, taller | `docs/manuales/` |
| UAT: guion, planilla y acta | `docs/uat/` |
| Reporte de pruebas | `docs/pruebas/reporte-regresion.md` |
| Plantilla PDF institucional | `docs/plantilla-pdf-institucional.md` |

## 5. Pendientes que dependen de la Fundación (no de desarrollo)
1. Dominio oficial y servidor contratados → primer despliegue con `docs/despliegue.md`.
2. Planillas reales para la carga de datos iniciales.
3. Sesión de UAT y firma del acta de aceptación (incluye la medición de RNF-03).
4. Taller de capacitación (2 h) con su grabación y la lista de asistencia firmada.
5. Credenciales de la pasarela de pago y del correo institucional (SMTP).
