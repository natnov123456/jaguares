import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { cursosApi, plantillasApi } from '../../services/api';
import SelectorPlantilla from '../../components/SelectorPlantilla';

export default function CrearCurso() {
  const [form, setForm] = useState({ titulo: '', descripcion: '', fechaInicio: '', fechaFin: '', cupoMaximo: 25, asistenciaMinimaPct: 80, plantillaId: null, docenteId: '' });
  const [plantillas, setPlantillas] = useState([]);
  const [docentes, setDocentes] = useState(null);
  const [msg, setMsg] = useState(null);
  const navigate = useNavigate();

  // Si la lista no carga, el curso se puede crear igual con el diseño estándar y asignar la plantilla después
  useEffect(() => { plantillasApi.listar().then(r => setPlantillas(r.data)).catch(() => {}); }, []);
  useEffect(() => {
    cursosApi.docentes().then(r => setDocentes(r.data))
      .catch(() => { setDocentes([]); setMsg({ tipo: 'error', texto: 'No se pudo cargar la lista de docentes' }); });
  }, []);

  const crear = async (e) => {
    e.preventDefault();
    if (form.fechaFin < form.fechaInicio) {
      setMsg({ tipo: 'error', texto: 'La fecha de fin no puede ser anterior a la de inicio' });
      return;
    }
    if (!form.docenteId) {
      setMsg({ tipo: 'error', texto: 'Selecciona el docente a cargo del curso' });
      return;
    }
    try {
      await cursosApi.crear({ ...form, cupoMaximo: +form.cupoMaximo, asistenciaMinimaPct: +form.asistenciaMinimaPct });
      setMsg({ tipo: 'success', texto: 'Curso creado ✅' });
      setTimeout(() => navigate('/admin/cursos'), 1200);
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'Error al crear el curso' });
    }
  };

  return (
    <div className="auth-container">
      <h1>Crear curso</h1>
      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}
      <form onSubmit={crear}>
        <label>Título</label>
        <input type="text" value={form.titulo} onChange={e => setForm({...form, titulo: e.target.value})} required />
        <label>Descripción</label>
        <textarea value={form.descripcion} onChange={e => setForm({...form, descripcion: e.target.value})} />
        <label htmlFor="c-docente">Docente a cargo</label>
        <select id="c-docente" value={form.docenteId} onChange={e => setForm({...form, docenteId: e.target.value})} required>
          <option value="">{docentes === null ? 'Cargando...' : 'Selecciona un docente'}</option>
          {docentes?.map(d => <option key={d.id} value={d.id}>{d.nombre} ({d.email})</option>)}
        </select>
        {docentes?.length === 0 && (
          <p className="muted" style={{ fontSize: '0.85rem' }}>No hay cuentas con rol Docente activas. Crea una antes de registrar el curso.</p>
        )}
        <label>Fecha inicio</label>
        <input type="date" value={form.fechaInicio} onChange={e => setForm({...form, fechaInicio: e.target.value})} required />
        <label>Fecha fin</label>
        <input type="date" value={form.fechaFin} min={form.fechaInicio} onChange={e => setForm({...form, fechaFin: e.target.value})} required />
        <label>Cupo máximo</label>
        <input type="number" min="1" value={form.cupoMaximo} onChange={e => setForm({...form, cupoMaximo: e.target.value})} required />
        <label>Asistencia mínima para certificar (%)</label>
        <input type="number" min="0" max="100" value={form.asistenciaMinimaPct} onChange={e => setForm({...form, asistenciaMinimaPct: e.target.value})} required />
        <label htmlFor="c-plantilla">Plantilla de certificado</label>
        <SelectorPlantilla id="c-plantilla" plantillas={plantillas} value={form.plantillaId}
                           onChange={plantillaId => setForm({...form, plantillaId})} />
        <button type="submit" className="btn-primary">Crear curso</button>
      </form>
      <p><Link to="/admin/cursos">← Volver a los cursos</Link></p>
    </div>
  );
}
