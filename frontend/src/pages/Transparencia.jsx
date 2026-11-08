import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { transparenciaApi } from '../services/api';
import { bs, fecha, MESES } from '../utils/formato';
import './Transparencia.css';

// FUJ-121: resumen público de cómo se usan las donaciones, mes por mes (sin datos de donantes)
export default function Transparencia() {
  const [meses, setMeses] = useState([]);
  const [sel, setSel] = useState('');
  const [resumen, setResumen] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    transparenciaApi.meses()
      .then(r => {
        setMeses(r.data);
        if (r.data.length > 0) setSel(`${r.data[0].anio}-${r.data[0].mes}`);
        else setLoading(false);
      })
      .catch(() => { setError('No se pudo cargar la información de transparencia'); setLoading(false); });
  }, []);

  useEffect(() => {
    if (!sel) return undefined;
    const [anio, mes] = sel.split('-');
    let vigente = true;
    transparenciaApi.resumen(anio, mes)
      .then(r => { if (vigente) { setResumen(r.data); setError(null); } })
      .catch(() => { if (vigente) setError('No se pudo cargar el resumen de ese mes'); })
      .finally(() => { if (vigente) setLoading(false); });
    return () => { vigente = false; };
  }, [sel]);

  const porAsignar = resumen ? Math.max(0, resumen.totalRecaudado - resumen.totalAsignado) : 0;
  const maxMonto = resumen ? Math.max(...resumen.destinos.map(d => d.monto), porAsignar, 1) : 1;

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Transparencia</h1>
          <p>Cuánto recibimos en donaciones cada mes y en qué se usó. No se publica ningún dato personal de los donantes.</p>
        </div>
        {meses.length > 0 && (
          <div className="form-stack" style={{ minWidth: 200 }}>
            <label htmlFor="mes">Mes</label>
            <select id="mes" value={sel} onChange={e => { setLoading(true); setSel(e.target.value); }}>
              {meses.map(m => (
                <option key={`${m.anio}-${m.mes}`} value={`${m.anio}-${m.mes}`}>{MESES[m.mes - 1]} {m.anio}</option>
              ))}
            </select>
          </div>
        )}
      </div>

      {error && <div className="error-msg">{error}</div>}

      {loading ? (
        <p>Cargando...</p>
      ) : meses.length === 0 ? (
        <section className="panel" style={{ textAlign: 'center' }}>
          <p className="muted">Todavía no hay resúmenes publicados. Vuelve pronto.</p>
        </section>
      ) : resumen && (
        <>
          <div className="tr-tarjetas">
            <div className="stat-box"><p>Total recaudado</p><h2>{bs(resumen.totalRecaudado)}</h2></div>
            <div className="stat-box"><p>Donaciones recibidas</p><h2>{resumen.cantidadDonaciones}</h2></div>
            <div className="stat-box"><p>Destinado a proyectos</p><h2>{bs(resumen.totalAsignado)}</h2></div>
          </div>

          <section className="panel">
            <h2>¿En qué se usó? — {MESES[resumen.mes - 1]} {resumen.anio}</h2>
            <div className="tr-barras">
              {resumen.destinos.map(d => (
                <div key={d.categoria} className="tr-fila">
                  <div className="tr-etiqueta">
                    <strong>{d.categoria}</strong>
                    <span>{bs(d.monto)} · {d.porcentaje}%</span>
                  </div>
                  <div className="tr-pista"><div className="tr-barra" style={{ width: `${(d.monto / maxMonto) * 100}%` }} /></div>
                  {d.descripcion && <p className="muted tr-desc">{d.descripcion}</p>}
                </div>
              ))}
              {porAsignar > 0 && (
                <div className="tr-fila">
                  <div className="tr-etiqueta">
                    <strong>Reservado para próximas actividades</strong>
                    <span>{bs(porAsignar)}</span>
                  </div>
                  <div className="tr-pista"><div className="tr-barra tr-reserva" style={{ width: `${(porAsignar / maxMonto) * 100}%` }} /></div>
                </div>
              )}
            </div>
            {resumen.nota && <p className="tr-nota">{resumen.nota}</p>}
            {resumen.publicadoEn && <p className="muted" style={{ fontSize: '0.85rem', marginTop: 12 }}>Publicado el {fecha(resumen.publicadoEn)}</p>}
          </section>

          <section className="panel tr-cta">
            <p>Cada aporte cuenta y rendimos cuentas de cada boliviano.</p>
            <Link to="/donaciones" className="btn-primary">Quiero donar</Link>
          </section>
        </>
      )}
    </div>
  );
}
