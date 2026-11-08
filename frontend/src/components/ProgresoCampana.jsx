import { bs, fecha } from '../utils/formato';

// FUJ-117: barra de avance. Se llena hasta 100 %, pero el porcentaje muestra el valor real si se supera la meta.
export default function ProgresoCampana({ campana }) {
  return (
    <div>
      <div role="progressbar" aria-label={`Avance de ${campana.nombre}`} aria-valuemin={0} aria-valuemax={100}
           aria-valuenow={Math.min(100, campana.porcentaje)}
           style={{ height: 12, background: '#f3f4f6', borderRadius: 999, overflow: 'hidden', margin: '8px 0 6px' }}>
        <div style={{ width: `${Math.min(100, campana.porcentaje)}%`, height: '100%', background: '#d97706', transition: 'width .4s' }} />
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap', fontSize: '0.9rem' }}>
        <span><strong>{bs(campana.recaudado)}</strong> de {bs(campana.meta)}</span>
        <span className="muted">{campana.porcentaje}% · hasta {fecha(campana.fechaLimite)}</span>
      </div>
    </div>
  );
}
