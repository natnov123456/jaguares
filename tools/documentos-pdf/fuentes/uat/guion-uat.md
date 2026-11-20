# Pruebas de aceptación de usuario (UAT) con la Fundación (JAG-78)

**Objetivo:** que las personas que usarán la plataforma confirmen que hace lo acordado en el SRS, con sus propias tareas del día a día.

**Formato:** una sesión por rol (presencial en la oficina o por videollamada compartiendo pantalla), de 30 a 45 minutos cada una. La persona usa la plataforma **sola**: el equipo observa, toma tiempos y anota, pero no indica dónde hacer clic. Solo se ayuda si la persona queda bloqueada, y eso se anota como observación.

**Preparación (el día anterior):**
- Plataforma desplegada en el entorno de pruebas o en producción, con los datos iniciales cargados (JAG-76).
- Una cuenta por rol creada desde *Usuarios y roles* (el enlace para la contraseña llega al correo de cada persona).
- Un curso de prueba con cupos, un evento de prueba para hoy y 2 o 3 donaciones de ejemplo.
- Imprimir este guion, la planilla de observaciones y el acta.

---

## 1. Directora Ejecutiva — contenidos, indicadores y usuarios

| N° | Tarea que se le pide | Resultado esperado | ✔ / ✘ | Tiempo |
|---|---|---|---|---|
| D1 | Crear la cuenta del Contador con su correo y rol | Aparece en la lista; el Contador recibe el correo para definir su contraseña | | |
| D2 | Desactivar una cuenta de prueba y volver a activarla | Desactivada no puede iniciar sesión; activada sí | | |
| D3 | Abrir el *Panel de indicadores*, cambiar al mes anterior | Los 6 indicadores y los 2 gráficos cambian; cargan en ≤ 2 s | | |
| D4 | Exportar el panel de indicadores a PDF | Se descarga un PDF con encabezado de la Fundación y el mes elegido | | |
| D5 | Crear un curso y asignarle un docente | El curso aparece en el catálogo; el docente recibe un aviso | | |
| D6 | Publicar una noticia con imagen y guardar otra como borrador | La publicada se ve en el sitio; el borrador no | | |
| D7 | Crear un álbum, subir 3 fotos y tocar **Publicar** | El álbum aparece en *Galería* con miniaturas; al abrir una foto se ve sola y en grande | | |
| D8 | Revisar el *Registro de exportaciones* | Se ven los PDF/Excel generados en la sesión, con usuario y filtros | | |

## 2. Contador — donaciones, reportes y bitácora

| N° | Tarea | Resultado esperado | ✔ / ✘ | Tiempo |
|---|---|---|---|---|
| C1 | Iniciar sesión con el enlace del correo (definir contraseña) | Entra y ve solo: Donaciones, Campañas, Transparencia, Bitácora | | |
| C2 | Intentar abrir `/admin/cursos` escribiendo la dirección | Acceso denegado (no ve cursos) — CP-16 | | |
| C3 | Registrar un aporte en efectivo de 100 Bs. con correo del donante | Aparece en el detalle del mes; el donante recibe el comprobante PDF | | |
| C4 | Corregir ese aporte a 150 Bs. con un motivo | La bitácora muestra la edición con su nombre, la hora, 100 → 150 — CP-19 | | |
| C5 | Anular un aporte duplicado | Deja de sumar en el total del mes; queda en la bitácora con el motivo | | |
| C6 | Filtrar la bitácora por su usuario y la acción "Anulación" | Solo aparecen sus anulaciones | | |
| C7 | Exportar el reporte del mes a Excel y a PDF | Archivos con encabezado institucional, filtros usados y total | | |
| C8 | Publicar la transparencia del mes | El sitio muestra totales por categoría, sin nombres de donantes | | |

## 3. Encargado/a de operaciones — eventos, asistencia y voluntariado

| N° | Tarea | Resultado esperado | ✔ / ✘ | Tiempo |
|---|---|---|---|---|
| O1 | Crear un evento para hoy | Aparece en el calendario público | | |
| O2 | Escanear con el celular el QR de un inscrito (check-in) | "Asistencia registrada"; al escanearlo de nuevo, "ya registrada" | | |
| O3 | Registrar con el código escrito a mano a alguien que no trajo el celular | Queda registrado | | |
| O4 | Exportar la asistencia del evento a Excel | Archivo con nombre, correo, inscripción, asistencia y hora — CP-18 | | |
| O5 | Cerrar la asistencia del evento | Se envía la encuesta a los asistentes | | |
| O6 | Asignar 2 voluntarios a una actividad y registrar su horario (ej. de 15:30 a 18:15) | Se calcula el total (2 h 45 min); los voluntarios lo ven en su portal y las horas suman en su historial | | |
| O7 | Exportar las horas de voluntariado del mes | Archivo con voluntario, actividades y horas acumuladas | | |

