import axios from 'axios';

const API_URL = 'http://localhost:5055/api';

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
  finalizar: (id) => api.post(`/Cursos/${id}/finalizar`),
};

export const certificadosApi = {
  verificar: (codigo) => api.get(`/Certificados/verificar/${codigo}`),
};

export const donacionesApi = {
  registrar: (data) => api.post('/Donaciones', data),
  enLinea: (data) => api.post('/Donaciones/en-linea', data),
  reporte: (anio, mes) => api.get(`/Donaciones/reporte/${anio}/${mes}`),
};

export const voluntariosApi = {
  postular: (data) => api.post('/Voluntarios/postular', data),
  listar: () => api.get('/Voluntarios'),
};

export const contenidoApi = {
  noticias: () => api.get('/Contenido/noticias'),
  crearNoticia: (data) => api.post('/Contenido/noticias', data),
  galeria: () => api.get('/Contenido/galeria'),
};

export default api;