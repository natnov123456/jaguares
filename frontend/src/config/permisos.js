// Fuente única de verdad de qué puede hacer cada rol en la interfaz.
// Debe coincidir con los [Authorize(Roles = ...)] del backend: ocultar algo aquí
// es solo comodidad visual, la seguridad real la aplica la API.

export const ROLES = {
  PARTICIPANTE: 'PARTICIPANTE',
  VOLUNTARIO: 'VOLUNTARIO',
  DOCENTE: 'DOCENTE',
  ENCARGADO_EVENTOS: 'ENCARGADO_EVENTOS',
  DIRECTORA: 'DIRECTORA',
  // FUJ-122: roles del personal que asigna la Directora
  CONTADOR: 'CONTADOR',
  COORDINADOR_VOLUNTARIADO: 'COORDINADOR_VOLUNTARIADO',
};

export const ETIQUETA_ROL = {
  PARTICIPANTE: 'Participante',
  VOLUNTARIO: 'Voluntario/a',
  DOCENTE: 'Docente',
  ENCARGADO_EVENTOS: 'Encargado/a de operaciones',
  DIRECTORA: 'Dirección',
  CONTADOR: 'Contador/a',
  COORDINADOR_VOLUNTARIADO: 'Coordinador/a de voluntariado',
};

export const ROLES_STAFF = [ROLES.DIRECTORA, ROLES.DOCENTE, ROLES.ENCARGADO_EVENTOS, ROLES.CONTADOR, ROLES.COORDINADOR_VOLUNTARIADO];

const { PARTICIPANTE, VOLUNTARIO, DOCENTE, ENCARGADO_EVENTOS, DIRECTORA, CONTADOR, COORDINADOR_VOLUNTARIADO } = ROLES;

export const PERMISOS = {
  // Comunidad
  inscribirseCursos: [PARTICIPANTE, VOLUNTARIO],
  inscribirseEventos: [PARTICIPANTE, VOLUNTARIO],
  verMiCuenta: [PARTICIPANTE, VOLUNTARIO],
  verPortalVoluntario: [VOLUNTARIO],

  // Staff
  verPanel: ROLES_STAFF,
  gestionarCursos: [DOCENTE, DIRECTORA],
  // Solo la directora crea cursos y asigna al docente; el docente gestiona los que le asignan
  crearCursos: [DIRECTORA],
  gestionarEventos: [ENCARGADO_EVENTOS, DIRECTORA],
  registrarCheckin: [ENCARGADO_EVENTOS, DIRECTORA],
  gestionarVoluntarios: [ENCARGADO_EVENTOS, COORDINADOR_VOLUNTARIADO, DIRECTORA],
  registrarDonaciones: [ENCARGADO_EVENTOS, CONTADOR, DIRECTORA],
  verReporteFinanciero: [CONTADOR, DIRECTORA],
  // FUJ-125: corregir/anular donaciones y consultar la bitácora de auditoría
  corregirDonaciones: [CONTADOR, DIRECTORA],
  verBitacora: [CONTADOR, DIRECTORA],
  publicarNoticias: [DIRECTORA],
  publicarTransparencia: [CONTADOR, DIRECTORA],
  gestionarGaleria: [DIRECTORA],
  gestionarPlantillas: [DIRECTORA],
  gestionarCampanas: [CONTADOR, DIRECTORA],
  // FUJ-120: el backend filtra: docente (sus cursos), encargado (eventos), directora (todas)
  verEncuestas: [DOCENTE, ENCARGADO_EVENTOS, DIRECTORA],
  editarInstitucional: [DIRECTORA],
  // FUJ-122: solo la Directora administra cuentas y roles
  gestionarUsuarios: [DIRECTORA],
  // FUJ-123: panel de indicadores; evaluación: registro de exportaciones
  verIndicadores: [DIRECTORA],
  verExportaciones: [DIRECTORA],
  verRegistroActividad: [DIRECTORA],
};

export function puede(usuario, permiso) {
  return !!usuario && PERMISOS[permiso].includes(usuario.rol);
}

export function esStaff(usuario) {
  return !!usuario && ROLES_STAFF.includes(usuario.rol);
}

export function rutaInicio(usuario) {
  if (!usuario) return '/';
  if (esStaff(usuario)) return '/admin';
  if (usuario.rol === VOLUNTARIO) return '/voluntario';
  return '/mi-cuenta';
}

// Grupos del menú superior del personal: cada sección del panel pertenece a uno. Si un rol solo ve una
// sección de un grupo, el navbar la muestra como enlace directo en lugar de un menú desplegable.
export const GRUPOS_PANEL = [
  { id: 'cursos', titulo: 'Cursos' },
  { id: 'eventos', titulo: 'Eventos y voluntariado' },
  { id: 'finanzas', titulo: 'Finanzas' },
  { id: 'sitio', titulo: 'Sitio web' },
  { id: 'gestion', titulo: 'Gestión' },
];

