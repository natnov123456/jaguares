import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { transparenciaApi } from '../../services/api';
import { bs, fechaHora, MESES } from '../../utils/formato';
import '../Transparencia.css';

// FUJ-121: la Directora publica en el sitio el resumen del mes (total del reporte financiero + destinos)

const CATEGORIAS_SUGERIDAS = ['Cursos y capacitaciones', 'Eventos comunitarios', 'Materiales educativos',
  'Conservación', 'Apoyo a voluntarios', 'Gastos administrativos'];
const FILA_VACIA = { categoria: '', monto: '', descripcion: '' };

export default function AdminTransparencia() {
  const [hoy] = useState(() => new Date());
  const [periodo, setPeriodo] = useState({ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1 });
  const [previa, setPrevia] = useState(null);
  const [destinos, setDestinos] = useState([FILA_VACIA]);
  const [nota, setNota] = useState('');
  const [msg, setMsg] = useState(null);
  const [guardando, setGuardando] = useState(false);
  const [recarga, setRecarga] = useState(0);

  useEffect(() => {
    let vigente = true;
    transparenciaApi.vistaPrevia(periodo.anio, periodo.mes)
      .then(({ data }) => {
        if (!vigente) return;
        setPrevia(data);
        setDestinos(data.destinos.length > 0
          ? data.destinos.map(d => ({ categoria: d.categoria, monto: String(d.monto), descripcion: d.descripcion ?? '' }))
          : [FILA_VACIA]);
        setNota(data.nota ?? '');
      })
      .catch(err => { if (vigente) setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo cargar el mes' }); });
    return () => { vigente = false; };
  }, [periodo.anio, periodo.mes, recarga]);

  const cambiarPeriodo = (cambio) => { setMsg(null); setPrevia(null); setPeriodo({ ...periodo, ...cambio }); };
  const editar = (i, campo, valor) => setDestinos(destinos.map((d, j) => (j === i ? { ...d, [campo]: valor } : d)));
  const quitar = (i) => setDestinos(destinos.length === 1 ? [FILA_VACIA] : destinos.filter((_, j) => j !== i));

  const asignado = destinos.reduce((s, d) => s + (parseFloat(d.monto) || 0), 0);
  const total = previa?.totalRecaudado ?? 0;
  const excede = asignado > total + 0.001;
  const futuro = periodo.anio > hoy.getFullYear() || (periodo.anio === hoy.getFullYear() && periodo.mes > hoy.getMonth() + 1);

  const publicar = async (e) => {
    e.preventDefault();
    setMsg(null);
    const lista = destinos.filter(d => d.categoria.trim() || d.monto !== '');
    if (lista.length === 0) { setMsg({ tipo: 'error', texto: 'Agrega al menos un destino' }); return; }
    if (lista.some(d => !d.categoria.trim() || d.monto === '' || Number(d.monto) < 0)) {
      setMsg({ tipo: 'error', texto: 'Cada destino necesita categoría y un monto válido' });
      return;
    }
    setGuardando(true);
    try {
      await transparenciaApi.publicar(periodo.anio, periodo.mes, {
        destinos: lista.map(d => ({ categoria: d.categoria.trim(), monto: Number(d.monto), descripcion: d.descripcion.trim() || null })),
        nota: nota.trim() || null,
      });
      setMsg({ tipo: 'success', texto: '✅ Resumen publicado en el sitio' });
      setRecarga(n => n + 1);
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo publicar' });
    } finally {
      setGuardando(false);
    }
  };

  const despublicar = async () => {
    if (!window.confirm(`¿Quitar del sitio el resumen de ${MESES[periodo.mes - 1]} ${periodo.anio}?`)) return;
    try {
      await transparenciaApi.despublicar(periodo.anio, periodo.mes);
      setMsg({ tipo: 'success', texto: 'Resumen retirado del sitio' });
      setRecarga(n => n + 1);
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo retirar' });
    }
  };

  const anios = Array.from({ length: 5 }, (_, i) => hoy.getFullYear() - i);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Transparencia</h1>
          <p>Publica cuánto se recaudó en el mes y en qué se usó. El sitio no muestra datos de los donantes.</p>
        </div>
        <Link to="/transparencia" className="btn-secondary">Ver página pública</Link>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <div className="split">
        <section className="panel">
          <h2>Destino de los fondos</h2>
          <form onSubmit={publicar} className="form-stack">
            <datalist id="categorias">{CATEGORIAS_SUGERIDAS.map(c => <option key={c} value={c} />)}</datalist>
            {destinos.map((d, i) => (
              <div key={i} className="tr-destino">
                <input list="categorias" placeholder="Categoría" value={d.categoria} maxLength={80}
                       onChange={e => editar(i, 'categoria', e.target.value)} aria-label="Categoría" />
                <input type="number" min="0" step="0.01" placeholder="Monto Bs." value={d.monto}
                       onChange={e => editar(i, 'monto', e.target.value)} aria-label="Monto" />
                <input className="tr-col-desc" placeholder="Detalle (opcional)" value={d.descripcion} maxLength={300}
                       onChange={e => editar(i, 'descripcion', e.target.value)} aria-label="Detalle" />
                <button type="button" className="btn-secondary" onClick={() => quitar(i)} aria-label="Quitar destino">✕</button>
              </div>
            ))}
            <div>
              <button type="button" className="btn-secondary" onClick={() => setDestinos([...destinos, FILA_VACIA])}
                      disabled={destinos.length >= 15}>+ Agregar categoría</button>
            </div>

            <div className={`tr-suma ${excede ? 'excede' : ''}`}>
              <span>Asignado: {bs(asignado)} de {bs(total)}</span>
              <span>{excede ? 'Supera lo recaudado' : `Sin asignar: ${bs(total - asignado)}`}</span>
            </div>

            <div>
              <label htmlFor="nota">Nota para el público (opcional)</label>
              <textarea id="nota" rows="3" maxLength={500} value={nota} onChange={e => setNota(e.target.value)}
                        placeholder="Ej: Este mes se compraron materiales para el taller de reciclaje." />
            </div>

            <button type="submit" className="btn-primary" disabled={guardando || excede || futuro || !previa}>
              {previa?.publicado ? 'Actualizar publicación' : 'Publicar en el sitio'}
            </button>
          </form>
        </section>

        <section className="panel">
          <h2>Mes</h2>
          <div className="form-stack">
            <div>
              <label htmlFor="t-mes">Mes</label>
              <select id="t-mes" value={periodo.mes} onChange={e => cambiarPeriodo({ mes: Number(e.target.value) })}>
                {MESES.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="t-anio">Año</label>
              <select id="t-anio" value={periodo.anio} onChange={e => cambiarPeriodo({ anio: Number(e.target.value) })}>
                {anios.map(a => <option key={a} value={a}>{a}</option>)}
              </select>
            </div>
          </div>

          {futuro ? (
            <p className="info-msg" style={{ marginTop: 16 }}>Ese mes todavía no empezó.</p>
          ) : previa && (
            <div style={{ marginTop: 16 }}>
              <div className="stat-box"><p>Recaudado (reporte financiero)</p><h2>{bs(previa.totalRecaudado)}</h2></div>
              <p className="muted" style={{ marginTop: 8 }}>{previa.cantidadDonaciones} donaciones confirmadas</p>
              <p style={{ marginTop: 12 }}>
                {previa.publicado
                  ? <>✅ Publicado el {fechaHora(previa.publicadoEn)}</>
                  : <span className="muted">Aún no publicado</span>}
              </p>
              {previa.publicado && (
                <button className="btn-danger" style={{ marginTop: 12 }} onClick={despublicar}>Retirar del sitio</button>
              )}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
