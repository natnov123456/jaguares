import { useState } from 'react';
import { voluntariosApi } from '../services/api';

export default function Voluntariado() {
  const [form, setForm] = useState({ nombreCompleto: '', email: '', telefono: '' });
  const [msg, setMsg] = useState('');

  const postular = async (e) => {
    e.preventDefault();
    try {
      await voluntariosApi.postular(form);
      setMsg('¡Postulación enviada con éxito!');
      setForm({ nombreCompleto: '', email: '', telefono: '' });
    } catch (err) {
      setMsg(err.response?.data?.error || 'Error al postularse');
    }
  };

  return (
    <div className="auth-container">
      <h1>Ser voluntario</h1>
      {msg && <div className="success-msg">{msg}</div>}
      <form onSubmit={postular}>
        <label>Nombre completo</label>
        <input type="text" value={form.nombreCompleto} onChange={e => setForm({...form, nombreCompleto: e.target.value})} required />
        <label>Email</label>
        <input type="email" value={form.email} onChange={e => setForm({...form, email: e.target.value})} required />
        <label>Teléfono</label>
        <input type="tel" value={form.telefono} onChange={e => setForm({...form, telefono: e.target.value})} />
        <button type="submit" className="btn-primary">Postularme</button>
      </form>
    </div>
  );
}