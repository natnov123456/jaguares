import { BrowserRouter, Routes, Route } from 'react-router-dom';
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

// Páginas Admin
import Dashboard from './pages/admin/Dashboard';
import CrearCurso from './pages/admin/CrearCurso';
import PublicarNoticia from './pages/admin/PublicarNoticia';
import AdminDonaciones from './pages/admin/Donaciones'; // <-- NUEVA IMPORTACIÓN

// Páginas Voluntario
import VoluntarioDashboard from './pages/voluntario/Dashboard';
import VoluntarioCertificados from './pages/voluntario/Certificados';

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

          {/* Rutas Protegidas - ADMIN */}
          <Route 
            path="/admin" 
            element={<ProtectedRoute roles={['DIRECTORA', 'DOCENTE', 'ENCARGADO_EVENTOS']}><Dashboard /></ProtectedRoute>} 
          />
          <Route 
            path="/admin/crear-curso" 
            element={<ProtectedRoute roles={['DIRECTORA', 'DOCENTE']}><CrearCurso /></ProtectedRoute>} 
          />
          <Route 
            path="/admin/publicar-noticia" 
            element={<ProtectedRoute roles={['DIRECTORA']}><PublicarNoticia /></ProtectedRoute>} 
          />
          {/* NUEVA RUTA DE DONACIONES */}
          <Route 
            path="/admin/donaciones" 
            element={<ProtectedRoute roles={['DIRECTORA', 'ENCARGADO_EVENTOS']}><AdminDonaciones /></ProtectedRoute>} 
          />

          {/* Rutas Protegidas - VOLUNTARIO */}
          <Route 
            path="/voluntario" 
            element={<ProtectedRoute roles={['VOLUNTARIO']}><VoluntarioDashboard /></ProtectedRoute>} 
          />
          <Route 
            path="/voluntario/certificados" 
            element={<ProtectedRoute roles={['VOLUNTARIO']}><VoluntarioCertificados /></ProtectedRoute>} 
          />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}