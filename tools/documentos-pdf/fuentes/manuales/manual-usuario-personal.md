# Manual de usuario del personal

Plataforma web de la Fundación Unión de los Jaguares · versión 1.0 · noviembre de 2026

Este manual explica, por rol, cómo hacer las tareas del día a día. Cada persona ve en el menú **solo los módulos de su rol**.

> 📷 Las marcas *[Captura: …]* indican dónde va una captura de pantalla. Se toman en el entorno de producción con datos reales antes de exportar el manual a PDF.

---

## 0. Primeros pasos (todos los roles)

**Activar la cuenta.** La Dirección crea tu cuenta y te llega un correo *"Tu cuenta en la Fundación Unión de los Jaguares"*. Abre el enlace (vale 24 horas y sirve una sola vez) y define tu contraseña: al menos 8 caracteres, con letras y números. Si el enlace venció, pide a la Dirección que te lo reenvíe. *[Captura: pantalla "Define tu contraseña"]*

**Tu contraseña es solo tuya.** Nadie más puede cambiarla, ni la Dirección. Para cambiarla: *menú de tu nombre → Cambiar mi contraseña* (pide la actual). Si la olvidaste: en *Iniciar sesión* toca **¿Olvidaste tu contraseña?**, escribe tu correo y abre el enlace que te llega (vale 1 hora y sirve una sola vez).

**Ingresar.** *Ingresar* → correo y contraseña. Llegas al **Panel de control**, que muestra las tarjetas de tus módulos. *[Captura: panel de control]*

**La sesión dura 60 minutos.** Si la Dirección te cambia el rol o desactiva tu cuenta, la sesión se cierra en ese momento.

**Filtros y exportar (en todas las pantallas con listas).** Arriba de cada lista está la barra de filtros:
- **Buscar:** escribe parte del nombre, correo o título. No importan las mayúsculas ni las tildes.
- **Estado** y **rango de fechas** (*desde / hasta*): se combinan con la búsqueda.
- Debajo aparece "N de M resultados" y el botón **Limpiar filtros**.
- **Exportar: ⬇ PDF / ⬇ Excel** descarga exactamente lo que ves, con el encabezado de la Fundación y los filtros usados. Si no hay datos, aparece "No hay datos para exportar". *[Captura: barra de filtros con exportar]*

Los filtros quedan en la dirección de la página: si copias el enlace, la otra persona ve la misma lista filtrada.

---

## 1. Dirección (Directora Ejecutiva)

Tiene acceso a todos los módulos.

### 1.1 Usuarios y roles
*Panel → Usuarios y roles* *[Captura]*
1. **Nueva cuenta del personal:** nombre, correo, teléfono (opcional) y rol → *Crear y enviar enlace*. Debajo del rol se describe qué puede hacer.
2. **Cambiar el rol:** elige otro rol en la lista de esa persona. Deberá volver a iniciar sesión.
3. **Desactivar o activar (dar de baja):** solo la Dirección puede hacerlo. *Desactivar* bloquea el ingreso de inmediato. Nunca puedes desactivarte ni cambiarte el rol a ti misma, y siempre debe quedar una cuenta de Dirección activa.
4. **Reenviar enlace:** si la persona no definió su contraseña a tiempo. La Dirección **no** ve ni cambia contraseñas: la persona la define con el enlace o con *¿Olvidaste tu contraseña?*.

| Rol | Puede |
|---|---|
| Contador/a | Donaciones (registrar, corregir, anular), reporte financiero, campañas, transparencia, bitácora |
| Encargado/a de operaciones | Eventos, check-in QR, voluntariado, registro de efectivo |
| Coordinador/a de voluntariado | Voluntarios, asignaciones y horas |
| Docente | Los cursos que la Dirección le asigna |

### 1.2 Panel de indicadores
*Panel → Panel de indicadores* *[Captura]*
- Elige el **mes**. Las 6 tarjetas (inscritos, asistencia promedio, certificados, donaciones con su variación respecto al mes anterior, voluntarios activos y satisfacción) y los 2 gráficos se recalculan.
- *Ver como tabla* muestra los valores del gráfico de donaciones.
- Los datos se actualizan cada 10 minutos. Al pie se indica cuándo se calcularon.
- **Exportar** genera el PDF del panel para presentar a financiadores.

