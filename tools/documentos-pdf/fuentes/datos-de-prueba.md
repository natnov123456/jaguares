# Datos de prueba para demostrar la plataforma

Para mostrar que la plataforma funciona, se puede cargar un conjunto de datos de prueba que simula **seis meses de uso** de la Fundación. Los datos se cargan con la API real, así que cumplen todas las reglas del sistema: cupos y lista de espera, asistencia y certificados, bitácora de donaciones, encuestas y horas de voluntariado.

> Son datos ficticios con contraseñas conocidas. Sirven para demostraciones y pruebas, **nunca** para la base de producción real de la Fundación.

## 1. Cómo cargarlos

Requisitos: la API y la base de datos en marcha (con todos los scripts de `db/` aplicados) y Node.js 18 o superior.

```bash
cd tools/datos-demo
npm install
node cargar-datos-demo.mjs --api http://localhost:5055 --db "postgresql://postgres:postgres@localhost:5432/fundacion_jaguares"
```

- `--api`: dirección de la API (en el servidor, `https://dominio`).
- `--db`: conexión a la base. Se usa solo para llevar fechas al pasado (cursos terminados, eventos realizados, donaciones de meses anteriores).
- `--inseguro`: acepta el certificado local de `https://localhost` (prueba con Docker).
- `--sin-fotos`: no descarga las fotos de ejemplo de la galería.
- Si la base ya tiene datos de prueba, el cargador se detiene. `--forzar` carga otra vez.

La carga tarda de 1 a 3 minutos. Si la API frena las solicitudes por su límite de seguridad, el cargador espera y sigue solo.

## 2. Cuentas para entrar

| Rol | Correo | Contraseña |
|---|---|---|
| Directora Ejecutiva | `directora@jaguares.org` | `Admin123!` |
| Contador | `contador@jaguares.org` | `Contador123!` |
| Encargada de operaciones | `eventos@jaguares.org` | `Eventos123!` |
| Coordinadora de voluntariado | `coordinacion@jaguares.org` | `Coord123!` |
| Docente | `docente@jaguares.org` | `Docente123!` |
| Docente | `lucia.docente@jaguares.org` | `Docente123!` |
| Voluntaria | `voluntario@example.com` | `Vol123!` |
| Participante | `juan@example.com` | `Juan123!` |
| 18 participantes | `nombre.apellido@demo.jaguares.org` (ej. `ana.quispe@demo.jaguares.org`) | `Demo2026!` |

## 3. Qué se carga

| Módulo | Datos de prueba |
|---|---|
| Cursos | 3 terminados con asistencia por día, certificados y encuestas; 1 en curso; 1 que empieza hoy; 3 próximos (uno lleno, con lista de espera); 1 cancelado |
| Eventos | 4 realizados con check-in por QR, cierre de asistencia y encuestas; 3 próximos (uno reprogramado); 1 cancelado |
| Voluntariado | 8 voluntarios (uno inactivo), asignaciones a cursos y eventos con **horario** (ej. de 14:00 a 17:30 × 5 días = 17 h 30 min) y actividades registradas a mano |
| Donaciones | 3 campañas con meta y progreso; 44 donaciones en 6 meses (efectivo, transferencia y QR); 2 en línea con comprobante; 2 correcciones y 1 anulación en la bitácora |
| Transparencia | Publicada para los 3 meses anteriores, sin datos de donantes |
| Indicadores | Inscritos, asistencia, certificados, donaciones, voluntarios y satisfacción en varios meses |
| Sitio público | Quiénes somos completo, 6 noticias publicadas y 1 borrador, 3 álbumes publicados con fotos y video y 1 oculto, 2 consultas de contacto |

## 4. Recorrido sugerido para una demostración

1. **Visitante:** Inicio → Cursos (ver un curso "Ya comenzó" y uno lleno) → Calendario → Galería (abrir una foto) → Transparencia → Certificados (verificar un código).
2. **Participante** (`ana.quispe@demo.jaguares.org`): Mi panel con cursos, asistencia, certificados y QR de eventos.
3. **Directora:** Panel de indicadores (cambiar de mes) → Usuarios y roles → Galería (Publicar / Ocultar) → Exportaciones.
4. **Contador:** Donaciones del mes → Bitácora (ver la corrección y la anulación) → Transparencia.
5. **Coordinadora:** Asignaciones → registrar horas con horario.
6. **Docente:** Mis cursos → pasar lista en "Huerto urbano en casa".
