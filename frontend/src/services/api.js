import axios from 'axios';

const SERVIDOR = 'http://localhost:5055';
const API_URL = `${SERVIDOR}/api`;

// Las imágenes subidas se sirven desde la API ("/uploads/..."); las URLs externas se dejan igual
export const archivoUrl = (url) => (url?.startsWith('/') ? `${SERVIDOR}${url}` : url);

const api = axios.create({
  baseURL: API_URL,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && window.location.pathname !== '/login') {
      localStorage.removeItem('token');
      localStorage.removeItem('usuario');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export const authApi = {
  login: (email, password) => api.post('/Auth/login', { email, password }),
  registro: (data) => api.post('/Auth/registro', data),
};

export const cursosApi = {
  listar: () => api.get('/Cursos'),
  crear: (data) => api.post('/Cursos', data),
  inscribir: (id) => api.post(`/Cursos/${id}/inscribir`),
  cancelarInscripcion: (id) => api.delete(`/Cursos/${id}/inscripcion`),
  misInscripciones: () => api.get('/Cursos/mis-inscripciones'),
  gestion: () => api.get('/Cursos/gestion'),
  finalizar: (id) => api.post(`/Cursos/${id}/finalizar`),
  asignarPlantilla: (id, plantillaId) => api.put(`/Cursos/${id}/plantilla`, { plantillaId }),
};

export const eventosApi = {
  listar: () => api.get('/Eventos'),
  crear: (data) => api.post('/Eventos', data),
  inscribir: (id) => api.post(`/Eventos/${id}/inscribir`),
  misInscripciones: () => api.get('/Eventos/mis-inscripciones'),
  qr: (inscripcionId) => api.get(`/Eventos/mis-inscripciones/${inscripcionId}/qr`, { responseType: 'blob' }),
  checkin: (id, codigo) => api.post(`/Eventos/${id}/checkin`, { codigo }),
  inscritos: (id) => api.get(`/Eventos/${id}/inscritos`),
};

export const actividadesApi = {
  rango: (desde, hasta, signal) => api.get('/Actividades', { params: { desde, hasta }, signal }),
};

export const certificadosApi = {
  verificar: (codigo) => api.get(`/Certificados/verificar/${codigo}`),
  mios: () => api.get('/Certificados/mios'),
};

export const plantillasApi = {
  listar: () => api.get('/Certificados/plantillas'),
  crear: (data) => api.post('/Certificados/plantillas', data),
  actualizar: (id, data) => api.put(`/Certificados/plantillas/${id}`, data),
  eliminar: (id) => api.delete(`/Certificados/plantillas/${id}`),
  subirImagen: (id, campo, archivo) => {
    const form = new FormData();
    form.append('archivo', archivo);
    return api.post(`/Certificados/plantillas/${id}/imagenes/${campo}`, form, { headers: { 'Content-Type': 'multipart/form-data' } });
  },
  quitarImagen: (id, campo) => api.delete(`/Certificados/plantillas/${id}/imagenes/${campo}`),
  vistaPrevia: (id) => api.get(`/Certificados/plantillas/${id}/vista-previa`, { responseType: 'blob' }),
};

export const donacionesApi = {
  registrar: (data) => api.post('/Donaciones', data),
  enLinea: (data) => api.post('/Donaciones/en-linea', data),
  reporte: (anio, mes) => api.get(`/Donaciones/reporte/${anio}/${mes}`),
  misRegistros: () => api.get('/Donaciones/mis-registros'),
};

export const transparenciaApi = {
  meses: () => api.get('/Transparencia'),
  resumen: (anio, mes) => api.get(`/Transparencia/${anio}/${mes}`),
  vistaPrevia: (anio, mes) => api.get(`/Transparencia/vista-previa/${anio}/${mes}`),
  publicar: (anio, mes, data) => api.put(`/Transparencia/${anio}/${mes}`, data),
  despublicar: (anio, mes) => api.delete(`/Transparencia/${anio}/${mes}`),
};

export const galeriaApi = {
  albumes: () => api.get('/Galeria/albumes'),
  album: (id) => api.get(`/Galeria/albumes/${id}`),
  albumesAdmin: () => api.get('/Galeria/admin/albumes'),
  crearAlbum: (data) => api.post('/Galeria/albumes', data),
  actualizarAlbum: (id, data) => api.put(`/Galeria/albumes/${id}`, data),
  eliminarAlbum: (id) => api.delete(`/Galeria/albumes/${id}`),
  subirImagen: (id, archivo) => {
    const form = new FormData();
    form.append('archivos', archivo);
    // Hay que pisar el 'application/json' por defecto: con él axios convertiría el FormData a JSON.
    // Con multipart/form-data el navegador agrega solo el "boundary".
    return api.post(`/Galeria/albumes/${id}/imagenes`, form, { headers: { 'Content-Type': 'multipart/form-data' } });
  },
  agregarVideo: (id, data) => api.post(`/Galeria/albumes/${id}/videos`, data),
  eliminarItem: (itemId) => api.delete(`/Galeria/items/${itemId}`),
};

export const institucionalApi = {
  obtener: () => api.get('/Institucional'),
  actualizar: (data) => api.put('/Institucional', data),
};

export const contactoApi = {
  enviar: (data) => api.post('/Contacto', data),
};

export const voluntariosApi = {
  postular: (data) => api.post('/Voluntarios/postular', data),
  listar: () => api.get('/Voluntarios'),
  miHistorial: () => api.get('/Voluntarios/mi-historial'),
  registrarParticipacion: (id, data) => api.post(`/Voluntarios/${id}/participaciones`, data),
};

export const contenidoApi = {
  noticias: () => api.get('/Contenido/noticias'),
  crearNoticia: (data) => api.post('/Contenido/noticias', data),
  galeria: () => api.get('/Contenido/galeria'),
};

export default api;