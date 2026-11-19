import { useEffect, useState } from 'react';
import { indicadoresApi } from '../../services/api';
import BotonExportar from '../../components/BotonExportar';
import { bs, MESES } from '../../utils/formato';

// JAG-85 / FUJ-123: panel de indicadores de la Directora. 6 tarjetas + donaciones de los últimos 6 meses
// + inscritos por curso. Al cambiar el mes se recalcula todo. Diseño responsive (se revisa desde el celular).
const COLOR = '#b45309'; // un solo color de serie (validado contra la superficie clara)
const mesActual = () => {
  const d = new Date(Date.now() - 4 * 3600 * 1000);
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
};
const nombreMes = (aaaamm, corto = false) => {
  const [a, m] = aaaamm.split('-').map(Number);
  const n = MESES[m - 1];
  return corto ? `${n.slice(0, 3)} ${String(a).slice(2)}` : `${n} ${a}`;
};
const pct = (v) => (v == null ? '—' : `${v.toLocaleString('es-BO', { maximumFractionDigits: 1 })} %`);

function Tarjeta({ titulo, valor, detalle, tendencia }) {
  return (
    <div className="kpi">
      <p className="kpi-titulo">{titulo}</p>
      <p className="kpi-valor">{valor}</p>
      {tendencia}
      {detalle && <p className="kpi-detalle">{detalle}</p>}
    </div>
  );
}

function Variacion({ valor }) {
  if (valor == null) return <p className="kpi-detalle">Sin donaciones el mes anterior</p>;
  const sube = valor >= 0;
  // La flecha y el texto acompañan al color: el dato no depende solo del color
  return (
    <p className={`kpi-tendencia ${sube ? 'sube' : 'baja'}`}>
      {sube ? '▲' : '▼'} {sube ? '+' : ''}{pct(valor)} vs. mes anterior
    </p>
  );
}

// Columnas: barras de ≤ 24 px, punta redondeada, base recta; valor rotulado solo en el mes elegido; tooltip al pasar
function ColumnasDonaciones({ serie }) {
  const [activo, setActivo] = useState(null);
  const max = Math.max(...serie.map(s => s.total), 1);
  const ancho = 360, alto = 180, base = 150, techo = 24;
  const paso = ancho / serie.length;
  const barra = Math.min(24, paso * 0.5);
  const y = (v) => base - (v / max) * (base - techo);
  return (
    <div className="grafico" onMouseLeave={() => setActivo(null)}>
      <svg viewBox={`0 0 ${ancho} ${alto}`} role="img" aria-label="Donaciones de los últimos 6 meses">
        <line x1="0" x2={ancho} y1={base} y2={base} stroke="#e5e7eb" strokeWidth="1" />
        {serie.map((s, i) => {
          const x = i * paso + (paso - barra) / 2;
          const alturaBarra = Math.max(base - y(s.total), s.total > 0 ? 2 : 0);
          const ultimo = i === serie.length - 1;
          return (
            <g key={s.mes} onMouseEnter={() => setActivo(i)} onFocus={() => setActivo(i)} tabIndex={0}>
              {/* área de interacción más grande que la barra */}
              <rect x={i * paso} y={0} width={paso} height={base} fill="transparent" />
              {alturaBarra > 0 && (
                <path d={`M${x},${base} V${base - alturaBarra + 4} Q${x},${base - alturaBarra} ${x + 4},${base - alturaBarra}
                          H${x + barra - 4} Q${x + barra},${base - alturaBarra} ${x + barra},${base - alturaBarra + 4} V${base} Z`}
                      fill={COLOR} opacity={activo == null || activo === i ? 1 : 0.45} />
              )}
              <text x={i * paso + paso / 2} y={base + 18} textAnchor="middle" fontSize="11" fill="#6b7280">{nombreMes(s.mes, true)}</text>
              {ultimo && (
                <text x={i * paso + paso / 2} y={base - alturaBarra - 6} textAnchor="middle" fontSize="11" fontWeight="600" fill="#1f2937">
                  {bs(s.total)}
                </text>
              )}
            </g>
          );
        })}
      </svg>
      {activo != null && (
        <div className="grafico-tooltip" role="status">
          <strong>{nombreMes(serie[activo].mes)}</strong>: {bs(serie[activo].total)}
        </div>
      )}
    </div>
  );
}

// Barras horizontales: inscritos por curso, de mayor a menor, valor en la punta
function BarrasCursos({ cursos }) {
  const max = Math.max(...cursos.map(c => c.inscritos), 1);
  return (
    <ul className="barras-h" aria-label="Inscritos por curso">
      {cursos.map(c => (
        <li key={c.cursoId} title={`${c.curso}: ${c.inscritos} inscrito(s)`}>
          <span className="barras-h-etiqueta">{c.curso}</span>
          <span className="barras-h-pista">
            <span className="barras-h-barra" style={{ width: `${(c.inscritos / max) * 100}%`, background: COLOR }} />
            <span className="barras-h-valor">{c.inscritos}</span>
          </span>
        </li>
      ))}
    </ul>
  );
}

