import { useState } from 'react';
import { Link } from 'react-router-dom';
import { voluntariosApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { esStaff, puede } from '../config/permisos';

export default function Voluntariado() {
  const { usuario } = useAuth();
  // Si hay sesión se precarga el nombre; el email no viaja en la sesión, así que se pide igual
  const [form, setForm] = useState({ nombreCompleto: usuario?.nombreCompleto ?? '', email: '', telefono: '' });
  const [msg, setMsg] = useState(null);

  if (puede(usuario, 'verPortalVoluntario')) {
    return (
      <div className="auth-container">
        <h1>Ya eres voluntario/a 🐾</h1>
        <p>Gracias por ser parte de la fundación. Revisa tus actividades en tu portal.</p>
        <p><Link to="/voluntario" className="btn-primary">Ir a mi portal</Link></p>
      </div>
    );
  }

  if (esStaff(usuario)) {
    return (
      <div className="auth-container">
        <h1>Voluntariado</h1>
        <p>Las postulaciones de voluntarios se revisan en el panel.</p>
        {puede(usuario, 'gestionarVoluntarios') && (
          <p><Link to="/admin/voluntarios" className="btn-primary">Ver voluntarios</Link></p>
        )}
      </div>
    );
  }

  const postular = async (e) => {
    e.preventDefault();
    try {
      await voluntariosApi.postular(form);
      setMsg({ tipo: 'success', texto: '¡Postulación enviada con éxito! Te contactaremos pronto.' });
      setForm({ nombreCompleto: '', email: '', telefono: '' });
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'Error al postularse' });
    }
  };

  return (
    <div className="auth-container">
      <h1>Ser voluntario</h1>
      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}
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