## 4. Docente — su curso

| N° | Tarea | Resultado esperado | ✔ / ✘ | Tiempo |
|---|---|---|---|---|
| T1 | Entrar a *Mis cursos* | Ve solo los cursos que la Dirección le asignó | | |
| T2 | Pasar lista de la clase de hoy | Cada inscrito ve su porcentaje de asistencia | | |
| T3 | Exportar inscritos y asistencia del curso | PDF/Excel con el porcentaje de cada participante | | |
| T4 | Finalizar el curso | Solo quienes alcanzan la asistencia mínima reciben certificado | | |

## 5. Participante — inscripción, panel y certificado (incluye RNF-03)

| N° | Tarea | Resultado esperado | ✔ / ✘ | Tiempo |
|---|---|---|---|---|
| P1 | **Sin cuenta**, inscribirse en el curso de prueba (cronometrar, ver `rnf03-prueba-usabilidad-inscripcion.md`) | Inscripción exitosa en ≤ 3 min — CP-13 | | |
| P2 | Ver sus cursos y su asistencia en *Mi panel* | Aparece el curso con su porcentaje | | |
| P3 | Descargar su certificado y verificar el código en *Certificados* | El verificador muestra su nombre, curso y fecha | | |
| P4 | Inscribirse a un evento y mostrar su QR | El QR aparece en *Mi panel* | | |
| P5 | Responder la encuesta del correo | "¡Gracias por tu opinión!" | | |
| P6 | Intentar inscribirse en un curso que ya comenzó | Aparece "Ya comenzó · inscripciones cerradas" y no deja inscribirse | | |
| P7 | En *Iniciar sesión*, usar **¿Olvidaste tu contraseña?** y definir una nueva con el enlace del correo | Llega el correo; con la contraseña nueva entra y la anterior ya no sirve | | |
| P8 | Enviar una consulta desde **Quiénes somos → Contacto** | "¡Gracias!…"; la consulta llega al correo institucional | | |

---

## Clasificación de observaciones

Cada observación se anota en la planilla y se clasifica en la misma sesión:

| Tipo | Criterio | Qué se hace |
|---|---|---|
| **Defecto crítico** | Impide completar una tarea del guion o expone datos | Se corrige en el Sprint 3, antes del acta |
| **Defecto menor** | La tarea se completa, pero con un error visible (texto, formato, cálculo menor) | Se corrige en el Sprint 3 si hay tiempo; si no, queda en el acta como pendiente |
| **Mejora** | Funciona como se acordó, pero la persona propone otra forma | Se documenta para una fase futura (no bloquea la aceptación) |

## Planilla de observaciones

| N° | Rol | Tarea | Descripción (palabras de la persona) | Tipo | Responsable | Estado |
|---|---|---|---|---|---|---|
| 1 | | | | | | |
| 2 | | | | | | |
| 3 | | | | | | |
| 4 | | | | | | |
| 5 | | | | | | |

Los defectos se registran como **Bug** en Jira (proyecto JAG), enlazados a la historia afectada.

---

## Acta de aceptación

**Proyecto:** Plataforma web de la Fundación Unión de los Jaguares
**Fecha de la UAT:** ____/____/2026 · **Lugar o medio:** ______________________

**Participantes de la Fundación:**

| Nombre | Rol en la plataforma | Firma |
|---|---|---|
| | Directora Ejecutiva | |
| | Contador/a | |
| | Encargado/a de operaciones | |
| | Docente | |
| | Participante | |

**Resultados:**
- Tareas del guion completadas: ____ de 35.
- RNF-03 (inscripción ≤ 3 min, éxito ≥ 90 %): ☐ Cumple ☐ No cumple — tasa ____ %, tiempo máximo ____.
- Observaciones registradas: ____ (críticas ____ · menores ____ · mejoras ____).
- Defectos críticos pendientes: ☐ Ninguno ☐ Sí (detallar): ______________________________.

**Declaración:** La Fundación Unión de los Jaguares declara que la plataforma cumple lo acordado en la Especificación de Requisitos (SRS) para su puesta en producción, con las observaciones registradas en la planilla adjunta.

☐ **Aceptada** ☐ **Aceptada con observaciones** ☐ **No aceptada**

_______________________________
Directora Ejecutiva — Fundación Unión de los Jaguares

_______________________________
Responsable del equipo de desarrollo
