import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { cursosApi } from '../services/api';
import { fecha, fechaHora } from '../utils/formato';

// FUJ-111: cursos llenos en los que el participante espera un cupo, con su posición en la cola
export default function MiListaEspera({ onCambio }) {
  const [lista, setLista] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);

  const cargar = () =>
    cursosApi.miListaEspera()
      .then(r => setLista(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar tu lista de espera' }))
      .finally(() => setLoading(false));

  useEffect(() => { cargar(); }, []);

  const salir = async (l) => {
    if (!window.confirm(`¿Salir de la lista de espera de "${l.titulo}"? Perderás tu posición.`)) return;
    try {
      await cursosApi.salirListaEspera(l.cursoId);
      setMsg({ tipo: 'success', texto: 'Saliste de la lista de espera' });
      cargar();
      onCambio?.();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo salir de la lista' });
    }
  };

  if (loading) return <p>Cargando...</p>;

  return (
    <>
      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}
      {lista.length === 0 ? (
        <p className="muted">
          No estás en ninguna lista de espera. Si un curso está lleno, puedes anotarte desde{' '}
          <Link to="/cursos" style={{ color: '#b45309' }}>Cursos</Link> y te inscribiremos automáticamente cuando se libere un cupo.
        </p>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Curso</th><th>Fechas</th><th>Tu posición</th><th>Anotado/a</th><th /></tr></thead>
            <tbody>
              {lista.map(l => (
                <tr key={l.cursoId}>
                  <td style={{ fontWeight: 600 }}>{l.titulo}</td>
                  <td>{fecha(l.fechaInicio)} – {fecha(l.fechaFin)}</td>
                  <td><span className="badge badge-warn">{l.posicion} de {l.totalEnEspera}</span></td>
                  <td>{fechaHora(l.anotadoEn)}</td>
                  <td><button className="btn-secondary" onClick={() => salir(l)}>Salir</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
