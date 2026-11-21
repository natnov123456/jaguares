import { Fragment, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { voluntariosApi } from '../../services/api';
import Estado from '../../components/Estado';
import { fecha, horas as formatoHoras, horario, horasDeHorario } from '../../utils/formato';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aplicarFiltros, ESTADOS_VOLUNTARIO } from '../../utils/filtros';

export default function AdminVoluntarios() {
  const [voluntarios, setVoluntarios] = useState([]);
  const [loading, setLoading] = useState(true);
  const [abierto, setAbierto] = useState(null);
  const [actividad, setActividad] = useState('');
  const [horarioManual, setHorarioManual] = useState({ inicio: '', fin: '' });
  const [mensaje, setMensaje] = useState('');
  const { valores, cambiar, limpiar } = useFiltros();
  const [periodoHoras, setPeriodoHoras] = useState({ desde: '', hasta: '' });

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
      await voluntariosApi.registrarParticipacion(voluntarioId, {
        eventoId: null, descripcion: actividad,
        horaInicio: horarioManual.inicio || null, horaFin: horarioManual.fin || null,
      });
      setActividad('');
      setHorarioManual({ inicio: '', fin: '' });
      setMensaje('✅ Actividad registrada');
      cargar();
    } catch (err) {
      setMensaje(`❌ ${err.response?.data?.error || 'Error al registrar la actividad'}`);
    }
  };

  const visibles = aplicarFiltros(voluntarios, valores,
    { texto: v => [v.nombreCompleto, v.email, v.telefono], estado: v => v.estado, fecha: v => v.postuladoEn });
  const activos = voluntarios.filter(v => v.estado === 'ACTIVO').length;
  const totalHoras = voluntarios.reduce((n, v) => n + (v.totalHoras ?? 0), 0);

  return (
    <div className="container" style={{ padding: '48px 24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '32px' }}>
        <div>
          <h1 style={{ fontSize: '2rem', color: '#111827' }}>Voluntarios</h1>
          <p style={{ color: '#6b7280' }}>Listado único de voluntarios con su historial de actividades</p>
        </div>
        <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="stat-box">
            <p>Activos</p>
            <h2>{activos} / {voluntarios.length}</h2>
          </div>
          <div className="stat-box">
            <p>Horas aportadas</p>
            <h2>{formatoHoras(totalHoras)}</h2>
          </div>
          <Link to="/admin/voluntarios/asignaciones" className="btn-primary">Asignar a actividades</Link>
        </div>
      </div>

      <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Nombre, correo o teléfono"
                    estados={ESTADOS_VOLUNTARIO} fechas="Postulación" total={voluntarios.length} visibles={visibles.length}>
        <BotonExportar reporte="voluntarios" filtros={valores} />
      </BarraFiltros>

      {/* FUJ-124: horas de voluntariado por rango de fechas (voluntario, actividades y horas acumuladas) */}
      <div className="barra-filtros" aria-label="Reporte de horas de voluntariado">
        <div className="barra-filtros-campos">
          <label className="filtro"><span>Horas de voluntariado — desde</span>
            <input type="date" value={periodoHoras.desde} max={periodoHoras.hasta || undefined}
                   onChange={e => setPeriodoHoras({ ...periodoHoras, desde: e.target.value })} /></label>
          <label className="filtro"><span>hasta</span>
            <input type="date" value={periodoHoras.hasta} min={periodoHoras.desde || undefined}
                   onChange={e => setPeriodoHoras({ ...periodoHoras, hasta: e.target.value })} /></label>
          <BotonExportar reporte="horas-voluntariado" etiqueta="Exportar horas"
                         filtros={{ ...periodoHoras, q: valores.q, estado: valores.estado }} />
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
                <th style={{ padding: '8px' }}>Horas</th>
                <th style={{ padding: '8px' }}>Actividades</th>
              </tr>
            </thead>
            <tbody>
              {visibles.map(v => (
                <Fragment key={v.id}>
                  <tr style={{ borderBottom: '1px solid #e5e7eb' }}>
                    <td style={{ padding: '12px 8px', fontWeight: 'bold' }}>{v.nombreCompleto}</td>
                    <td style={{ padding: '12px 8px' }}>
                      {v.email}<br /><span style={{ color: '#6b7280' }}>{v.telefono || '-'}</span>
                    </td>
                    <td style={{ padding: '12px 8px' }}><Estado valor={v.estado} /></td>
                    <td style={{ padding: '12px 8px' }}>{fecha(v.postuladoEn)}</td>
                    <td style={{ padding: '12px 8px', fontWeight: 600 }}>{formatoHoras(v.totalHoras)}</td>
                    <td style={{ padding: '12px 8px' }}>
                      <button className="btn-primary" style={{ padding: '4px 12px' }}
                              onClick={() => { setAbierto(abierto === v.id ? null : v.id); setMensaje(''); }}>
                        {abierto === v.id ? 'Ocultar' : `Ver historial (${v.historial.length})`}
                      </button>
                    </td>
                  </tr>
                  {abierto === v.id && (
                    <tr>
                      <td colSpan="6" style={{ padding: '16px', background: '#f9fafb' }}>
                        {v.historial.length === 0 ? (
                          <p style={{ color: '#6b7280' }}>Sin actividades registradas.</p>
                        ) : (
                          <ul style={{ paddingLeft: 20 }}>
                            {v.historial.map(h => (
                              <li key={h.id}>
                                <strong>{new Date(h.fecha).toLocaleDateString('es-BO')}</strong>
                                {(h.eventoTitulo || h.cursoTitulo) && ` — ${h.eventoTitulo || h.cursoTitulo}`}: {h.descripcion}
                                {h.horas != null && (
                                  <span className="badge badge-ok" style={{ marginLeft: 8 }}>
                                    {h.horaInicio ? `${horario(h.horaInicio, h.horaFin, h.dias)} · ` : ''}{formatoHoras(h.horas)}
                                  </span>
                                )}
                              </li>
                            ))}
                          </ul>
                        )}
                        <form onSubmit={(e) => registrarActividad(e, v.id)} style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
                          <input type="text" value={actividad} onChange={e => setActividad(e.target.value)} required
                                 placeholder="Ej: Apoyo en feria de reciclaje" maxLength={300}
                                 style={{ flex: 1, padding: '8px', border: '1px solid #d1d5db', borderRadius: '4px' }} />
                          <span className="horario-form" title="Horario en que colaboró (opcional)">
                            <label className="horario-campo">de
                              <input type="time" value={horarioManual.inicio} aria-label="Hora de inicio (opcional)"
                                     onChange={e => setHorarioManual(h => ({ ...h, inicio: e.target.value }))} />
                            </label>
                            <label className="horario-campo">a
                              <input type="time" value={horarioManual.fin} aria-label="Hora de fin (opcional)"
                                     onChange={e => setHorarioManual(h => ({ ...h, fin: e.target.value }))} />
                            </label>
                            <span className="horario-total">
                              {horasDeHorario(horarioManual.inicio, horarioManual.fin) ? `= ${formatoHoras(horasDeHorario(horarioManual.inicio, horarioManual.fin))}` : ''}
                            </span>
                          </span>
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
