import { useEffect, useState } from 'react';
import { contactoApi, institucionalApi } from '../services/api';
import './QuienesSomos.css';

// FUJ-127: misión, visión, historia, equipo y contacto + formulario de consulta

const VACIO = { nombre: '', email: '', asunto: '', mensaje: '', sitioWeb: '' };
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Mismas reglas que el backend, para avisar sin esperar al servidor
function validar(f) {
  const e = {};
  if (f.nombre.trim().length < 2) e.nombre = 'Ingresa tu nombre';
  if (!f.email.trim()) e.email = 'Ingresa tu correo';
  else if (!EMAIL_RE.test(f.email.trim())) e.email = 'Correo inválido';
  if (f.mensaje.trim().length < 10) e.mensaje = 'El mensaje debe tener al menos 10 caracteres';
  return e;
}

function FormularioContacto() {
  const [form, setForm] = useState(VACIO);
  const [errores, setErrores] = useState({});
  const [estado, setEstado] = useState(null); // { tipo, texto }
  const [enviando, setEnviando] = useState(false);

  const cambiar = (campo) => (e) => {
    setForm({ ...form, [campo]: e.target.value });
    if (errores[campo]) setErrores({ ...errores, [campo]: undefined });
  };

  const enviar = async (e) => {
    e.preventDefault();
    setEstado(null);
    const errs = validar(form);
    setErrores(errs);
    if (Object.keys(errs).length > 0) return;
    setEnviando(true);
    try {
      const { data } = await contactoApi.enviar({
        nombre: form.nombre.trim(), email: form.email.trim(), asunto: form.asunto.trim() || null,
        mensaje: form.mensaje.trim(), sitioWeb: form.sitioWeb,
      });
      setForm(VACIO);
      setEstado({ tipo: 'success', texto: data.mensaje });
    } catch (err) {
      const r = err.response?.data;
      if (r?.errores) setErrores(r.errores);
      setEstado({ tipo: 'error', texto: r?.error || 'No se pudo enviar tu consulta. Intenta de nuevo.' });
    } finally {
      setEnviando(false);
    }
  };

  const campo = (nombre, etiqueta, input) => (
    <div>
      <label htmlFor={`c-${nombre}`}>{etiqueta}</label>
      {input}
      {errores[nombre] && <span className="qs-error" id={`c-${nombre}-error`}>{errores[nombre]}</span>}
    </div>
  );
  const props = (nombre) => ({
    id: `c-${nombre}`, value: form[nombre], onChange: cambiar(nombre),
    'aria-invalid': !!errores[nombre], 'aria-describedby': errores[nombre] ? `c-${nombre}-error` : undefined,
  });

  return (
    <form onSubmit={enviar} className="form-stack" noValidate>
      {estado && <div className={`${estado.tipo}-msg`} role="status">{estado.texto}</div>}
      {campo('nombre', 'Nombre', <input {...props('nombre')} maxLength={150} autoComplete="name" />)}
      {campo('email', 'Correo electrónico', <input {...props('email')} type="email" maxLength={150} autoComplete="email" />)}
      {campo('asunto', 'Asunto (opcional)', <input {...props('asunto')} maxLength={150} />)}
      {campo('mensaje', 'Mensaje', <textarea {...props('mensaje')} rows="5" maxLength={3000} />)}
      {/* Campo trampa anti-spam: invisible para personas; si se llena, el backend descarta el envío */}
      <div className="qs-trampa" aria-hidden="true">
        <label htmlFor="c-sitioWeb">Sitio web</label>
        <input id="c-sitioWeb" tabIndex={-1} autoComplete="off" value={form.sitioWeb} onChange={cambiar('sitioWeb')} />
      </div>
      <button type="submit" className="btn-primary" disabled={enviando}>{enviando ? 'Enviando...' : 'Enviar consulta'}</button>
    </form>
  );
}

export default function QuienesSomos() {
  const [info, setInfo] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    institucionalApi.obtener()
      .then(r => setInfo(r.data))
      .catch(() => setError('No se pudo cargar la información de la fundación'));
  }, []);

  return (
    <div className="qs">
      <section className="qs-hero">
        <div className="container">
          <h1>Quiénes somos</h1>
          <p>Conoce a la Fundación Unión de los Jaguares y escríbenos.</p>
        </div>
      </section>

      <div className="container page">
        {error && <div className="error-msg">{error}</div>}
        {!info && !error && <p>Cargando...</p>}
        {info && (
          <>
            <div className="qs-pilares">
              {info.mision && <section className="panel"><h2>🎯 Misión</h2><p>{info.mision}</p></section>}
              {info.vision && <section className="panel"><h2>🔭 Visión</h2><p>{info.vision}</p></section>}
            </div>

            {info.historia && (
              <section className="panel"><h2>📖 Nuestra historia</h2><p className="qs-texto">{info.historia}</p></section>
            )}

            {info.equipo.length > 0 && (
              <section className="panel">
                <h2>👥 Nuestro equipo</h2>
                <div className="qs-equipo">
                  {info.equipo.map((m, i) => (
                    <div key={i} className="qs-miembro">
                      <div className="qs-avatar" aria-hidden="true">{m.nombre.charAt(0).toUpperCase()}</div>
                      <strong>{m.nombre}</strong>
                      {m.cargo && <span className="muted">{m.cargo}</span>}
                      {m.descripcion && <p>{m.descripcion}</p>}
                    </div>
                  ))}
                </div>
              </section>
            )}
          </>
        )}

        <div className="split" id="contacto">
          <section className="panel">
            <h2>✉️ Escríbenos</h2>
            <FormularioContacto />
          </section>
          {info && (
            <section className="panel">
              <h2>Datos de contacto</h2>
              <ul className="qs-datos">
                {info.direccion && <li>📍 {info.direccion}</li>}
                {info.telefono && <li>📞 <a href={`tel:${info.telefono.replace(/\s/g, '')}`}>{info.telefono}</a></li>}
                {info.emailContacto && <li>✉️ <a href={`mailto:${info.emailContacto}`}>{info.emailContacto}</a></li>}
                {info.horario && <li>🕘 {info.horario}</li>}
              </ul>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}
