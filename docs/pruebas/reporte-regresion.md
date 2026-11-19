# Reporte de pruebas de regresión e integración (JAG-75)

**Fecha de ejecución:** 19/11/2026 · **Rama:** `main` · **Entorno:** PostgreSQL 16 aislado por ejecución (`jaguares_test_xxxx`), API completa en memoria (WebApplicationFactory), correos capturados.

## Resultado general

| Suite | Pruebas | Aprobadas | Fallidas |
|---|---|---|---|
| Backend (`dotnet test backend/tests/FundacionJaguares.Tests`) | 169 | **169** | 0 |
| Frontend (`npm test`: filtros combinados) | 6 | **6** | 0 |
| Cobertura de líneas del backend (RNF-05, meta ≥ 70 %) | — | **95,6 %** (ramas 77,9 %) | — |

Se ejecutan automáticamente en cada push y pull request (`.github/workflows/backend-tests.yml`) y antes de cada despliegue (`despliegue.yml`). Si una falla, no se despliega.

## Flujos de punta a punta (`Integracion/FlujosE2ETests.cs`)

| Flujo | Pasos verificados | Resultado |
|---|---|---|
| **Curso** | Directora crea y asigna docente → persona nueva se registra e inscribe (cupo baja en 1, correo de confirmación) → recordatorio 24 h antes → docente pasa lista (100 %) → finaliza → certificado emitido y verificable en público → encuesta respondida → resultado visible para el docente → reporte exportable | ✅ |
| **Evento** | Inscripción con QR único → recordatorio con el QR adjunto → check-in con el QR (registra; repetido → 409 "ya registrado") → cierre de asistencia → encuesta → asistencia en el Excel exportado | ✅ |
| **Donación** | Donación en línea sin cuenta → comprobante PDF por correo → registro ALTA en la bitácora → reporte mensual del Contador y exportación → publicación de transparencia → el sitio público muestra el total **sin** nombre, correo ni referencia del donante | ✅ |

## Casos de prueba CP-01 a CP-21

| CP | Historia / RNF | Prioridad | Cómo se verifica | Resultado |
|---|---|---|---|---|
| CP-01 | FUJ-101 Inscripción con cupos | Must | `CursosTests.Inscripcion_con_cupos_lista_de_espera_y_promocion_al_cancelar`, flujo E2E de curso | ✅ |
| CP-02 | FUJ-102 Asistencia a evento | Must | `EventosTests.Asistencia_manual_y_cierre_con_encuesta`, `EventosTests.Checkin_QR_…`, flujo E2E de evento | ✅ |
| CP-03 | FUJ-103 Certificado automático | Must | `CursosTests.Finalizar_curso_emite_certificados_…`, `AsistenciaCursosTests` (solo con asistencia mínima) | ✅ |
| CP-04 | FUJ-104 Verificación pública | Must | `CursosTests.Finalizar_…` (código válido / "no encontrado"), flujo E2E de curso | ✅ |
| CP-05 | FUJ-105 Registro de donación | Must | `DonacionesTests.Registro_manual_valida_monto_medio_y_donante` | ✅ |
| CP-06 | FUJ-106 Reporte financiero mensual | Must | `DonacionesTests.Reporte_mensual_solo_para_la_directora`, `ReportesFinanzasTests`, flujo E2E de donación | ✅ |
| CP-07 | FUJ-107 Registro de voluntarios | Should | `VoluntariadoTests.Postulacion_publica_sin_duplicados_…` | ✅ |
| CP-08 | FUJ-108 Noticias | Should | `ContenidoTests.Noticias_publicadas_aparecen_…`, `ReportesContenidoTests` | ✅ |
| CP-09 | FUJ-109 Donación en línea | Must | `DonacionesTests.Donacion_en_linea_publica_y_con_sesion`, flujo E2E de donación | ✅ |
| CP-10 | FUJ-110 Confirmación automática | Must | Flujo E2E de curso (correo "Inscripción confirmada" al instante) | ✅ |
| CP-11 | RNF-01 Rendimiento | Must | Prueba de carga del Sprint 2 (JAG-25). Además, `IndicadoresTests.CP17_…` verifica la respuesta del panel en ≤ 2 s. | ✅ (ver JAG-25) |
| CP-12 | RNF-02 Seguridad | Must | `AuthTests.Endpoint_protegido_sin_token_devuelve_401_y_con_otro_rol_403`, `UsuariosTests.Desactivar_o_cambiar_rol_invalida_la_sesion_abierta` | ✅ |
| CP-13 | RNF-03 Usabilidad | Must | Prueba con usuarios reales en la UAT (`docs/uat/rnf03-prueba-usabilidad-inscripcion.md`) | ⏳ Pendiente de la sesión con la Fundación |
| CP-14 | RNF-04 Disponibilidad | Must | `SaludTests` + `deploy/monitoreo/reporte-disponibilidad.sh` al cierre del primer mes en producción | ⏳ Se mide en producción |
| CP-15 | RNF-05 Cobertura ≥ 70 % | Must | CI con umbral: **95,6 %** | ✅ |
| CP-16 | FUJ-122 Usuarios y roles | Must | `UsuariosTests.CP16_contador_no_entra_a_cursos_pero_si_a_donaciones` y demás reglas | ✅ |
| CP-17 | FUJ-123 Panel de indicadores | Should | `IndicadoresTests.CP17_…` (cifras = consultas SQL directas, ≤ 2 s, caché) | ✅ |
| CP-18 | FUJ-124 Exportación | Should | `ReportesTests.CP18_asistencia_de_un_evento_con_20_participantes_en_excel` | ✅ |
| CP-19 | FUJ-125 Bitácora | Must | `AuditoriaTests.CP19_editar_el_monto_de_100_a_150_…` | ✅ |
| CP-20 | FUJ-126 Galería | Could | `ContenidoTests.Galeria_albumes_imagenes_con_miniatura_videos_y_errores_claros` | ✅ |
| CP-21 | FUJ-127 Contacto | Could | `ContenidoTests.Contacto_valida_campos_y_descarta_bots` (correo capturado al instante) | ✅ |

