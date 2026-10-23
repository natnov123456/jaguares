import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { cursosApi } from '../services/api';
import MisCertificados from '../components/MisCertificados';
import Estado from '../components/Estado';
import { fecha } from '../utils/formato';

export default function MiCuenta() {
  const { usuario } = useAuth();
  const [inscripciones, setInscripciones] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);

  const cargar = () =>
    cursosApi.misInscripciones()
      .then(r => setInscripciones(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar tus inscripciones' }))
      .finally(() => setLoading(false));

  useEffect(() => { cargar(); }, []);

  const cancelar = async (i) => {
    if (!window.confirm(`¿Cancelar tu inscripción a "${i.titulo}"? Tu cupo quedará libre para otra persona.`)) return;
    try {
      await cursosApi.cancelarInscripcion(i.cursoId);
      setMsg({ tipo: 'success', texto: 'Inscripción cancelada' });
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo cancelar la inscripción' });
    }
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Mis cursos</h1>
          <p>Hola, {usuario?.nombreCompleto}. Aquí ves tus inscripciones y certificados.</p>
        </div>
        <Link to="/cursos" className="btn-primary">Buscar cursos</Link>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <section className="panel">
        <h2>Mis inscripciones</h2>
        {loading ? (
          <p>Cargando...</p>
        ) : inscripciones.length === 0 ? (
          <p className="muted">Todavía no te inscribiste a ningún curso. <Link to="/cursos" style={{ color: '#b45309' }}>Ver cursos disponibles →</Link></p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>Curso</th><th>Fechas</th><th>Estado del curso</th><th>Inscripción</th><th /></tr>
              </thead>
              <tbody>
                {inscripciones.map(i => (
                  <tr key={i.cursoId}>
                    <td style={{ fontWeight: 600 }}>{i.titulo}</td>
                    <td>{fecha(i.fechaInicio)} – {fecha(i.fechaFin)}</td>
                    <td><Estado valor={i.estadoCurso} /></td>
                    <td><Estado valor={i.estadoInscripcion} /></td>
                    <td>
                      {i.estadoCurso === 'PUBLICADO' && i.estadoInscripcion === 'CONFIRMADA' && (
                        <button className="btn-secondary" onClick={() => cancelar(i)}>Cancelar</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="panel">
        <h2>Mis certificados</h2>
        <MisCertificados />
      </section>
    </div>
  );
}