### 1.3 Cursos
*Panel → Cursos de la fundación* *[Captura]*
- **+ Crear curso:** título, descripción, **docente a cargo**, fechas, cupo, asistencia mínima para certificar y plantilla del certificado.
- La columna *Docente* permite reasignar el curso. El docente nuevo recibe un aviso por correo.
- *Asistencia y datos* abre el detalle del curso (ver 4. Docente).
- *Reprogramar* / *Cancelar* avisan por correo a los inscritos y a la lista de espera.
- *Finalizar y certificar* emite los certificados (solo a quien alcanza la asistencia mínima) y envía la encuesta.

### 1.4 Contenidos del sitio
- **Noticias:** lista de publicadas y borradores (con filtros y exportación) y formulario *Nueva noticia*. Desmarca *Publicar ahora* para guardar un borrador.
- **Galería:** crea álbumes, sube fotos (JPG/PNG de hasta 5 MB) o enlaces de YouTube/Vimeo. Arriba del álbum, un solo botón lo **Publica** (se ve en el sitio) o lo **Oculta**. En el sitio, al tocar una foto se abre sola, a pantalla completa; con las flechas se pasa a la siguiente y con Esc se cierra.
- **Quiénes somos:** misión, visión, historia, equipo y datos de contacto.
- **Plantillas de certificado:** logo, firmas y texto por tipo de actividad, con vista previa. Abajo se exportan los **certificados emitidos**.

### 1.5 Registro de exportaciones
*Panel → Registro de exportaciones*: cada PDF o Excel generado en la plataforma, con quién lo hizo, cuándo, con qué filtros y si hubo un error. Sirve para auditoría.

### 1.6 Registro de actividad
*Panel → Gestión → Registro de actividad*: todo lo que pasa en la plataforma, guardado en la base de datos. Cada fila muestra **fecha y hora**, **persona** (nombre, correo y rol), **acción** (por ejemplo "Inicio de sesión", "Inicio de sesión fallido", "Inscripción a un curso", "Corrección de donación", "Cierre de sesión"), **detalle** (qué curso, evento o donación), **resultado** (correcto, denegado o error) y **origen** (IP y navegador).

- Filtra por persona, correo, acción o IP, por resultado (por ejemplo, solo los intentos fallidos) y por rango de fechas. **Exportar** genera el PDF o Excel (sin rango, los últimos 30 días).
- No se guardan contraseñas ni el contenido de los formularios, y los enlaces con token aparecen como `***`.
- Nadie puede modificar ni borrar el registro, ni siquiera desde la base de datos.

### 1.7 Documentos legales
El pie de página del sitio enlaza los **Términos y condiciones**, la **Política de privacidad**, la **Política de cookies** y la **Política de reembolsos**. Los formularios de registro, voluntariado, donación en línea y contacto tienen **dos casillas obligatorias y separadas** (Términos y Privacidad). Cada aceptación queda guardada con fecha, hora, formulario, versión, IP y navegador. Si la Fundación cambia los textos, hay que actualizar la fecha de versión (ver el manual técnico).

---

## 2. Contador/a

### 2.1 Donaciones
*Panel → Donaciones* *[Captura]*
- Elige **mes y año**. Arriba ves el *Total recaudado*.
- **Registrar efectivo** (columna derecha): monto, recibo, nombre y correo del donante (opcionales) y campaña. Si pones el correo, el donante recibe su comprobante PDF.
- **Corregir:** cambia el monto, el medio o la referencia, con un motivo opcional.
- **Anular:** el motivo es obligatorio. La donación deja de sumar en el reporte, las campañas y la transparencia.
- Las correcciones y anulaciones quedan registradas en la bitácora con tu nombre y la hora.
- Filtros: N° de comprobante o referencia, estado (confirmadas / anuladas) y medio. **Exportar** genera el reporte financiero del mes en PDF o Excel (CP-06).

### 2.2 Bitácora de auditoría
*Panel → Bitácora de auditoría* *[Captura]*
- Cada alta, corrección o anulación, con fecha y hora, comprobante, acción, usuario y motivo.
- Filtros: rango de fechas, **usuario**, **acción** y texto (N° de comprobante, nombre o motivo).
- *Antes / después* muestra el valor anterior y el nuevo, y resalta lo que cambió.
- La bitácora **no se puede modificar ni borrar**: ni desde la plataforma ni directamente en la base de datos.

