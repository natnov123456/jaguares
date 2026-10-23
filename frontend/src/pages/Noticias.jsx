import { useEffect, useState } from 'react';
import { contenidoApi } from '../services/api';

export default function Noticias() {
  const [noticias, setNoticias] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    contenidoApi.noticias()
      .then(r => setNoticias(r.data))
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="container" style={{ padding: '32px 24px' }}>
      <h1>Noticias</h1>
      {loading ? (
        <p>Cargando...</p>
      ) : noticias.length === 0 ? (
        <p style={{ color: '#6b7280', marginTop: 16 }}>Aún no hay noticias publicadas.</p>
      ) : (
        <div className="grid">
          {noticias.map(n => (
            <div key={n.id} className="card">
              {n.imagenUrl && (
                <img src={n.imagenUrl} alt={n.titulo} style={{ width: '100%', height: 180, objectFit: 'cover' }}
                     onError={(e) => { e.target.style.display = 'none'; }} />
              )}
              <div className="card-body">
                <p style={{ color: '#6b7280', fontSize: '0.85rem' }}>
                  {n.publicadoEn && new Date(n.publicadoEn).toLocaleDateString('es-BO')}
                </p>
                <h3>{n.titulo}</h3>
                {n.resumen && <p style={{ fontWeight: 500 }}>{n.resumen}</p>}
                <p style={{ whiteSpace: 'pre-line', marginTop: 8 }}>{n.contenido}</p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
