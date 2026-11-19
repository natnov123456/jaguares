import { useEffect, useState } from 'react';
import { institucionalApi } from '../services/api';
import './QuienesSomos.css';

// FUJ-127: misión, visión, historia, equipo y datos de contacto

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
          <p>Conoce a la Fundación Unión de los Jaguares.</p>
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

        {info && (info.direccion || info.telefono || info.emailContacto || info.horario) && (
          <section className="panel" id="contacto">
            <h2>📍 Datos de contacto</h2>
            <ul className="qs-datos">
              {info.direccion && <li>📍 {info.direccion}</li>}
              {info.telefono && <li>📞 <a href={`tel:${info.telefono.replace(/\s/g, '')}`}>{info.telefono}</a></li>}
              {info.emailContacto && <li>✉️ {info.emailContacto}</li>}
              {info.horario && <li>🕘 {info.horario}</li>}
            </ul>
          </section>
        )}
      </div>
    </div>
  );
}
