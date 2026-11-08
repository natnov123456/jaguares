import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { institucionalApi } from '../../services/api';
import { fechaHora } from '../../utils/formato';
import '../QuienesSomos.css';

// FUJ-127: la Directora edita el contenido de "Quiénes somos" y el correo que recibe las consultas

const MIEMBRO_VACIO = { nombre: '', cargo: '', descripcion: '' };
const CAMPOS_TEXTO = ['mision', 'vision', 'historia', 'direccion', 'telefono', 'emailContacto', 'horario'];

export default function AdminInstitucional() {
  const [form, setForm] = useState(null);
  const [errores, setErrores] = useState({});
  const [msg, setMsg] = useState(null);
  const [guardando, setGuardando] = useState(false);
  const [actualizadoEn, setActualizadoEn] = useState(null);

  const cargarEnForm = (d) => {
    const f = { equipo: d.equipo.map(m => ({ nombre: m.nombre, cargo: m.cargo ?? '', descripcion: m.descripcion ?? '' })) };
    CAMPOS_TEXTO.forEach(c => { f[c] = d[c] ?? ''; });
    setForm(f);
    setActualizadoEn(d.actualizadoEn);
  };

  useEffect(() => {
    institucionalApi.obtener()
      .then(r => cargarEnForm(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar el contenido' }));
  }, []);

  if (!form) return <div className="container page">{msg ? <div className="error-msg">{msg.texto}</div> : <p>Cargando...</p>}</div>;

  const set = (campo) => (e) => setForm({ ...form, [campo]: e.target.value });
  const setMiembro = (i, campo, valor) =>
    setForm({ ...form, equipo: form.equipo.map((m, j) => (j === i ? { ...m, [campo]: valor } : m)) });
  const mover = (i, paso) => {
    const eq = [...form.equipo];
    [eq[i], eq[i + paso]] = [eq[i + paso], eq[i]];
    setForm({ ...form, equipo: eq });
  };

  const guardar = async (e) => {
    e.preventDefault();
    setMsg(null);
    setErrores({});
    const equipo = form.equipo.filter(m => m.nombre.trim() || m.cargo.trim() || m.descripcion.trim());
    if (equipo.some(m => !m.nombre.trim())) {
      setErrores({ equipo: 'Cada integrante necesita un nombre' });
      return;
    }
    setGuardando(true);
    try {
      const datos = { equipo: equipo.map(m => ({ nombre: m.nombre.trim(), cargo: m.cargo.trim() || null, descripcion: m.descripcion.trim() || null })) };
      CAMPOS_TEXTO.forEach(c => { datos[c] = form[c].trim() || null; });
      const { data } = await institucionalApi.actualizar(datos);
      cargarEnForm(data);
      setMsg({ tipo: 'success', texto: '✅ Cambios publicados en "Quiénes somos"' });
    } catch (err) {
      const r = err.response?.data;
      if (r?.errores) setErrores(r.errores);
      setMsg({ tipo: 'error', texto: r?.error || 'No se pudo guardar' });
    } finally {
      setGuardando(false);
    }
  };

  const texto = (campo, etiqueta, filas, max, ayuda) => (
    <div>
      <label htmlFor={`i-${campo}`}>{etiqueta}</label>
      {filas
        ? <textarea id={`i-${campo}`} rows={filas} maxLength={max} value={form[campo]} onChange={set(campo)} />
        : <input id={`i-${campo}`} maxLength={max} value={form[campo]} onChange={set(campo)} type={campo === 'emailContacto' ? 'email' : 'text'} />}
      {ayuda && <small className="muted">{ayuda}</small>}
      {errores[campo] && <span className="qs-error">{errores[campo]}</span>}
    </div>
  );

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Quiénes somos</h1>
          <p>Edita la información institucional que ve el público.{actualizadoEn && ` Última actualización: ${fechaHora(actualizadoEn)}.`}</p>
        </div>
        <Link to="/quienes-somos" className="btn-secondary">Ver página pública</Link>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <form onSubmit={guardar}>
        <div className="split">
          <div>
            <section className="panel">
              <h2>Institución</h2>
              <div className="form-stack">
                {texto('mision', 'Misión', 3, 3000)}
                {texto('vision', 'Visión', 3, 3000)}
                {texto('historia', 'Historia', 6, 10000)}
              </div>
            </section>

            <section className="panel">
              <h2>Equipo</h2>
              <div className="form-stack">
                {form.equipo.map((m, i) => (
                  <div key={i} className="qs-miembro-edit">
                    <input placeholder="Nombre" value={m.nombre} maxLength={150} aria-label="Nombre"
                           onChange={e => setMiembro(i, 'nombre', e.target.value)} />
                    <input className="qs-cargo" placeholder="Cargo" value={m.cargo} maxLength={150} aria-label="Cargo"
                           onChange={e => setMiembro(i, 'cargo', e.target.value)} />
                    <div style={{ display: 'flex', gap: 4 }}>
                      <button type="button" className="btn-secondary" disabled={i === 0} onClick={() => mover(i, -1)} aria-label="Subir">↑</button>
                      <button type="button" className="btn-secondary" disabled={i === form.equipo.length - 1} onClick={() => mover(i, 1)} aria-label="Bajar">↓</button>
                      <button type="button" className="btn-secondary" aria-label="Quitar"
                              onClick={() => setForm({ ...form, equipo: form.equipo.filter((_, j) => j !== i) })}>✕</button>
                    </div>
                    <textarea placeholder="Descripción breve (opcional)" rows="2" value={m.descripcion} maxLength={500}
                              aria-label="Descripción" onChange={e => setMiembro(i, 'descripcion', e.target.value)} />
                  </div>
                ))}
                {errores.equipo && <span className="qs-error">{errores.equipo}</span>}
                <div>
                  <button type="button" className="btn-secondary" disabled={form.equipo.length >= 30}
                          onClick={() => setForm({ ...form, equipo: [...form.equipo, MIEMBRO_VACIO] })}>+ Agregar integrante</button>
                </div>
              </div>
            </section>
          </div>

          <section className="panel">
            <h2>Contacto</h2>
            <div className="form-stack">
              {texto('emailContacto', 'Correo institucional', 0, 150, 'Aquí llegan las consultas del formulario de contacto.')}
              {texto('telefono', 'Teléfono / WhatsApp', 0, 50)}
              {texto('direccion', 'Dirección', 0, 300)}
              {texto('horario', 'Horario de atención', 0, 200)}
              <button type="submit" className="btn-primary" disabled={guardando}>{guardando ? 'Guardando...' : 'Guardar cambios'}</button>
            </div>
          </section>
        </div>
      </form>
    </div>
  );
}
