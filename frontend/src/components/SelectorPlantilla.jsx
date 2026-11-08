import { TIPOS_ACTIVIDAD } from '../config/certificados';

// Lista de plantillas agrupadas por tipo de actividad. Valor vacío = diseño estándar.
export default function SelectorPlantilla({ plantillas, value, onChange, ...props }) {
  return (
    <select value={value ?? ''} onChange={e => onChange(e.target.value || null)} {...props}>
      <option value="">Diseño estándar</option>
      {Object.entries(TIPOS_ACTIVIDAD).map(([tipo, etiqueta]) => {
        const delTipo = plantillas.filter(p => p.tipoActividad === tipo);
        return delTipo.length > 0 && (
          <optgroup key={tipo} label={etiqueta}>
            {delTipo.map(p => <option key={p.id} value={p.id}>{p.nombre}</option>)}
          </optgroup>
        );
      })}
    </select>
  );
}
