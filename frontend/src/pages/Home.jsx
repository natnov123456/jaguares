import { Link } from 'react-router-dom';
import JaguarSvg from '../components/JaguarSvg';
import './Home.css';

export default function Home() {
  return (
    <div className="home-wrapper">
      {/* Hero Section */}
      <section className="hero-jaguar">
        <div className="hero-overlay">
          <div className="hero-rosetas fondo-rosetas" aria-hidden="true" />
          <div className="container hero-content">
            <span className="hero-etiqueta">🐾 La Paz, Bolivia</span>
            <h1>Protegiendo la vida,<br /><span className="hero-resaltado">forjando el futuro.</span></h1>
            <p>Somos la Fundación Unión de los Jaguares. Trabajamos en La Paz por la conservación, la educación y el desarrollo comunitario.</p>
            <div className="hero-actions">
              <Link to="/donaciones" className="btn-primary-large">Donar Ahora</Link>
              <Link to="/cursos" className="btn-secondary-large">Ver Cursos</Link>
            </div>
          </div>
          <a href="#mision" className="hero-bajar" aria-label="Bajar a nuestra misión">
            <span />
          </a>
        </div>
      </section>

      {/* Misión / Información */}
      <section id="mision" className="mission-section container">
        <div className="mission-text revelar">
          <span className="seccion-etiqueta">Quiénes somos</span>
          <h2>Nuestra Misión</h2>
          <p>Nacimos con el espíritu del jaguar: fuerte, ágil y protector de su territorio. Buscamos empoderar a las comunidades de La Paz a través de talleres, capacitaciones y voluntariado activo.</p>
          <Link to="/voluntariado" className="link-voluntario">Únete como voluntario →</Link>
          <br />
          <Link to="/quienes-somos" className="link-voluntario">Conoce quiénes somos y escríbenos →</Link>
        </div>
        <div className="mission-stats">
          <div className="stat-card revelar">
            <h3>+500</h3>
            <p>Personas capacitadas</p>
          </div>
          <div className="stat-card revelar revelar-2">
            <h3>+50</h3>
            <p>Eventos realizados</p>
          </div>
          <div className="stat-card revelar revelar-3">
            <h3>+100</h3>
            <p>Voluntarios activos</p>
          </div>
          <div className="stat-card stat-card-jaguar revelar revelar-3" aria-hidden="true">
            <JaguarSvg className="stat-jaguar" titulo="" />
          </div>
        </div>
      </section>

      {/* Servicios */}
      <section className="services-section">
        <div className="container">
          <div className="revelar" style={{ textAlign: 'center' }}>
            <span className="seccion-etiqueta">Para ti</span>
            <h2>¿Qué puedes hacer aquí?</h2>
          </div>
          <div className="services-grid">
            <div className="service-card revelar">
              <div className="icon">📚</div>
              <h3>Aprende con nosotros</h3>
              <p>Inscríbete a nuestros talleres y capacitaciones.</p>
              <Link to="/cursos" className="service-link">Ver cursos disponibles</Link>
            </div>
            <div className="service-card revelar revelar-2">
              <div className="icon">🎓</div>
              <h3>Valida tus logros</h3>
              <p>Verifica la autenticidad de un certificado emitido.</p>
              <Link to="/certificados" className="service-link">Verificar certificado</Link>
            </div>
            <div className="service-card revelar revelar-3">
              <div className="icon">❤️</div>
              <h3>Apoya la causa</h3>
              <p>Tu donación nos ayuda a seguir transformando vidas.</p>
              <Link to="/donaciones" className="service-link">Hacer una donación</Link>
            </div>
          </div>
        </div>
      </section>

      {/* Llamado final */}
      <section className="container">
        <div className="cta-jaguar revelar">
          <div>
            <h2>Sé parte de la manada</h2>
            <p>Revisa las próximas actividades y súmate como participante o voluntario/a.</p>
          </div>
          <div className="cta-acciones">
            <Link to="/calendario" className="btn-primary-large">Ver calendario</Link>
            <Link to="/voluntariado" className="btn-secondary-large">Ser voluntario/a</Link>
          </div>
        </div>
      </section>
    </div>
  );
}
