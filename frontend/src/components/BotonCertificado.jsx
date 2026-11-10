import { useState } from 'react';
import { certificadosApi } from '../services/api';

// FUJ-112: descarga el PDF del certificado con la sesión del usuario.
// El backend lo genera en el momento si el certificado es anterior a las plantillas (FUJ-115).
export default function BotonCertificado({ id, codigo, className = 'btn-primary' }) {
  const [estado, setEstado] = useState('listo');

  const descargar = async () => {
    setEstado('descargando');
    try {
      const { data } = await certificadosApi.pdf(id);
      const url = URL.createObjectURL(data);
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = `certificado-${codigo}.pdf`;
      enlace.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      setEstado('listo');
    } catch {
      setEstado('error');
    }
  };

  return (
    <button type="button" className={className} onClick={descargar} disabled={estado === 'descargando'}>
      {estado === 'descargando' ? 'Descargando...' : estado === 'error' ? 'Reintentar descarga' : 'Descargar PDF'}
    </button>
  );
}