// Secciones del panel de administración. El navbar y el dashboard se arman desde aquí.
export const SECCIONES_PANEL = [
  { permiso: 'verIndicadores', ruta: '/admin/indicadores', grupo: 'gestion', titulo: 'Panel de indicadores', icono: '📈',
    descripcion: 'Inscritos, asistencia, certificados, donaciones, voluntarios y satisfacción del mes.' },
  { permiso: 'gestionarUsuarios', ruta: '/admin/usuarios', grupo: 'gestion', titulo: 'Usuarios y roles', icono: '👥',
    descripcion: 'Crear cuentas del personal, asignar su rol y activarlas o desactivarlas.' },
  { permiso: 'gestionarCursos', ruta: '/admin/cursos', grupo: 'cursos', titulo: 'Mis cursos', icono: '📚',
    descripcion: 'Ver inscritos, registrar la asistencia de cada clase y emitir certificados al finalizar.' },
  { permiso: 'gestionarPlantillas', ruta: '/admin/plantillas-certificado', grupo: 'cursos', titulo: 'Plantillas de certificado', icono: '🎓',
    descripcion: 'Configurar logo, firmas y texto de los certificados por tipo de actividad.' },
  { permiso: 'gestionarEventos', ruta: '/admin/eventos', grupo: 'eventos', titulo: 'Eventos', icono: '📅',
    descripcion: 'Programar eventos y actividades de la fundación.' },
  { permiso: 'registrarCheckin', ruta: '/admin/checkin', grupo: 'eventos', titulo: 'Check-in QR', icono: '📷',
    descripcion: 'Registrar la asistencia escaneando el QR del participante desde el celular.' },
  { permiso: 'verEncuestas', ruta: '/admin/encuestas', grupo: 'gestion', titulo: 'Encuestas', icono: '⭐',
    descripcion: 'Promedio de satisfacción y comentarios de las encuestas enviadas al terminar cada actividad.' },
  { permiso: 'gestionarVoluntarios', ruta: '/admin/voluntarios', grupo: 'eventos', titulo: 'Voluntarios', icono: '🤝',
    descripcion: 'Registro único de voluntarios con su historial de actividades.' },
  { permiso: 'gestionarVoluntarios', ruta: '/admin/voluntarios/asignaciones', grupo: 'eventos', titulo: 'Asignaciones', icono: '🗓️',
    descripcion: 'Asignar voluntarios a eventos y cursos, y registrar sus horas al terminar.' },
  { permiso: 'registrarDonaciones', ruta: '/admin/donaciones', grupo: 'finanzas', titulo: 'Donaciones', icono: '💵',
    descripcion: 'Registrar aportes en efectivo recibidos en oficina o eventos.' },
  { permiso: 'verBitacora', ruta: '/admin/bitacora', grupo: 'finanzas', titulo: 'Bitácora de auditoría', icono: '🧾',
    descripcion: 'Cada alta, corrección o anulación de donaciones con su usuario, hora y valores anterior y nuevo.' },
  { permiso: 'gestionarCampanas', ruta: '/admin/campanas', grupo: 'finanzas', titulo: 'Campañas', icono: '🎯',
    descripcion: 'Crear campañas de donación con meta, fecha límite y barra de avance.' },
  { permiso: 'publicarNoticias', ruta: '/admin/publicar-noticia', grupo: 'sitio', titulo: 'Noticias', icono: '📝',
    descripcion: 'Publicar novedades en el sitio público.' },
  { permiso: 'gestionarGaleria', ruta: '/admin/galeria', grupo: 'sitio', titulo: 'Galería', icono: '🖼️',
    descripcion: 'Crear álbumes con fotos y videos de las actividades.' },
  { permiso: 'editarInstitucional', ruta: '/admin/institucional', grupo: 'sitio', titulo: 'Quiénes somos', icono: '🏛️',
    descripcion: 'Editar misión, visión, historia, equipo y datos de contacto.' },
  { permiso: 'verExportaciones', ruta: '/admin/exportaciones', grupo: 'gestion', titulo: 'Registro de exportaciones', icono: '🗂️',
    descripcion: 'Quién generó cada reporte PDF o Excel, cuándo, con qué filtros y si hubo errores.' },
  { permiso: 'verRegistroActividad', ruta: '/admin/actividad', grupo: 'gestion', titulo: 'Registro de actividad', icono: '🕵️',
    descripcion: 'Quién entró, a qué hora, desde dónde y qué hizo en la plataforma.' },
  { permiso: 'publicarTransparencia', ruta: '/admin/transparencia', grupo: 'finanzas', titulo: 'Transparencia', icono: '📊',
    descripcion: 'Publicar en el sitio en qué se usaron las donaciones de cada mes.' },
];
