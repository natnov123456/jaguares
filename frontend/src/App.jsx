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

// Participante / Voluntario
import MiCuenta from './pages/MiCuenta';

// Páginas Admin
import Dashboard from './pages/admin/Dashboard';
import AdminCursos from './pages/admin/Cursos';
import CrearCurso from './pages/admin/CrearCurso';
import AdminEventos from './pages/admin/Eventos';
import PublicarNoticia from './pages/admin/PublicarNoticia';
import AdminDonaciones from './pages/admin/Donaciones';
import AdminVoluntarios from './pages/admin/Voluntarios';

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

          {/* Participante y Voluntario */}
          <Route path="/mi-cuenta" element={protegida('verMiCuenta', <MiCuenta />)} />

          {/* Staff — cada ruta exige el permiso de su sección (ver config/permisos.js) */}
          <Route path="/admin" element={protegida('verPanel', <Dashboard />)} />
          <Route path="/admin/cursos" element={protegida('gestionarCursos', <AdminCursos />)} />
          <Route path="/admin/crear-curso" element={protegida('gestionarCursos', <CrearCurso />)} />
          <Route path="/admin/eventos" element={protegida('gestionarEventos', <AdminEventos />)} />
          <Route path="/admin/publicar-noticia" element={protegida('publicarNoticias', <PublicarNoticia />)} />
          <Route path="/admin/donaciones" element={protegida('registrarDonaciones', <AdminDonaciones />)} />
          <Route path="/admin/voluntarios" element={protegida('gestionarVoluntarios', <AdminVoluntarios />)} />

          {/* Voluntario */}
          <Route path="/voluntario" element={protegida('verPortalVoluntario', <VoluntarioDashboard />)} />
          <Route path="/voluntario/certificados" element={protegida('verPortalVoluntario', <VoluntarioCertificados />)} />

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
