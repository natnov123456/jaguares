import { useState } from 'react';
import { donacionesApi } from '../services/api';
import { numeroComprobante } from '../utils/formato';

// FUJ-116: descarga el comprobante con la sesión del usuario (el PDF no tiene una URL pública)
export default function BotonComprobante({ donacion }) {
  const [estado, setEstado] = useState('listo');

  const descargar = async () => {
    setEstado('descargando');
    try {
      const { data } = await donacionesApi.comprobante(donacion.id);
      const url = URL.createObjectURL(data);
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = `comprobante-${numeroComprobante(donacion.numeroComprobante)}.pdf`;
      enlace.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      setEstado('listo');
    } catch {
      setEstado('error');
    }
  };

  return (
    <button type="button" className="btn-secondary" onClick={descargar} disabled={estado === 'descargando'}>
      {estado === 'descargando' ? 'Descargando...' : estado === 'error' ? 'Reintentar descarga' : 'Comprobante PDF'}
    </button>
  );
}
