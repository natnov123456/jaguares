# Carga de datos iniciales de la Fundación (JAG-76)

Objetivo: que la plataforma arranque con la información real de la Fundación (personal, voluntarios, cursos vigentes, donantes frecuentes y noticias recientes), sin duplicados y con formatos normalizados.

## 1. Preparar las planillas

1. Pedir a la Fundación sus planillas Excel actuales.
2. Pasar cada una a la plantilla correspondiente de `docs/datos-iniciales/plantillas/` (mismos encabezados) y guardarla como **CSV UTF-8** en una carpeta, por ejemplo `datos-fundacion/`. Las columnas pueden ir separadas por `;` o `,`.

| Archivo | Columnas | Qué crea |
|---|---|---|
| `cuentas_internas.csv` | nombre; correo; rol; telefono | Cuentas del personal. Rol: *Directora, Contador, Encargado de operaciones, Coordinador de voluntariado, Docente*. Se crean sin contraseña: la Directora les asigna una desde *Usuarios y roles → Contraseña* (FUJ-122). |
| `voluntarios.csv` | nombre; correo; telefono; estado; fecha_postulacion | Registro único de voluntarios (se enlaza a la cuenta si ya existe una con ese correo). |
| `cursos.csv` | titulo; descripcion; fecha_inicio; fecha_fin; cupo; asistencia_minima; correo_docente | Cursos vigentes. El docente debe estar en `cuentas_internas.csv`. El estado se calcula según las fechas. |
| `donaciones.csv` | fecha; monto; medio; referencia; donante; correo_donante | Historial de donantes frecuentes. Cada donación queda también en la bitácora de auditoría como "Importación de datos iniciales". |
| `noticias.csv` | titulo; resumen; contenido; imagen_url; fecha_publicacion; publicada | Noticias recientes (publicada: sí/no). |

**Normalización automática:** nombres en mayúscula inicial y sin espacios dobles; correos en minúsculas y validados; teléfonos al formato `+591 7xxxxxxx` (acepta espacios, guiones o el prefijo 591); fechas `dd/mm/aaaa` o `aaaa-mm-dd`; montos `1.234,50` o `1234.50`.

> La plataforma no guarda el CI de las personas: las cuentas se identifican por el correo. Si la planilla de la Fundación trae CI, no se carga. Así no se guardan datos personales que no se usan.

## 2. Verificar (no escribe nada)

```bash
cd backend
JAGUARES_CONEXION="Host=...;Database=fundacion_jaguares;Username=...;Password=..." \
  dotnet run --project tools/FundacionJaguares.Importador -- ../datos-fundacion
```

Muestra por archivo cuántas filas hay, cuántas son válidas, cuántas están duplicadas dentro del archivo, cuántas ya existían en la base y los **errores con su número de línea** (correo inválido, fecha imposible, rol desconocido, docente sin cuenta…). Corregir la planilla y repetir hasta que no haya errores.

## 3. Importar

```bash
JAGUARES_CONEXION="..." \
  dotnet run --project tools/FundacionJaguares.Importador -- ../datos-fundacion --aplicar
```

- **Se puede ejecutar varias veces:** lo que ya se cargó no se duplica (cuentas y voluntarios por correo; cursos por título y fecha de inicio; donaciones por fecha, monto, referencia y donante; noticias por título).
- Al final, la columna **Conteo** compara las filas válidas del archivo con los registros en la base: debe decir `OK` en todos. Ese es el control de **conteos origen = destino** que se valida con la Encargada de Operaciones.
- Las cuentas internas importadas no pueden iniciar sesión hasta que la Directora les asigne una contraseña desde *Panel → Usuarios y roles → Contraseña*. No se envían correos.

Ejemplo real (con las plantillas de ejemplo, contra una base recién migrada):

```
Archivo                 Origen  Válidas  Dupl.  Ya había  Importadas  En base  Conteo
cuentas_internas.csv         5        5      0         0           5        5  OK
voluntarios.csv              1        1      0         0           1        1  OK
cursos.csv                   1        1      0         0           1        1  OK
donaciones.csv               1        1      0         0           1        1  OK
noticias.csv                 1        1      0         0           1        1  OK
```

En producción se ejecuta desde el servidor con el contenedor de la API detenido o en una ventana de mantenimiento, con la cadena de conexión de `deploy/.env`.

## 4. Privacidad

- Los datos de donantes solo se ven en el área financiera (Contador y Directora). El sitio público de transparencia muestra **solo totales** por mes y categoría. Una prueba automática verifica que, después de importar, ningún dato de donante aparece en la API pública.
- Las planillas con datos personales no se suben al repositorio: quedan en el equipo de la Fundación y se borran del servidor después de importarlas.

## 5. Cuentas internas

Las cuentas del personal (Directora, Contador, Encargado/a de operaciones, Coordinador/a de voluntariado y docentes) se crean con `cuentas_internas.csv`. **No** se usan las cuentas de demostración de `db/02_seed_usuarios.sql`: el despliegue de producción no las carga.
