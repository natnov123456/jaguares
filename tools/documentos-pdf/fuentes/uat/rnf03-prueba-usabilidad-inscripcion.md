# RNF-03 · Prueba de usabilidad: inscripción a un curso

**Requisito:** un usuario nuevo, sin capacitación previa, completa la inscripción a un curso en **≤ 3 minutos**, con una **tasa de éxito ≥ 90 %** (caso de prueba CP-13).

## Mejoras aplicadas antes de la prueba (JAG-27)

| Problema detectado | Corrección |
|---|---|
| Un visitante que tocaba "Inicia sesión para inscribirte" y luego "Regístrate" terminaba en *Mi panel* y perdía el curso elegido. | El curso elegido viaja por inicio de sesión **y** registro (`/cursos?inscribir=<id>`). Al volver, el curso aparece resaltado con el botón **Confirmar inscripción**: un solo clic. |
| El botón decía "Inicia sesión para inscribirte" (pedía un paso sin explicar por qué). | Ahora dice **Inscribirme**, con la ayuda "Te pediremos iniciar sesión o crear tu cuenta (1 minuto)". |
| Con muchos cursos, encontrar uno llevaba tiempo. | El catálogo tiene buscador por nombre o tema, filtro de disponibilidad (con cupos o llenos) y rango de fechas de inicio. |
| Los formularios de ingreso y registro no tenían etiquetas asociadas ni autocompletado. | Etiquetas `label for`, `autocomplete` (el navegador completa el correo y el nombre) y foco automático en el primer campo. |
| Después de inscribirse no quedaba claro dónde ver el curso. | El mensaje de éxito indica que el curso aparece en "Mi panel". |

## Protocolo

- **Participantes:** al menos 10 personas que nunca usaron la plataforma (familias o participantes de la Fundación), con su propio celular o la computadora de la oficina.
- **Preparación:** un curso publicado con cupos (p. ej. "Taller de prueba RNF-03"). La persona **no** tiene cuenta.
- **Consigna (se lee igual a todos, sin más ayuda):** "Inscríbete en el *Taller de prueba RNF-03* desde la página de la Fundación".
- **Medición:** se cronometra desde que la persona ve la página de inicio hasta que aparece "Inscripción exitosa". El observador no ayuda; si la persona se rinde o pasan 5 minutos, se registra como **no completada**.
- **Verificación:** al terminar, el observador confirma en *Panel → Cursos* (o con el reporte *Inscritos y asistencia del curso*) que la inscripción quedó registrada.

## Planilla de registro

| N° | Dispositivo | ¿Completó? (sí/no) | Tiempo (mm:ss) | ¿Dónde dudó? | Observaciones |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| 6 | | | | | |
| 7 | | | | | |
| 8 | | | | | |
| 9 | | | | | |
| 10 | | | | | |

**Resultado:**
- Tasa de éxito = completadas / total = ____ % (meta ≥ 90 %)
- Tiempo de las completadas: mediana ____ · máximo ____ (meta: todas ≤ 3:00)
- ¿Cumple RNF-03? ☐ Sí ☐ No → si no, cada punto de duda se registra como observación en la UAT (JAG-78).

> La prueba con usuarios reales se hace en la misma sesión de la UAT con la Fundación (ver `guion-uat.md`). Este documento es el protocolo y la planilla; el resultado lo completa el equipo durante la sesión.
