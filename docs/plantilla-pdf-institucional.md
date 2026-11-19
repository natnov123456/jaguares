# Plantilla PDF institucional de reportes

Todos los reportes exportables de la plataforma (inscritos, asistencia, voluntariado, donaciones, bitácora, usuarios, indicadores, etc.) salen por **un único camino**, así todos se ven igual y cumplen las mismas reglas de seguridad.

```
Pantalla (botón Exportar) ──► GET /api/Reportes/{clave}?formato=pdf|xlsx&filtros…
                                   │
                         ReporteService (backend/src/FundacionJaguares.Application/Reportes)
                           1. Verifica permisos (rol + actividad propia)  → 403
                           2. Valida los filtros combinados               → 400
                           3. Arma la TablaReporte (IProveedorReportes)
                           4. Sin filas → "No hay datos para exportar"    → 422
                           5. PlantillaPdfInstitucional / GeneradorExcel
                           6. Registra la exportación (o el error)        → tabla exportaciones + log
```

## Estructura de la página

| Zona | Contenido | Notas |
|---|---|---|
| Franja superior | Barra color ámbar `#B45309` de 6 pt | En todas las páginas |
| Encabezado | "Fundación Unión de los Jaguares" (10 pt, gris) · Título del reporte (16 pt, negrita, ámbar) · Subtítulo (10 pt) | En todas las páginas |
| Metadatos visibles | "Generado el dd/MM/aaaa HH:mm (hora de Bolivia) · N registro(s)" | Hora de Bolivia (UTC-4) |
| Filtros | "Filtros: Búsqueda: "ana" · Estado: Confirmada · Período: …" o "Sin filtros (todos los registros)" | Solo los filtros usados |
| Tabla | Encabezado con fondo `#FEF3C7`, filas de 17 pt alternando fondo `#F9FAFB`, letra 8,5 pt | El encabezado de la tabla se repite en cada página |
| Totales | Al final de la última página, en negrita | Opcionales |
| Pie | "Fundación Unión de los Jaguares · Documento generado por la plataforma · Uso interno" y "Página X de Y" | En todas las páginas |

- **Tamaño:** A4. Vertical hasta 5 columnas; con más de 5 columnas se usa horizontal.
- **Márgenes:** 36 pt.
- **Columnas:** cada columna tiene un *peso* (ancho relativo). Las numéricas se alinean a la derecha.
- **Textos largos:** se recortan con "…" para no invadir la columna vecina. Si con la fuente real todavía no caben, se reduce la letra.

## Seguridad (correcciones de la evaluación)

- **Permisos antes de exportar:** cada reporte declara los roles que pueden pedirlo (`DefinicionReporte.Roles`). Además, la construcción rechaza actividades ajenas (por ejemplo, un docente no puede exportar el curso de otro docente). La verificación ocurre **antes** de consultar los datos.
- **Sin datos sensibles en el encabezado ni en los metadatos:** el encabezado no muestra quién generó el reporte. Los metadatos del PDF (`Author`, `Creator`, `Producer`) y las propiedades del Excel (`Author`, `LastModifiedBy`, `Company`) contienen solo el nombre de la fundación. Nunca llevan el nombre ni el correo del usuario.
- La respuesta se envía con `Cache-Control: no-store`, porque el cuerpo del reporte sí puede contener datos de participantes.

## Registro (logging)

Cada exportación PDF o Excel se guarda en la tabla `exportaciones` (`db/16_fuj124_exportaciones.sql`) y en el log de la aplicación (`[EXPORTACION] …`), con:

- usuario (id) y rol, fecha y hora, reporte, formato, **filtros usados** (JSON), cantidad de filas y duración;
- si el archivo no se pudo dibujar (**error de render**), se guarda `exito = false` con el tipo y el mensaje del error, y se registra con `LogError` junto con los filtros para poder reproducirlo.

La Dirección consulta y exporta ese registro desde *Panel → Registro de exportaciones* (reporte `exportaciones`).

## Filtros combinados

Las reglas están en `FiltroCombinado` (backend) y en `src/utils/filtros.js` (frontend), y son las mismas en ambos lados, así lo que se ve en pantalla es lo que se exporta:

- todos los filtros se combinan con **Y**; un filtro vacío no restringe;
- **texto:** sin distinguir mayúsculas ni tildes, busca en cualquiera de los campos del reporte;
- **estado:** igualdad exacta;
- **rango de fechas:** inclusivo, en hora de Bolivia; si hay rango, los registros sin fecha quedan fuera;
- `desde > hasta` o una búsqueda de más de 100 caracteres → 400.

## Cómo agregar un reporte nuevo

1. En el módulo correspondiente, implementar `IProveedorReportes` y devolver un `DefinicionReporte(clave, título, roles, construir)`.
2. En `construir`, consultar los datos, aplicar `FiltroCombinado.Aplicar(...)` y devolver una `TablaReporte` con `FiltroCombinado.Describir(...)` como filtros.
3. Registrar el proveedor en `Program.cs`: `builder.Services.AddScoped<IProveedorReportes, MiProveedor>();`
4. En la pantalla, usar `<BotonExportar reporte="clave" filtros={valores} />`.
5. Agregar pruebas de permisos y de combinaciones de filtros.

## Pruebas de regresión (snapshot)

`PlantillaPdfInstitucional.Maquetar()` calcula la posición de cada elemento sin depender de las fuentes instaladas. Las pruebas `ReportesUnitTests` comparan esa maqueta con los archivos de `backend/tests/FundacionJaguares.Tests/Snapshots/`. Si cambias la plantilla a propósito, regenera los snapshots y revisa el diff:

```bash
ACTUALIZAR_SNAPSHOTS=1 dotnet test backend/tests/FundacionJaguares.Tests --filter "FullyQualifiedName~Snapshot"
```
