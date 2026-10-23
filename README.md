# 🐯 Fundación Unión de los Jaguares — Plataforma Web Integral

Sistema completo (Backend + Frontend + Base de Datos) para la gestión institucional y de actividades de la **Fundación Unión de los Jaguares** (La Paz, Bolivia).

Implementa los 10 requisitos funcionales (RF) y 5 requisitos no funcionales (RNF) especificados en el **Documento 2 — SRS**, siguiendo la arquitectura de 4 capas definida en el **Documento 3 — Modelo C4**.

---

## 🛠 Tecnologías Utilizadas

### Backend
- **Framework:** ASP.NET Core 10 (C#)
- **Base de Datos:** PostgreSQL 15+
- **ORM:** Entity Framework Core 10 + Npgsql
- **Seguridad:** JWT (JSON Web Token) + BCrypt
- **Documentación:** Swagger / OpenAPI

### Frontend
- **Librería:** React 19
- **Empaquetador:** Vite
- **Enrutamiento:** React Router DOM v6
- **Peticiones HTTP:** Axios
- **Estilos:** CSS Puro (Diseño responsivo sin dependencias externas)

---

## 🏗 Arquitectura del Proyecto

El proyecto está dividido en dos grandes bloques: el **Backend** (Modelo C4) y el **Frontend** (React).

```text
FundacionJaguares/
├── backend/                             # API ASP.NET Core (Modelo C4, 4 capas)
│   ├── FundacionJaguares.slnx           # Solución .NET
│   └── src/
│       ├── FundacionJaguares.Api            # Controladores y Swagger
│       ├── FundacionJaguares.Application    # DTOs y Lógica de Negocio (Services)
│       ├── FundacionJaguares.Infrastructure # DbContext, Repositorios y Adaptadores
│       └── FundacionJaguares.Domain         # Entidades puras
├── frontend/                            # Aplicación React 19
│   └── src/
│       ├── pages                        # Vistas (Públicas, Admin, Voluntario)
│       ├── components                   # Navbar, ProtectedRoute
│       ├── context                      # Autenticación (AuthContext)
│       └── services                     # Conexión API (Axios)
├── db/                                  # Scripts SQL (schema + seed)
└── docs/                                # Documentos del proyecto (Minuta, SRS, C4)
```

---

## 🚀 Guía de Instalación y Ejecución

Sigue estos pasos para levantar el proyecto en tu máquina local.

### 1. Requisitos Previos
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Node.js 20+](https://nodejs.org/) (para el frontend)

### 2. Configurar la Base de Datos
Abre una terminal y ejecuta:
```powershell
# Crear la base de datos
psql -U postgres -c "CREATE DATABASE fundacion_jaguares;"

# Ejecutar el script de tablas (está en la carpeta db/)
psql -U postgres -d fundacion_jaguares -f db/01_schema.sql

# Cargar los usuarios de prueba (ver tabla de credenciales más abajo)
psql -U postgres -d fundacion_jaguares -f db/02_seed_usuarios.sql
```

> Si `psql` no se reconoce en Windows, usa la ruta completa: `& "C:\Program Files\PostgreSQL\16\bin\psql.exe" ...` (ajusta la versión), o ejecuta los scripts desde el Query Tool de pgAdmin.

*(Nota: Si el script falla por un error de BOM, ejecuta manualmente `CREATE EXTENSION IF NOT EXISTS "uuid-ossp";` y luego vuelve a correr el script).*

### 3. Configurar el Backend
1. Abre `backend/src/FundacionJaguares.Api/appsettings.json`
2. Cambia la contraseña de la base de datos por la tuya:
```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=fundacion_jaguares;Username=postgres;Password=TU_CONTRASEÑA"
}
```

### 4. Ejecutar el Backend (Terminal 1)
```powershell
cd backend
dotnet restore
dotnet run --project src/FundacionJaguares.Api
```
*La API correrá en: `http://localhost:5055`*
*Swagger estará disponible en: `http://localhost:5055/swagger`*

### 5. Ejecutar el Frontend (Terminal 2)
Abre una **nueva** terminal y ejecuta:
```powershell
cd frontend
npm install
npm run dev
```
*La página web se abrirá en: `http://localhost:5173`*

---

## 🔐 Roles y Credenciales de Prueba

El sistema cuenta con 3 portales diferentes según el rol del usuario. Estos usuarios se crean con el script `db/02_seed_usuarios.sql` (se puede volver a ejecutar sin problema: restablece las contraseñas):

| Rol | Nombre | Email | Password | Inicio tras login |
|-----|--------|-------|----------|-------------------|
| **DIRECTORA** | María Directora | `directora@jaguares.org` | `Admin123!` | `/admin` |
| **DOCENTE** | Carlos Docente | `docente@jaguares.org` | `Docente123!` | `/admin` |
| **ENCARGADO_EVENTOS** | Elena Encargada | `eventos@jaguares.org` | `Eventos123!` | `/admin` |
| **VOLUNTARIO** | Valeria Voluntaria | `voluntario@example.com` | `Vol123!` | `/voluntario` |
| **PARTICIPANTE** | Juan Pérez | `juan@example.com` | `Juan123!` | `/mi-cuenta` |

### Matriz de permisos

| Funcionalidad | Visitante | Participante | Voluntario | Docente | Encargado eventos | Directora |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Ver cursos, noticias, verificar certificado, donar en línea | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Inscribirse / cancelar inscripción a cursos | — | ✅ | ✅ | — | — | — |
| Mis cursos y mis certificados (`/mi-cuenta`) | — | ✅ | ✅ | — | — | — |
| Portal de voluntario: historial de participaciones | — | — | ✅ | — | — | — |
| Crear cursos | — | — | — | ✅ | — | ✅ |
| Ver inscritos y finalizar curso (emite certificados) | — | — | — | Solo sus cursos | — | Todos |
| Crear eventos | — | — | — | — | ✅ | ✅ |
| Registro de voluntarios y sus actividades | — | — | — | — | ✅ | ✅ |
| Registrar donaciones en efectivo | — | — | — | — | ✅ (ve solo lo que registró) | ✅ |
| Reporte financiero mensual (RF-06) | — | — | — | — | — | ✅ |
| Publicar noticias | — | — | — | — | — | ✅ |

Los permisos de la interfaz están centralizados en `frontend/src/config/permisos.js` (navbar, rutas y panel se arman desde ahí) y deben coincidir con los `[Authorize(Roles = ...)]` de los controladores, que son los que realmente protegen los datos. Un usuario con `activo = false` no puede iniciar sesión.

*(También puedes registrar usuarios nuevos desde la web o Swagger; quedan como `PARTICIPANTE` y el rol se cambia con SQL: `UPDATE usuarios SET rol='DIRECTORA' WHERE email='...';`)*

---

## 🌐 Módulos del Frontend (Vistas)

### Sitio Público (Landing Page)
- **Inicio:** Información de la fundación, estadísticas y noticias.
- **Cursos:** Listado de talleres con control de cupos en tiempo real.
- **Verificar Certificado:** Búsqueda pública por código para validar autenticidad.
- **Donaciones:** Pasarela para donar en línea (QR, transferencia o tarjeta).
- **Voluntariado:** Formulario público para postularse como voluntario.

### Mi Cuenta (`/mi-cuenta`) — Participante y Voluntario
- **Mis inscripciones:** Cursos inscritos con estado; se puede cancelar mientras el curso esté publicado.
- **Mis certificados:** Certificados emitidos con su código de verificación.

### Portal de Administración (`/admin`) — cada rol ve solo sus secciones
- **Panel:** Tarjetas de las secciones permitidas para el rol.
- **Mis cursos:** Cursos con inscritos/cupo y botón "Finalizar y certificar" (docente: solo los suyos).
- **Eventos:** Listado y creación de eventos.
- **Voluntarios:** Registro único con historial de actividades.
- **Donaciones:** La directora ve el reporte mensual con selector de mes; el encargado registra efectivo y ve sus registros.
- **Noticias:** Editor para publicar novedades en el sitio público.

### Portal del Voluntario (`/voluntario`)
- **Dashboard:** Historial de participaciones, datos del registro y acceso a sus cursos.
- **Mis Certificados:** Certificados obtenidos en cursos.

---

## 🔌 Documentación de la API (Endpoints)

Todos los endpoints están documentados en Swagger. Aquí un resumen:

| Módulo | Endpoints Principales |
|--------|-----------------------|
| **Auth** | `POST /registro`, `POST /login` |
| **Cursos** | `GET /`, `POST /`, `GET /gestion`, `GET /mis-inscripciones`, `POST /{id}/inscribir`, `DELETE /{id}/inscripcion`, `POST /{id}/finalizar` |
| **Eventos** | `GET /`, `POST /`, `POST /{id}/asistencia` |
| **Certificados** | `GET /verificar/{codigo}` (Público), `GET /mios` |
| **Donaciones** | `POST /`, `POST /en-linea`, `GET /mis-registros`, `GET /reporte/{anio}/{mes}` (solo Directora) |
| **Voluntarios** | `POST /postular`, `GET /` (con historial), `GET /mi-historial`, `POST /{id}/participaciones` |
| **Contenido** | `GET /noticias`, `POST /noticias`, `GET /galeria` |

---

## 📊 Trazabilidad SRS (Historias de Usuario)

| Key | Título | RF | Puntos | Estado |
|-----|--------|----|--------|--------|
| **JAG-15** | Inscripción a curso con control de cupos | RF-01 | 5 | ✅ Done |
| **JAG-16** | Registro de asistencia a evento | RF-02 | 5 | ✅ Done |
| **JAG-17** | Emisión automática de certificado | RF-03 | 8 | ✅ Done |
| **JAG-18** | Verificación pública de certificado | RF-04 | 3 | ✅ Done |
| **JAG-19** | Registro de donación | RF-05 | 5 | ✅ Done |
| **JAG-20** | Reporte financiero mensual | RF-06 | 5 | ✅ Done |
| **JAG-21** | Registro único de voluntarios | RF-07 | 3 | ✅ Done |
| **JAG-22** | Publicación de noticias institucionales | RF-08 | 3 | ✅ Done |
| **JAG-23** | Donación en línea (QR/pasarela) | RF-09 | 8 | ✅ Done |
| **JAG-24** | Notificación automática de confirmación | RF-10 | 3 | ✅ Done |

**Total:** 48 Story Points.

---

## 👩‍💻 Autor

**Natalia** — Proyecto de Ingeniería de Software  
Fundación Unión de los Jaguares — La Paz, Bolivia  
Septiembre 2026

**Repositorio:** [https://github.com/Ro-Nel/Union-de-los-Jaguares](https://github.com/Ro-Nel/Union-de-los-Jaguares)

---

## 📚 Documentos del Proyecto
Disponibles en la carpeta [`docs/`](docs/):
- **Doc 1:** Minuta de Entrevista y Relevamiento de Requerimientos.
- **Doc 2:** Especificación de Requisitos de Software (SRS - ISO/IEC/IEEE 29148).
- **Doc 3:** Arquitectura de Software (Modelo C4).