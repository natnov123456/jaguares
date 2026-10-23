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
  verMiCuenta: [PARTICIPANTE, VOLUNTARIO],
  verPortalVoluntario: [VOLUNTARIO],

  // Staff
  verPanel: ROLES_STAFF,
  gestionarCursos: [DOCENTE, DIRECTORA],
  gestionarEventos: [ENCARGADO_EVENTOS, DIRECTORA],
  gestionarVoluntarios: [ENCARGADO_EVENTOS, DIRECTORA],
  registrarDonaciones: [ENCARGADO_EVENTOS, DIRECTORA],
  verReporteFinanciero: [DIRECTORA],
  publicarNoticias: [DIRECTORA],
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
  { permiso: 'gestionarEventos', ruta: '/admin/eventos', titulo: 'Eventos', icono: '📅',
    descripcion: 'Programar eventos y actividades de la fundación.' },
  { permiso: 'gestionarVoluntarios', ruta: '/admin/voluntarios', titulo: 'Voluntarios', icono: '🤝',
    descripcion: 'Registro único de voluntarios con su historial de actividades.' },
  { permiso: 'registrarDonaciones', ruta: '/admin/donaciones', titulo: 'Donaciones', icono: '💵',
    descripcion: 'Registrar aportes en efectivo recibidos en oficina o eventos.' },
  { permiso: 'publicarNoticias', ruta: '/admin/publicar-noticia', titulo: 'Noticias', icono: '📝',
    descripcion: 'Publicar novedades en el sitio público.' },
];
