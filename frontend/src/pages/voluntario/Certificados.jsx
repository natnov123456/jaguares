import { Link } from 'react-router-dom';
import MisCertificados from '../../components/MisCertificados';

export default function VoluntarioCertificados() {
  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Mis Certificados</h1>
          <p>Certificados obtenidos en los cursos de la fundación.</p>
        </div>
        <Link to="/voluntario" className="btn-secondary">← Volver al portal</Link>
      </div>
      <section className="panel">
        <MisCertificados />
      </section>
    </div>
  );
}