### 2.3 Campañas
Crea campañas con nombre, meta y fecha límite. Se publican en el sitio con su barra de avance. Se filtran por vigentes o cerradas y se exportan.

### 2.4 Transparencia
Elige el mes, revisa el total recaudado y reparte el destino de los fondos por categoría. *Publicar* lo muestra en el sitio **sin datos de donantes**. Abajo puedes exportar los meses publicados.

---

## 3. Encargado/a de operaciones

### 3.1 Eventos
*Panel → Eventos*: crea eventos (título, lugar, inicio y fin), reprográmalos o cancélalos (se avisa a los inscritos). Debajo de la lista: *Inscritos y asistencia del evento* → elige un evento → **Exportar** (Excel o PDF con nombre, correo, inscripción, asistencia y hora de ingreso).

### 3.2 Check-in con QR (asistencia a eventos)
*Panel → Check-in QR* desde el **celular** *[Captura]*:
1. Elige el evento y toca *Activar cámara*.
2. Apunta al QR del participante (lo tiene en *Mi panel* o en el correo de recordatorio). Verás "Asistencia registrada" con su nombre.
3. Si vuelve a pasar, aparece "La asistencia ya fue registrada": no se duplica.
4. Si no trajo el celular: escribe su código en *O escribe el código*.
5. Abajo, la lista de inscritos con la hora de ingreso, con filtros y **Exportar asistencia**.

Al terminar el evento, en *Eventos* → **Cerrar asistencia**: se envía la encuesta de satisfacción a quienes asistieron.

### 3.3 Voluntariado y efectivo
- **Voluntarios** y **Asignaciones**: igual que el Coordinador de voluntariado (sección 5).
- **Donaciones:** registras el efectivo que recibes y ves y exportas solo lo que tú registraste.

---

## 4. Docente

*Panel → Mis cursos*: solo los cursos que la Dirección te asignó.
- **Asistencia** (detalle del curso) *[Captura]*: elige el día y marca *Presente / Ausente* a cada inscrito. Todos empiezan como presentes, así que solo tocas a los ausentes. Hay botones *Todos presentes / Todos ausentes*. Al guardar, cada participante recibe un correo con su porcentaje. Puedes corregir un día ya registrado.
- **Exportar inscritos y asistencia:** PDF o Excel con el porcentaje de cada persona y quiénes alcanzan el mínimo.
- **Datos del curso:** título, descripción, cupo y asistencia mínima. Las fechas se cambian con *Reprogramar*, que avisa a los inscritos.
- **Finalizar y certificar:** emite certificados solo a quienes alcanzan la asistencia mínima y envía la encuesta.

---

## 5. Coordinador/a de voluntariado

- **Voluntarios:** registro único con contacto, estado, horas aportadas e historial. Filtros por nombre, estado y fecha de postulación. Hay dos exportaciones: **Exportar** (listado) y **Exportar horas**, con un rango de fechas: voluntario, actividades y horas acumuladas.
- **Asignaciones:** elige una actividad, marca a los voluntarios y la tarea → *Asignar* (cada voluntario recibe un aviso y lo ve en su portal). Cuando la actividad termina, en *Horas por registrar* anota el **horario** de cada uno: hora de inicio y de fin (ej. de 15:30 a 18:15) y, en los cursos, cuántos días colaboró con ese horario. El sistema calcula el total al momento ("= 2 h 45 min") y lo suma a su historial. Las horas se muestran siempre en horas y minutos, nunca con decimales.

---

## Preguntas frecuentes

| Pregunta | Respuesta |
|---|---|
| No me llegó el correo para la contraseña | Revisa el correo no deseado. Si pasaron 24 h, pide a la Dirección *Reenviar enlace*. |
| Olvidé mi contraseña | *Iniciar sesión → ¿Olvidaste tu contraseña?* y abre el enlace del correo (1 hora). |
| "No tienes permiso para exportar este reporte" | Tu rol no incluye ese módulo. Pide el reporte a quien corresponde. |
| "No hay datos para exportar" | Con esos filtros no hay registros. Usa *Limpiar filtros* o amplía las fechas. |
| Me sacó de la sesión | Pasaron 60 minutos o la Dirección cambió tu rol. Vuelve a ingresar. |
| Me equivoqué al registrar una donación | Pide al Contador que la **corrija** o la **anule**. Nunca se borra: queda en la bitácora. |
