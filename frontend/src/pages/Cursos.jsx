import { useEffect, useState } from 'react';
import { cursosApi } from '../services/api';
import { useAuth } from '../context/AuthContext';

export default function Cursos() {
  const [cursos, setCursos] = useState([]);
  const [msg, setMsg] = useState('');
  const { usuario } = useAuth();

  useEffect(() => {
    cursosApi.listar().then(r => setCursos(r.data)).catch(console.error);
  }, []);

  const inscribir = async (id) => {
    if (!usuario) return alert('Debes iniciar sesión');
    try {
      await cursosApi.inscribir(id);
      setMsg('Inscripción exitosa ✅');
      cursosApi.listar().then(r => setCursos(r.data));
    } catch (err) {
      setMsg(err.response?.data?.error || 'Error al inscribirse');
    }
  };

  return (
    <div className="container" style={{ padding: '32px 24px' }}>
      <h1>Cursos y talleres</h1>
      {msg && <div className="success-msg">{msg}</div>}
      <div className="grid">
        {cursos.map(c => (
          <div key={c.id} className="card">
            <div className="card-body">
              <h3>{c.titulo}</h3>
              <p>{c.descripcion}</p>
              <p><strong>Cupos:</strong> {c.cupoDisponible} / {c.cupoMaximo}</p>
              <button onClick={() => inscribir(c.id)} disabled={c.cupoDisponible === 0} className="btn-primary">
                {c.cupoDisponible === 0 ? 'Sin cupos' : 'Inscribirme'}
              </button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}