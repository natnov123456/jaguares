import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { cursosApi } from '../../services/api';
import Estado from '../../components/Estado';
import { fecha } from '../../utils/formato';

export default function AdminCursos() {
  const { usuario } = useAuth();
  const [cursos, setCursos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);
  const [finalizando, setFinalizando] = useState(null);
  const esDirectora = usuario?.rol === 'DIRECTORA';

  const cargar = () =>
    cursosApi.gestion()
      .then(r => setCursos(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar los cursos' }))
      .finally(() => setLoading(false));

  useEffect(() => { cargar(); }, []);

  const finalizar = async (c) => {
    if (!window.confirm(
      `¿Finalizar "${c.titulo}"?\n\nSe emitirá un certificado para cada uno de los ${c.inscritos} inscritos y se les notificará por email. Esta acción no se puede deshacer.`
    )) return;
    setFinalizando(c.id);
    setMsg(null);
    try {
      const { data } = await cursosApi.finalizar(c.id);
      setMsg({ tipo: 'success', texto: `Curso finalizado: ${data.certificados_generados} certificado(s) emitido(s).` });
      cargar();
    } catch (err) {
      const texto = err.response?.status === 403
        ? 'Solo puedes finalizar los cursos que tú creaste.'
        : err.response?.data?.error || 'No se pudo finalizar el curso';
      setMsg({ tipo: 'error', texto });
    } finally {
      setFinalizando(null);
    }
  };

  const activos = cursos.filter(c => c.estado !== 'FINALIZADO' && c.estado !== 'CANCELADO');
  const totalInscritos = activos.reduce((n, c) => n + c.inscritos, 0);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>{esDirectora ? 'Cursos de la fundación' : 'Mis cursos'}</h1>
          <p>{esDirectora ? 'Todos los cursos, de todos los docentes.' : 'Solo ves los cursos que tú creaste.'}</p>
        </div>
        <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="stat-box">
            <p>Inscritos en cursos activos</p>
            <h2>{totalInscritos}</h2>
          </div>
          <Link to="/admin/crear-curso" className="btn-primary">+ Crear curso</Link>
        </div>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <section className="panel">
        {loading ? (
          <p>Cargando...</p>
        ) : cursos.length === 0 ? (
          <p className="muted" style={{ textAlign: 'center', padding: 24 }}>
            Aún no hay cursos. <Link to="/admin/crear-curso" style={{ color: '#b45309' }}>Crea el primero →</Link>
          </p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>Curso</th><th>Fechas</th><th>Inscritos / Cupo</th><th>Estado</th><th>Acciones</th></tr>
              </thead>
              <tbody>
                {cursos.map(c => (
                  <tr key={c.id}>
                    <td style={{ fontWeight: 600 }}>{c.titulo}</td>
                    <td>{fecha(c.fechaInicio)} – {fecha(c.fechaFin)}</td>
                    <td>{c.inscritos} / {c.cupoMaximo}</td>
                    <td><Estado valor={c.estado} /></td>
                    <td>
                      {c.estado === 'FINALIZADO' ? (
                        <span className="muted">Certificados emitidos</span>
                      ) : c.estado !== 'CANCELADO' && (
                        <button className="btn-primary" style={{ padding: '6px 12px' }}
                                disabled={finalizando === c.id} onClick={() => finalizar(c)}>
                          {finalizando === c.id ? 'Emitiendo...' : 'Finalizar y certificar'}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
