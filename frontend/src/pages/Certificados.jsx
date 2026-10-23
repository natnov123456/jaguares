import { useState } from 'react';
import { certificadosApi } from '../services/api';

export default function Certificados() {
  const [codigo, setCodigo] = useState('');
  const [res, setRes] = useState(null);

  const verificar = async (e) => {
    e.preventDefault();
    try {
      const { data } = await certificadosApi.verificar(codigo);
      setRes(data);
    } catch {
      setRes({ valido: false });
    }
  };

  return (
    <div className="auth-container">
      <h1>Verificar certificado</h1>
      <form onSubmit={verificar}>
        <label>Código del certificado</label>
        <input type="text" value={codigo} onChange={e => setCodigo(e.target.value)} required placeholder="Ej: FUJ-20261022-XXXX" />
        <button type="submit" className="btn-primary">Verificar</button>
      </form>
      {res && (
        <div style={{ marginTop: 16, padding: 16, background: res.valido ? '#dcfce7' : '#fee2e2', borderRadius: 4 }}>
          {res.valido ? (
            <>
              <p><strong>✅ Certificado válido</strong></p>
              <p>Participante: {res.nombreParticipante}</p>
              <p>Curso: {res.nombreCurso}</p>
            </>
          ) : (
            <p>❌ Certificado no encontrado</p>
          )}
        </div>
      )}
    </div>
  );
}