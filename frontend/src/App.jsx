import { lazy, Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import Navbar from './components/Navbar';
import ProtectedRoute from './components/ProtectedRoute';

// Páginas Públicas
import Home from './pages/Home';
import Login from './pages/Login';
import Registro from './pages/Registro';
import Cursos from './pages/Cursos';
import Certificados from './pages/Certificados';
import Donaciones from './pages/Donaciones';
import Voluntariado from './pages/Voluntariado';
import Noticias from './pages/Noticias';
import Calendario from './pages/Calendario';
import Transparencia from './pages/Transparencia';
import Galeria from './pages/Galeria';
import QuienesSomos from './pages/QuienesSomos';

// Participante / Voluntario
import MiCuenta from './pages/MiCuenta';

// Páginas Admin
import Dashboard from './pages/admin/Dashboard';
import AdminCursos from './pages/admin/Cursos';
import CrearCurso from './pages/admin/CrearCurso';
import AdminEventos from './pages/admin/Eventos';
// El lector QR (jsQR) solo lo usa el personal de eventos: se descarga aparte al abrir la página
const Checkin = lazy(() => import('./pages/admin/Checkin'));
import PublicarNoticia from './pages/admin/PublicarNoticia';
import AdminDonaciones from './pages/admin/Donaciones';
import AdminVoluntarios from './pages/admin/Voluntarios';
import AsignacionesVoluntarios from './pages/admin/AsignacionesVoluntarios';
import AdminTransparencia from './pages/admin/Transparencia';
import AdminGaleria from './pages/admin/Galeria';
import AdminInstitucional from './pages/admin/Institucional';
import PlantillasCertificado from './pages/admin/PlantillasCertificado';
import AdminCampanas from './pages/admin/Campanas';

// Páginas Voluntario
import VoluntarioDashboard from './pages/voluntario/Dashboard';
import VoluntarioCertificados from './pages/voluntario/Certificados';

const protegida = (permiso, elemento) => <ProtectedRoute permiso={permiso}>{elemento}</ProtectedRoute>;

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Navbar />
        <Routes>
          {/* Rutas Públicas */}
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<Login />} />
          <Route path="/registro" element={<Registro />} />
          <Route path="/cursos" element={<Cursos />} />
          <Route path="/certificados" element={<Certificados />} />
          <Route path="/donaciones" element={<Donaciones />} />
          <Route path="/voluntariado" element={<Voluntariado />} />
          <Route path="/noticias" element={<Noticias />} />
          <Route path="/calendario" element={<Calendario />} />
          <Route path="/transparencia" element={<Transparencia />} />
          <Route path="/galeria" element={<Galeria />} />
          <Route path="/galeria/:id" element={<Galeria />} />
          <Route path="/quienes-somos" element={<QuienesSomos />} />

          {/* Participante y Voluntario */}
          <Route path="/mi-cuenta" element={protegida('verMiCuenta', <MiCuenta />)} />

          {/* Staff — cada ruta exige el permiso de su sección (ver config/permisos.js) */}
          <Route path="/admin" element={protegida('verPanel', <Dashboard />)} />
          <Route path="/admin/cursos" element={protegida('gestionarCursos', <AdminCursos />)} />
          <Route path="/admin/crear-curso" element={protegida('gestionarCursos', <CrearCurso />)} />
          <Route path="/admin/plantillas-certificado" element={protegida('gestionarPlantillas', <PlantillasCertificado />)} />
          <Route path="/admin/eventos" element={protegida('gestionarEventos', <AdminEventos />)} />
          <Route path="/admin/checkin" element={protegida('registrarCheckin', <Suspense fallback={<p className="container page">Cargando...</p>}><Checkin /></Suspense>)} />
          <Route path="/admin/publicar-noticia" element={protegida('publicarNoticias', <PublicarNoticia />)} />
          <Route path="/admin/donaciones" element={protegida('registrarDonaciones', <AdminDonaciones />)} />
          <Route path="/admin/campanas" element={protegida('gestionarCampanas', <AdminCampanas />)} />
          <Route path="/admin/voluntarios" element={protegida('gestionarVoluntarios', <AdminVoluntarios />)} />
          <Route path="/admin/voluntarios/asignaciones" element={protegida('gestionarVoluntarios', <AsignacionesVoluntarios />)} />
          <Route path="/admin/transparencia" element={protegida('publicarTransparencia', <AdminTransparencia />)} />
          <Route path="/admin/galeria" element={protegida('gestionarGaleria', <AdminGaleria />)} />
          <Route path="/admin/institucional" element={protegida('editarInstitucional', <AdminInstitucional />)} />

          {/* Voluntario */}
          <Route path="/voluntario" element={protegida('verPortalVoluntario', <VoluntarioDashboard />)} />
          <Route path="/voluntario/certificados" element={protegida('verPortalVoluntario', <VoluntarioCertificados />)} />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
