import { Link } from 'react-router-dom';
import './Home.css';

export default function Home() {
  return (
    <div className="home-wrapper">
      {/* Hero Section */}
      <section className="hero-jaguar">
        <div className="hero-overlay">
          <div className="container hero-content">
            <h1>Protegiendo la vida,<br />forjando el futuro.</h1>
            <p>Somos la Fundación Unión de los Jaguares. Trabajamos en La Paz por la conservación, la educación y el desarrollo comunitario.</p>
            <div className="hero-actions">
              <Link to="/donaciones" className="btn-primary-large">Donar Ahora</Link>
              <Link to="/cursos" className="btn-secondary-large">Ver Cursos</Link>
            </div>
          </div>
        </div>
      </section>

      {/* Misión / Información */}
      <section className="mission-section container">
        <div className="mission-text">
          <h2>Nuestra Misión</h2>
          <p>Nacimos con el espíritu del jaguar: fuerte, ágil y protector de su territorio. Buscamos empoderar a las comunidades de La Paz a través de talleres, capacitaciones y voluntariado activo.</p>
          <Link to="/voluntariado" className="link-voluntario">Únete como voluntario →</Link>
        </div>
        <div className="mission-stats">
          <div className="stat-card">
            <h3>+500</h3>
            <p>Personas capacitadas</p>
          </div>
          <div className="stat-card">
            <h3>+50</h3>
            <p>Eventos realizados</p>
          </div>
          <div className="stat-card">
            <h3>+100</h3>
            <p>Voluntarios activos</p>
          </div>
        </div>
      </section>

      {/* Servicios */}
      <section className="services-section">
        <div className="container">
          <h2>¿Qué puedes hacer aquí?</h2>
          <div className="services-grid">
            <div className="service-card">
              <div className="icon">📚</div>
              <h3>Aprende con nosotros</h3>
              <p>Inscríbete a nuestros talleres y capacitaciones.</p>
              <Link to="/cursos" className="service-link">Ver cursos disponibles</Link>
            </div>
            <div className="service-card">
              <div className="icon">🎓</div>
              <h3>Valida tus logros</h3>
              <p>Verifica la autenticidad de un certificado emitido.</p>
              <Link to="/certificados" className="service-link">Verificar certificado</Link>
            </div>
            <div className="service-card">
              <div className="icon">❤️</div>
              <h3>Apoya la causa</h3>
              <p>Tu donación nos ayuda a seguir transformando vidas.</p>
              <Link to="/donaciones" className="service-link">Hacer una donación</Link>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}