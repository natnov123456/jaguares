import { useState } from 'react';
import { useAuth } from '../../context/AuthContext';

export default function VoluntarioCertificados() {
  const { usuario } = useAuth();

  // Aquí a futuro conectarías con un endpoint GET /api/Certificados/mis-certificados
  // Por ahora mostramos un mensaje genérico ya que el backend aún no filtra por ID de usuario.
  
  return (
    <div className="container" style={{ padding: '48px 24px' }}>
      <h1 style={{ fontSize: '2rem', marginBottom: '24px' }}>Mis Certificados</h1>
      <div className="auth-container" style={{ margin: 0, maxWidth: '100%' }}>
        <p style={{ textAlign: 'center', color: '#6b7280', padding: '40px' }}>
          Aún no tienes certificados emitidos o la función está en desarrollo.
          <br/><br/>
          Si acabas de terminar un curso, contacta a la Directora para que emita tu certificado.
        </p>
      </div>
    </div>
  );
}