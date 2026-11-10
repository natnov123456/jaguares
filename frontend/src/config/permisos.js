// Fuente única de verdad de qué puede hacer cada rol en la interfaz.
// Debe coincidir con los [Authorize(Roles = ...)] del backend: ocultar algo aquí
// es solo comodidad visual, la seguridad real la aplica la API.

export const ROLES = {
  PARTICIPANTE: 'PARTICIPANTE',
  VOLUNTARIO: 'VOLUNTARIO',
  DOCENTE: 'DOCENTE',
  ENCARGADO_EVENTOS: 'ENCARGADO_EVENTOS',
  DIRECTORA: 'DIRECTORA',
};

export const ETIQUETA_ROL = {
  PARTICIPANTE: 'Participante',
  VOLUNTARIO: 'Voluntario/a',
  DOCENTE: 'Docente',
  ENCARGADO_EVENTOS: 'Encargado/a de eventos',
  DIRECTORA: 'Dirección',
};

export const ROLES_STAFF = [ROLES.DIRECTORA, ROLES.DOCENTE, ROLES.ENCARGADO_EVENTOS];

const { PARTICIPANTE, VOLUNTARIO, DOCENTE, ENCARGADO_EVENTOS, DIRECTORA } = ROLES;

export const PERMISOS = {
  // Comunidad
  inscribirseCursos: [PARTICIPANTE, VOLUNTARIO],
  inscribirseEventos: [PARTICIPANTE, VOLUNTARIO],
  verMiCuenta: [PARTICIPANTE, VOLUNTARIO],
  verPortalVoluntario: [VOLUNTARIO],

  // Staff
  verPanel: ROLES_STAFF,
  gestionarCursos: [DOCENTE, DIRECTORA],
  gestionarEventos: [ENCARGADO_EVENTOS, DIRECTORA],
  registrarCheckin: [ENCARGADO_EVENTOS, DIRECTORA],
  gestionarVoluntarios: [ENCARGADO_EVENTOS, DIRECTORA],
  registrarDonaciones: [ENCARGADO_EVENTOS, DIRECTORA],
  verReporteFinanciero: [DIRECTORA],
  publicarNoticias: [DIRECTORA],
  publicarTransparencia: [DIRECTORA],
  gestionarGaleria: [DIRECTORA],
  gestionarPlantillas: [DIRECTORA],
  gestionarCampanas: [DIRECTORA],
  // FUJ-120: el backend filtra: docente (sus cursos), encargado (eventos), directora (todas)
  verEncuestas: [DOCENTE, ENCARGADO_EVENTOS, DIRECTORA],
  editarInstitucional: [DIRECTORA],
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

// Secciones del panel de administración. El navbar y el dashboard se arman desde aquí.
export const SECCIONES_PANEL = [
  { permiso: 'gestionarCursos', ruta: '/admin/cursos', titulo: 'Mis cursos', icono: '📚',
    descripcion: 'Crear talleres, ver inscritos y emitir certificados al finalizar.' },
  { permiso: 'gestionarPlantillas', ruta: '/admin/plantillas-certificado', titulo: 'Plantillas de certificado', icono: '🎓',
    descripcion: 'Configurar logo, firmas y texto de los certificados por tipo de actividad.' },
  { permiso: 'gestionarEventos', ruta: '/admin/eventos', titulo: 'Eventos', icono: '📅',
    descripcion: 'Programar eventos y actividades de la fundación.' },
  { permiso: 'registrarCheckin', ruta: '/admin/checkin', titulo: 'Check-in QR', icono: '📷',
    descripcion: 'Registrar la asistencia escaneando el QR del participante desde el celular.' },
  { permiso: 'verEncuestas', ruta: '/admin/encuestas', titulo: 'Encuestas', icono: '⭐',
    descripcion: 'Promedio de satisfacción y comentarios de las encuestas enviadas al terminar cada actividad.' },
  { permiso: 'gestionarVoluntarios', ruta: '/admin/voluntarios', titulo: 'Voluntarios', icono: '🤝',
    descripcion: 'Registro único de voluntarios con su historial de actividades.' },
  { permiso: 'gestionarVoluntarios', ruta: '/admin/voluntarios/asignaciones', titulo: 'Asignaciones', icono: '🗓️',
    descripcion: 'Asignar voluntarios a eventos y cursos, y registrar sus horas al terminar.' },
  { permiso: 'registrarDonaciones', ruta: '/admin/donaciones', titulo: 'Donaciones', icono: '💵',
    descripcion: 'Registrar aportes en efectivo recibidos en oficina o eventos.' },
  { permiso: 'gestionarCampanas', ruta: '/admin/campanas', titulo: 'Campañas', icono: '🎯',
    descripcion: 'Crear campañas de donación con meta, fecha límite y barra de avance.' },
  { permiso: 'publicarNoticias', ruta: '/admin/publicar-noticia', titulo: 'Noticias', icono: '📝',
    descripcion: 'Publicar novedades en el sitio público.' },
  { permiso: 'gestionarGaleria', ruta: '/admin/galeria', titulo: 'Galería', icono: '🖼️',
    descripcion: 'Crear álbumes con fotos y videos de las actividades.' },
  { permiso: 'editarInstitucional', ruta: '/admin/institucional', titulo: 'Quiénes somos', icono: '🏛️',
    descripcion: 'Editar misión, visión, historia, equipo y datos de contacto.' },
  { permiso: 'publicarTransparencia', ruta: '/admin/transparencia', titulo: 'Transparencia', icono: '📊',
    descripcion: 'Publicar en el sitio en qué se usaron las donaciones de cada mes.' },
];
