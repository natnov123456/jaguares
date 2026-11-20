# Cumplimiento legal y registro de actividad

Revisión de los aspectos legales del sitio de la Fundación Unión de los Jaguares (La Paz, Bolivia) y de lo que se implementó en la plataforma.

> Este documento y los textos legales del sitio son una base preparada por el equipo de desarrollo. **No reemplazan la revisión de un abogado**: antes de la puesta en producción, la asesoría legal de la Fundación debe revisarlos y aprobarlos.

## 1. Normas que aplican al sitio

| Norma | Qué exige o protege | Cómo se atiende |
|---|---|---|
| Constitución Política del Estado, art. 21 (privacidad, intimidad, honra, propia imagen) y arts. 130–131 (Acción de Protección de Privacidad) | Que los datos personales se usen solo con consentimiento y que la persona pueda conocerlos, corregirlos o eliminarlos | Política de privacidad, consentimiento expreso en cada formulario, derechos de acceso, corrección y eliminación por correo |
| Ley N° 164 de Telecomunicaciones y TIC y su reglamento, D.S. N° 1793 | Protección de datos personales y de las comunicaciones en servicios electrónicos | Datos mínimos, conexión cifrada (HTTPS), contraseñas cifradas, registro de actividad, sin cesión a terceros |
| Código Niña, Niño y Adolescente, Ley N° 548 | Protección de la imagen y los datos de personas menores de edad | Cuentas de menores con autorización de madre, padre o tutor; fotos de menores solo con esa autorización |
| Ley N° 1322 de Derecho de Autor | Uso de obras (fotos, textos) con autorización de su autor | Revisión de imágenes (punto 8); aviso en la carga de fotos de la galería |
| Ley N° 453 de derechos de usuarios y consumidores | Información clara sobre servicios y cobros | Términos y Política de reembolsos publicados y enlazados desde la donación |

## 2. Revisión de los 9 puntos

| N° | Punto | ¿Era necesario? | Qué se hizo |
|---|---|---|---|
| 1 | Términos y condiciones | **Sí**: hay cuentas, inscripciones, certificados y donaciones | Página **/terminos** (cuentas, menores de edad, cursos y certificados, donaciones, uso correcto, imágenes, ley aplicable) |
| 2 | Política de privacidad | **Sí**: se recogen datos personales | Página **/privacidad**: responsable, base legal, qué datos se piden y para qué, registro de actividad, con quién se comparten, plazos de conservación, derechos y menores |
| 3 | Política de cookies y consentimiento | Política **sí**; **aviso de consentimiento no** | El sitio **no usa cookies** de publicidad, analítica ni seguimiento. Solo guarda la sesión en *localStorage* (estrictamente necesaria). Los videos usan YouTube sin cookies (*youtube-nocookie*) y Vimeo sin seguimiento (*dnt=1*). Página **/cookies** que lo explica |
| 4 | Dos casillas separadas en cada formulario | **Sí** | Registro, voluntariado, donación en línea y contacto tienen dos casillas **obligatorias y separadas**: Términos y condiciones (incluye la declaración de autorización si es menor de 18) y Política de privacidad. El servidor rechaza el formulario si falta alguna |
| 5 | Registro de cuándo y cómo se aceptó | **Sí** | Tabla `consentimientos` (de solo lectura): persona, formulario, documento, **versión**, fecha y hora, IP y navegador |
| 6 | Política de reembolso | **Sí**: hay donaciones en línea y las pasarelas de pago la exigen | Página **/reembolsos** (cobro duplicado, monto erróneo, uso no autorizado, error técnico; 30 días; devolución por el mismo medio), enlazada en el formulario de donación |
| 7 | Pedir solo los datos necesarios | Revisado: **ya se cumplía** | Registro: nombre, correo, contraseña y teléfono **opcional**. Voluntariado: nombre, correo y teléfono opcional. Donación: nombre y correo (solo sin sesión). Contacto: nombre, correo y mensaje. No se pide CI, fecha de nacimiento ni dirección |
| 8 | Derechos de las imágenes | Revisado | El logotipo y el ícono son dibujos propios (SVG). Se **eliminaron** 4 imágenes sin usar de la plantilla de Vite. Las fotos de los **datos de prueba** vienen de Picsum (licencia Unsplash, de uso libre) y son solo para demostración. En producción, la galería debe usar fotos propias; la pantalla de carga recuerda pedir la autorización de las personas fotografiadas y de madre, padre o tutor en el caso de menores |
| 9 | Leyes locales | Revisado | Ver la sección 1. Queda pendiente la revisión de un abogado (sección 4) |

## 3. Registro de actividad (quién entró, cuándo y qué hizo)

Todo lo que pasa en la plataforma queda en la tabla `registro_actividad` de la base de datos:

| Dato | Ejemplo |
|---|---|
| Fecha y hora | 20/11/2026 15:48:24 |
| Persona | Ana Quispe Mamani · ana.quispe@demo.jaguares.org · Participante |
| Acción | Inicio de sesión · Inicio de sesión fallido · Inscripción a un curso · Corrección de donación · Cierre de sesión |
| Detalle | Identificador del curso, evento o donación; filtros usados |
| Resultado | Correcto · Denegado (401/403) · Error |
| Origen | IP y navegador |
| Duración | Milisegundos que tardó la respuesta |

- Se registran **todas** las solicitudes a la API: de visitantes, participantes y personal, también los intentos fallidos y los accesos denegados.
- **No** se guardan contraseñas ni el contenido de los formularios. Los enlaces con token (encuestas, contraseñas) aparecen como `***`.
- Es de **solo lectura**: la base de datos rechaza cualquier modificación o borrado.
- Solo la **Directora** lo consulta en *Panel → Gestión → Registro de actividad*, con filtros, paginación y exportación a PDF o Excel.
- Se avisa a las personas en la Política de privacidad (sección 4) y en los Términos (sección 6).

Además, la plataforma ya tenía la **bitácora de donaciones** (altas, correcciones y anulaciones con valores anteriores y nuevos) y el **registro de exportaciones** (cada PDF o Excel generado).

## 4. Pendientes de la Fundación

1. Revisión y aprobación de los cuatro textos legales por su asesoría legal. Si cambian, actualizar la versión (manual técnico).
2. Formulario físico de **autorización de uso de imagen** para actividades con niñas, niños y adolescentes, firmado por madre, padre o tutor, y archivado por la Fundación.
3. Confirmar con la pasarela de pago, cuando se contrate, las condiciones de devolución, y ajustar la Política de reembolsos si hace falta.
4. Designar a la persona que responde las solicitudes de acceso, corrección y eliminación de datos (plazo publicado: 15 días hábiles).
5. Definir cómo depurar el registro de actividad después de 2 años, ya que la base no permite borrarlo desde la aplicación.