export default function Indicadores() {
  const [mes, setMes] = useState(mesActual());
  const [datos, setDatos] = useState(null);
  const [error, setError] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [verTabla, setVerTabla] = useState(false);

  useEffect(() => {
    if (!mes) return;
    let vigente = true;
    setCargando(true);
    setError(null);
    indicadoresApi.obtener(mes)
      .then(r => { if (vigente) setDatos(r.data); })
      .catch(err => { if (vigente) setError(err.response?.data?.error || 'No se pudieron cargar los indicadores'); })
      .finally(() => { if (vigente) setCargando(false); });
    return () => { vigente = false; };
  }, [mes]);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Panel de indicadores</h1>
          <p>Los indicadores clave de la Fundación en una sola pantalla. Se actualizan cada 10 minutos.</p>
        </div>
        <div className="barra-filtros" style={{ marginBottom: 0 }}>
          <div className="barra-filtros-campos">
            <label className="filtro">
              <span>Mes</span>
              <input type="month" value={mes} max={mesActual()} onChange={e => setMes(e.target.value)} />
            </label>
            <BotonExportar reporte="indicadores" filtros={{ desde: `${mes}-01` }} deshabilitado={!mes} />
          </div>
        </div>
      </div>

      {error && <div className="error-msg">{error}</div>}
      {!datos ? <p>Cargando...</p> : (
        <div style={{ opacity: cargando ? 0.5 : 1, transition: 'opacity .2s' }} aria-busy={cargando}>
          <div className="kpis">
            <Tarjeta titulo="Inscritos del mes" valor={datos.inscritosMes}
                     detalle={`${datos.inscritosCursos} en cursos · ${datos.inscritosEventos} en eventos`} />
            <Tarjeta titulo="Asistencia promedio" valor={pct(datos.asistenciaPromedio)} detalle="Clases y eventos del mes" />
            <Tarjeta titulo="Certificados emitidos" valor={datos.certificadosEmitidos} />
            <Tarjeta titulo="Donaciones del mes" valor={bs(datos.donacionesMes)}
                     tendencia={<Variacion valor={datos.variacionDonacionesPct} />}
                     detalle={`${datos.cantidadDonaciones} aporte(s) · mes anterior ${bs(datos.donacionesMesAnterior)}`} />
            <Tarjeta titulo="Voluntarios activos" valor={datos.voluntariosActivos}
                     detalle={`${datos.voluntariosConActividad} participaron este mes`} />
            <Tarjeta titulo="Satisfacción promedio"
                     valor={datos.satisfaccionPromedio == null ? '—' : `${datos.satisfaccionPromedio.toLocaleString('es-BO')} / 5`}
                     detalle={`${datos.respuestasEncuestas} encuesta(s) respondida(s)`} />
          </div>

          <div className="split" style={{ marginTop: 24 }}>
            <section className="panel">
              <h2>Donaciones de los últimos 6 meses</h2>
              <p className="muted" style={{ fontSize: '0.85rem' }}>Hasta {nombreMes(datos.mes)} · solo donaciones confirmadas</p>
              <ColumnasDonaciones serie={datos.donacionesUltimosMeses} />
              <button type="button" className="btn-link" onClick={() => setVerTabla(v => !v)}>
                {verTabla ? 'Ocultar tabla' : 'Ver como tabla'}
              </button>
              {verTabla && (
                <table className="table" style={{ marginTop: 8 }}>
                  <thead><tr><th>Mes</th><th>Total</th></tr></thead>
                  <tbody>{datos.donacionesUltimosMeses.map(s => <tr key={s.mes}><td>{nombreMes(s.mes)}</td><td>{bs(s.total)}</td></tr>)}</tbody>
                </table>
              )}
            </section>
            <section className="panel">
              <h2>Inscritos por curso</h2>
              <p className="muted" style={{ fontSize: '0.85rem' }}>Cursos activos en {nombreMes(datos.mes)}</p>
              {datos.inscritosPorCurso.length === 0
                ? <p className="muted">No hubo cursos activos en este mes.</p>
                : <BarrasCursos cursos={datos.inscritosPorCurso} />}
            </section>
          </div>
          <p className="muted" style={{ fontSize: '0.8rem', marginTop: 12 }}>
            Calculado el {new Date(datos.calculadoEn).toLocaleString('es-BO')}{datos.desdeCache ? ' (desde la caché)' : ''}.
          </p>
        </div>
      )}
    </div>
  );
}
