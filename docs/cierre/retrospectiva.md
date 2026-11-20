# Retrospectiva del equipo (Sprints 1 a 3)

**Fecha:** ____/____/2026 · **Participantes:** Natalia Novillo, Jhonattan Herbas, Luzmila Mamani

> Se completa en la reunión de retrospectiva. Los puntos de partida de abajo salen de lo que muestran el repositorio, Jira y el informe de evaluación. El equipo los confirma, corrige o agrega.

## Qué funcionó
- **Pruebas automáticas desde el Sprint 2:** cada cambio se valida con la suite completa en CI (172 pruebas, 95,6 % de cobertura). Así se detectaron conflictos al integrar trabajo de distintas personas (por ejemplo, los cursos creados por el docente frente a los asignados por la Dirección).
- **Convenciones comunes:** commits con la clave de Jira (`feat(JAG-xx): … - backend/frontend`), scripts SQL numerados e idempotentes y una sola fuente de permisos por rol (`permisos.js` + `[Authorize]`).
- **Historias con criterios Given-When-Then y casos de prueba CP-xx:** facilitaron convertir cada criterio en una prueba automática.
- Nota del Sprint 2 (75) por encima de la del Sprint 1 (60).

## Qué mejorar
- **Coordinación interna** (pedida en la evaluación): hubo cambios hechos sobre una copia sin actualizar del repositorio (*pull* tardío) y dos scripts SQL con el mismo número (`13_`).
- **Consistencia entre pantallas** (evaluación): los filtros y la exportación estaban resueltos en algunas pantallas y en otras no. Se corrigió con un módulo único (`BarraFiltros` + `BotonExportar` + `ReporteService`).
- **Jira al día:** subtareas que quedaron en "Por hacer" aunque su historia estaba terminada, y tareas asignadas a un integrante que ya no participaba.
- *(completar en la reunión)*

## Acciones para un próximo proyecto
| Acción | Responsable | Cómo sabremos que se cumple |
|---|---|---|
| `git pull` antes de empezar cada tarea y PR obligatorio a `main` con al menos una revisión | Todo el equipo | Ningún commit directo a `main` sin PR |
| Reservar el número del script SQL en el canal del equipo antes de crearlo | Quien crea el script | Cero choques de numeración |
| Revisión de Jira al cierre de cada daily (estado y responsable de subtareas) | Scrum Master del sprint | El tablero refleja el repositorio |
| Definir los componentes reutilizables (filtros, exportación) en el Sprint 1 | Equipo, en el refinamiento | Las pantallas nuevas los usan desde el inicio |
| *(completar)* | | |