**Must aprobados:** 13 de 15 automatizables. CP-13 y CP-14 dependen de la sesión con usuarios reales y del primer mes en producción. **Should/Could:** 6 de 6 (100 %).

## Correcciones de la evaluación verificadas por pruebas

| Hallazgo | Pruebas |
|---|---|
| Filtros combinados consistentes en cada pantalla | `ReportesUnitTests` (texto + estado + rango, tildes, hora de Bolivia), `frontend/src/utils/filtros.test.js` (mismas reglas), `ReportesTests`, `ReportesGestionTests`, `ReportesFinanzasTests`, `ReportesContenidoTests`, `ReportesUsuariosTests`, `ReportesCertificadosTests` |
| Exportación PDF institucional en todas las pantallas | 19 reportes exportables con permisos y "No hay datos para exportar" |
| Log de cada PDF y de errores de render | `ReportesTests` (registro con usuario, filtros y filas), `ReportesErrorRenderTests` |
| Regresión (snapshot) del PDF | `ReportesUnitTests.Snapshot_reporte_de_una_pagina`, `…_varias_paginas_horizontal` |
| Permisos antes de exportar y metadatos sin datos sensibles | `ReportesTests.Permisos_se_verifican_antes_de_exportar`, `ReportesUnitTests.Pdf_valido_sin_datos_de_quien_lo_genero_en_los_metadatos`, `Excel_tiene_…_propiedades_institucionales` |

## Defectos encontrados en esta regresión

| Defecto | Corrección |
|---|---|
| Un usuario nuevo que pasaba por "Regístrate" perdía el curso elegido (afectaba RNF-03) | JAG-27: el curso se conserva en todo el flujo |
| Editar las fechas del curso desde "Datos del curso" no avisaba a los inscritos | Las fechas se cambian solo con "Reprogramar" (FUJ-119) |
| El reporte financiero mensual no se podía descargar | Exportación PDF/Excel institucional |
| Un token seguía válido hasta 60 min después de desactivar la cuenta | Validación de cuenta activa y rol en cada solicitud |
| Detrás del proxy, los límites por IP verían una sola IP para todos | `X-Forwarded-For` habilitado para el proxy interno |

No quedan bugs críticos ni altos abiertos.

## Cómo repetir la regresión

```bash
cd backend && dotnet test tests/FundacionJaguares.Tests          # o .\tests\cobertura.ps1 para ver la cobertura
cd frontend && npm test && npm run build
```
