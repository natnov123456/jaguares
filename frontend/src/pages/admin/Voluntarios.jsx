import { Fragment, useEffect, useState } from 'react';
import { voluntariosApi } from '../../services/api';

export default function AdminVoluntarios() {
  const [voluntarios, setVoluntarios] = useState([]);
  const [loading, setLoading] = useState(true);
  const [abierto, setAbierto] = useState(null);
  const [actividad, setActividad] = useState('');
  const [mensaje, setMensaje] = useState('');

  const cargar = async () => {
    try {
      const { data } = await voluntariosApi.listar();
      setVoluntarios(data);
    } catch (error) {
      console.error('Error al cargar voluntarios:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    cargar();
  }, []);

  const registrarActividad = async (e, voluntarioId) => {
    e.preventDefault();
    setMensaje('');
    try {
      await voluntariosApi.registrarParticipacion(voluntarioId, { eventoId: null, descripcion: actividad });
      setActividad('');
      setMensaje('✅ Actividad registrada');
      cargar();
    } catch {
      setMensaje('❌ Error al registrar la actividad');
    }
  };

  const activos = voluntarios.filter(v => v.estado === 'ACTIVO').length;

  return (
    <div className="container" style={{ padding: '48px 24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '32px' }}>
        <div>
          <h1 style={{ fontSize: '2rem', color: '#111827' }}>Voluntarios</h1>
          <p style={{ color: '#6b7280' }}>Listado único de voluntarios con su historial de actividades</p>
        </div>
        <div style={{ background: '#fef3c7', padding: '16px 24px', borderRadius: '8px', borderLeft: '4px solid #f59e0b' }}>
          <p style={{ fontSize: '0.9rem', color: '#92400e', margin: 0 }}>Activos</p>
          <h2 style={{ fontSize: '2rem', color: '#b45309', margin: 0 }}>{activos} / {voluntarios.length}</h2>
        </div>
      </div>

      <div className="card" style={{ padding: '24px' }}>
        {loading ? (
          <p>Cargando...</p>
        ) : voluntarios.length === 0 ? (
          <p style={{ color: '#6b7280', textAlign: 'center', padding: '24px' }}>No hay voluntarios registrados.</p>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid #e5e7eb', textAlign: 'left' }}>
                <th style={{ padding: '8px' }}>Nombre</th>
                <th style={{ padding: '8px' }}>Contacto</th>
                <th style={{ padding: '8px' }}>Estado</th>
                <th style={{ padding: '8px' }}>Postulado</th>
                <th style={{ padding: '8px' }}>Actividades</th>
              </tr>
            </thead>
            <tbody>
              {voluntarios.map(v => (
                <Fragment key={v.id}>
                  <tr style={{ borderBottom: '1px solid #e5e7eb' }}>
                    <td style={{ padding: '12px 8px', fontWeight: 'bold' }}>{v.nombreCompleto}</td>
                    <td style={{ padding: '12px 8px' }}>
                      {v.email}<br /><span style={{ color: '#6b7280' }}>{v.telefono || '-'}</span>
                    </td>
                    <td style={{ padding: '12px 8px' }}>{v.estado}</td>
                    <td style={{ padding: '12px 8px' }}>{new Date(v.postuladoEn).toLocaleDateString('es-BO')}</td>
                    <td style={{ padding: '12px 8px' }}>
                      <button className="btn-primary" style={{ padding: '4px 12px' }}
                              onClick={() => { setAbierto(abierto === v.id ? null : v.id); setMensaje(''); }}>
                        {abierto === v.id ? 'Ocultar' : `Ver historial (${v.historial.length})`}
                      </button>
                    </td>
                  </tr>
                  {abierto === v.id && (
                    <tr>
                      <td colSpan="5" style={{ padding: '16px', background: '#f9fafb' }}>
                        {v.historial.length === 0 ? (
                          <p style={{ color: '#6b7280' }}>Sin actividades registradas.</p>
                        ) : (
                          <ul style={{ paddingLeft: 20 }}>
                            {v.historial.map(h => (
                              <li key={h.id}>
                                <strong>{new Date(h.fecha).toLocaleDateString('es-BO')}</strong>
                                {h.eventoTitulo && ` — ${h.eventoTitulo}`}: {h.descripcion}
                              </li>
                            ))}
                          </ul>
                        )}
                        <form onSubmit={(e) => registrarActividad(e, v.id)} style={{ display: 'flex', gap: 8, marginTop: 12 }}>
                          <input type="text" value={actividad} onChange={e => setActividad(e.target.value)} required
                                 placeholder="Ej: Apoyo en feria de reciclaje" maxLength={300}
                                 style={{ flex: 1, padding: '8px', border: '1px solid #d1d5db', borderRadius: '4px' }} />
                          <button type="submit" className="btn-primary">Registrar actividad</button>
                        </form>
                        {mensaje && <p style={{ marginTop: 8, fontSize: '0.9rem' }}>{mensaje}</p>}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
