import { useState, useEffect } from 'react';
import { donacionesApi } from '../../services/api';

export default function AdminDonaciones() {
  const [donaciones, setDonaciones] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  
  // Estado para el formulario manual (por si entra efectivo a la oficina)
  const [monto, setMonto] = useState('');
  const [referencia, setReferencia] = useState('');
  const [mensaje, setMensaje] = useState('');

  const fechaActual = new Date();
  const anio = fechaActual.getFullYear();
  const mes = fechaActual.getMonth() + 1; // Enero es 0

  const cargarDonaciones = async () => {
    try {
      // Llamamos al endpoint de reporte que ya teníamos
      const { data } = await donacionesApi.reporte(anio, mes);
      setDonaciones(data.detalle || []);
      setTotal(data.total || 0);
    } catch (error) {
      console.error("Error al cargar donaciones:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    cargarDonaciones();
  }, []);

  const registrarDonacionManual = async (e) => {
    e.preventDefault();
    setMensaje('');
    try {
      await donacionesApi.registrar({
        monto: parseFloat(monto),
        medio: "EFECTIVO",
        referencia: referencia || "RECIBO-OFICINA",
        donanteId: null
      });
      setMensaje('✅ Donación registrada correctamente');
      setMonto('');
      setReferencia('');
      cargarDonaciones(); // Recargar la tabla
    } catch (error) {
      setMensaje('❌ Error al registrar la donación');
    }
  };

  return (
    <div className="container" style={{ padding: '48px 24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '32px' }}>
        <div>
          <h1 style={{ fontSize: '2rem', color: '#111827' }}>Gestión de Donaciones</h1>
          <p style={{ color: '#6b7280' }}>Resumen del mes actual ({mes}/{anio})</p>
        </div>
        <div style={{ background: '#fef3c7', padding: '16px 24px', borderRadius: '8px', borderLeft: '4px solid #f59e0b' }}>
          <p style={{ fontSize: '0.9rem', color: '#92400e', margin: 0 }}>Total Recaudado</p>
          <h2 style={{ fontSize: '2rem', color: '#b45309', margin: 0 }}>Bs. {total}</h2>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '32px' }}>
        
        {/* Tabla de Donaciones */}
        <div className="card" style={{ padding: '24px' }}>
          <h2 style={{ fontSize: '1.2rem', marginBottom: '16px' }}>Historial del Mes</h2>
          {loading ? (
            <p>Cargando...</p>
          ) : donaciones.length === 0 ? (
            <p style={{ color: '#6b7280', textAlign: 'center', padding: '24px' }}>No hay donaciones registradas este mes.</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid #e5e7eb', textAlign: 'left' }}>
                  <th style={{ padding: '8px' }}>Fecha</th>
                  <th style={{ padding: '8px' }}>Monto</th>
                  <th style={{ padding: '8px' }}>Medio</th>
                  <th style={{ padding: '8px' }}>Ref.</th>
                </tr>
              </thead>
              <tbody>
                {donaciones.map(d => (
                  <tr key={d.id} style={{ borderBottom: '1px solid #e5e7eb' }}>
                    <td style={{ padding: '12px 8px' }}>
                      {new Date(d.fecha).toLocaleDateString('es-BO')} {new Date(d.fecha).toLocaleTimeString('es-BO', {hour: '2-digit', minute:'2-digit'})}
                    </td>
                    <td style={{ padding: '12px 8px', fontWeight: 'bold' }}>Bs. {d.monto}</td>
                    <td style={{ padding: '12px 8px' }}>{d.medio}</td>
                    <td style={{ padding: '12px 8px', color: '#6b7280' }}>{d.referencia || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Formulario para registrar donación presencial */}
        <div className="card" style={{ padding: '24px', alignSelf: 'start' }}>
          <h2 style={{ fontSize: '1.2rem', marginBottom: '16px' }}>Registrar Efectivo</h2>
          <p style={{ fontSize: '0.9rem', color: '#6b7280', marginBottom: '16px' }}>
            Usa este formulario si alguien te entrega dinero en la oficina.
          </p>
          {mensaje && <div style={{ marginBottom: '16px', padding: '8px', background: mensaje.includes('✅') ? '#dcfce7' : '#fee2e2', borderRadius: '4px', fontSize: '0.9rem' }}>{mensaje}</div>}
          
          <form onSubmit={registrarDonacionManual} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            <div>
              <label style={{ fontSize: '0.9rem', fontWeight: 'bold' }}>Monto (Bs.)</label>
              <input 
                type="number" 
                min="1" 
                step="0.01" 
                value={monto} 
                onChange={e => setMonto(e.target.value)} 
                required 
                style={{ width: '100%', padding: '8px', border: '1px solid #d1d5db', borderRadius: '4px' }}
              />
            </div>
            <div>
              <label style={{ fontSize: '0.9rem', fontWeight: 'bold' }}>Referencia / Recibo</label>
              <input 
                type="text" 
                value={referencia} 
                onChange={e => setReferencia(e.target.value)} 
                placeholder="Ej: RECIBO-001"
                style={{ width: '100%', padding: '8px', border: '1px solid #d1d5db', borderRadius: '4px' }}
              />
            </div>
            <button type="submit" className="btn-primary" style={{ marginTop: '8px' }}>
              Guardar Donación
            </button>
          </form>
        </div>

      </div>
    </div>
  );
}