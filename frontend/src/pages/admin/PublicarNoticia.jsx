import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { contenidoApi } from '../../services/api';

export default function PublicarNoticia() {
  const [form, setForm] = useState({ titulo: '', resumen: '', contenido: '', imagenUrl: '', publicada: true });
  const [msg, setMsg] = useState('');
  const navigate = useNavigate();

  const publicar = async (e) => {
    e.preventDefault();
    try {
      await contenidoApi.crearNoticia(form);
      setMsg('Noticia publicada ✅');
      setTimeout(() => navigate('/noticias'), 1200);
    } catch (err) {
      setMsg(err.response?.data?.error || 'Error');
    }
  };

  return (
    <div className="auth-container">
      <h1>Publicar noticia</h1>
      {msg && <div className="success-msg">{msg}</div>}
      <form onSubmit={publicar}>
        <label>Título</label>
        <input type="text" value={form.titulo} onChange={e => setForm({...form, titulo: e.target.value})} required />
        <label>Resumen</label>
        <input type="text" value={form.resumen} onChange={e => setForm({...form, resumen: e.target.value})} />
        <label>Contenido</label>
        <textarea value={form.contenido} onChange={e => setForm({...form, contenido: e.target.value})} required rows="4" />
        <label>URL de la Imagen (Opcional)</label>
        <input type="url" value={form.imagenUrl} onChange={e => setForm({...form, imagenUrl: e.target.value})} placeholder="https://..." />
        <button type="submit" className="btn-primary">Publicar</button>
      </form>
    </div>
  );
}