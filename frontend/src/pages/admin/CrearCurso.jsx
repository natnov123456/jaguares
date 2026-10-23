import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { cursosApi } from '../../services/api';

export default function CrearCurso() {
  const [form, setForm] = useState({ titulo: '', descripcion: '', fechaInicio: '', fechaFin: '', cupoMaximo: 25, asistenciaMinimaPct: 80 });
  const [msg, setMsg] = useState('');
  const navigate = useNavigate();

  const crear = async (e) => {
    e.preventDefault();
    try {
      await cursosApi.crear({ ...form, cupoMaximo: +form.cupoMaximo, asistenciaMinimaPct: +form.asistenciaMinimaPct });
      setMsg('Curso creado ✅');
      setTimeout(() => navigate('/admin'), 1200);
    } catch (err) {
      setMsg(err.response?.data?.error || 'Error');
    }
  };

  return (
    <div className="auth-container">
      <h1>Crear curso</h1>
      {msg && <div className="success-msg">{msg}</div>}
      <form onSubmit={crear}>
        <label>Título</label>
        <input type="text" value={form.titulo} onChange={e => setForm({...form, titulo: e.target.value})} required />
        <label>Descripción</label>
        <textarea value={form.descripcion} onChange={e => setForm({...form, descripcion: e.target.value})} />
        <label>Fecha inicio</label>
        <input type="date" value={form.fechaInicio} onChange={e => setForm({...form, fechaInicio: e.target.value})} required />
        <label>Fecha fin</label>
        <input type="date" value={form.fechaFin} onChange={e => setForm({...form, fechaFin: e.target.value})} required />
        <label>Cupo máximo</label>
        <input type="number" value={form.cupoMaximo} onChange={e => setForm({...form, cupoMaximo: e.target.value})} required />
        <button type="submit" className="btn-primary">Crear curso</button>
      </form>
    </div>
  );
}