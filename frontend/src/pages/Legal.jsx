import { Link } from 'react-router-dom';
import './Legal.css';

// Documentos legales del sitio. La versión vigente coincide con DocumentosLegales.Version del backend:
// es la que queda registrada cada vez que alguien acepta los Términos o la Política de privacidad.
export const VERSION_LEGAL = '2026-11-20';
const VIGENTE = '20 de noviembre de 2026';

const FUNDACION = 'Fundación Unión de los Jaguares';
const CONTACTO = 'contacto@jaguares.org';

const DOCUMENTOS = {
  terminos: {
    titulo: 'Términos y condiciones de uso',
    secciones: [
      ['1. Quiénes somos', <>Este sitio pertenece a la {FUNDACION}, organización sin fines de lucro con domicilio en La Paz, Bolivia. Al usar el sitio o crear una cuenta aceptas estos términos. Si no estás de acuerdo, no uses los servicios que requieren registro.</>],
      ['2. Cuentas', <>
        <p>Para inscribirte en cursos y eventos necesitas una cuenta con datos verdaderos. Tu contraseña es personal: nadie de la Fundación te la pedirá ni puede cambiarla. Si la olvidas, usa <Link to="/olvide-contrasena">¿Olvidaste tu contraseña?</Link>.</p>
        <p>Las personas menores de 18 años deben registrarse con la autorización y el acompañamiento de su madre, padre o tutor, que acepta estos términos en su nombre.</p>
        <p>La Fundación puede desactivar una cuenta que se use para fines distintos a los de la plataforma, que suplante a otra persona o que dañe el servicio.</p>
      </>],
      ['3. Cursos, eventos y certificados', <>Las inscripciones dependen de los cupos disponibles y solo se admiten en cursos que empiezan ese día o más adelante. Los certificados se emiten a quien cumple la asistencia mínima de cada curso y pueden verificarse públicamente con su código. La Fundación puede reprogramar o cancelar una actividad; en ese caso avisa por correo a las personas inscritas.</>],
      ['4. Donaciones', <>Las donaciones son voluntarias y se destinan a los fines de la Fundación. Se emite un comprobante por cada donación. Los casos en los que se puede pedir la devolución están en la <Link to="/reembolsos">Política de reembolsos</Link>. La Fundación publica cada mes el destino de los fondos en <Link to="/transparencia">Transparencia</Link>, sin datos de los donantes.</>],
      ['5. Voluntariado', <>Postularte como voluntario o voluntaria no genera relación laboral. Las horas que registra la coordinación sirven para tu historial y tus constancias.</>],
      ['6. Uso correcto', <>No está permitido usar el sitio para enviar publicidad no solicitada, intentar acceder a cuentas ajenas, alterar el funcionamiento de la plataforma ni publicar contenido ofensivo o ilegal. Cada acción en la plataforma queda registrada (fecha, hora, cuenta e IP) para proteger a las personas y a la Fundación.</>],
      ['7. Contenido e imágenes', <>Los textos, el logotipo y el diseño son de la Fundación. Las fotos de la galería son de actividades de la Fundación y se publican con autorización de las personas que aparecen; si aparece una niña, un niño o un adolescente, con autorización de su madre, padre o tutor. Si apareces en una foto y quieres que se retire, escríbenos a {CONTACTO} y la quitaremos.</>],
      ['8. Responsabilidad', <>La Fundación cuida que la información publicada sea correcta y que la plataforma esté disponible, pero puede haber interrupciones por mantenimiento o causas ajenas. Los videos insertados (YouTube, Vimeo) pertenecen a sus plataformas.</>],
      ['9. Cambios y ley aplicable', <>Si estos términos cambian, publicaremos la nueva versión con su fecha y la pediremos aceptar en el siguiente formulario que completes. Se aplican las leyes del Estado Plurinacional de Bolivia; cualquier controversia se resolverá ante las autoridades competentes de la ciudad de La Paz.</>],
    ],
  },
  privacidad: {
    titulo: 'Política de privacidad y tratamiento de datos personales',
    secciones: [
      ['1. Responsable', <>La {FUNDACION} (La Paz, Bolivia) es responsable de los datos personales que se recogen en este sitio. Para cualquier consulta sobre tus datos escribe a <a href={`mailto:${CONTACTO}`}>{CONTACTO}</a>.</>],
      ['2. Marco legal', <>Tratamos tus datos conforme a la Constitución Política del Estado (derecho a la privacidad, intimidad, honra y propia imagen, art. 21; y Acción de Protección de Privacidad, arts. 130 y 131), la Ley N° 164 General de Telecomunicaciones y Tecnologías de Información y Comunicación y su reglamento (D.S. N° 1793) en lo referido a datos personales y comunicaciones electrónicas, y el Código Niña, Niño y Adolescente (Ley N° 548) cuando se trata de personas menores de edad.</>],
      ['3. Qué datos pedimos y para qué', <>
        <p>Solo pedimos los datos necesarios para cada servicio:</p>
        <table>
          <thead><tr><th>Formulario</th><th>Datos</th><th>Para qué</th></tr></thead>
          <tbody>
            <tr><td>Crear cuenta</td><td>Nombre, correo, contraseña (guardada cifrada) y teléfono opcional</td><td>Identificarte, inscribirte, emitir certificados y avisarte de tus actividades</td></tr>
            <tr><td>Voluntariado</td><td>Nombre, correo y teléfono opcional</td><td>Contactarte, asignarte actividades y llevar tu historial de horas</td></tr>
            <tr><td>Donación en línea</td><td>Nombre y correo</td><td>Emitir y enviarte el comprobante de tu donación</td></tr>
            <tr><td>Contacto</td><td>Nombre, correo y tu mensaje</td><td>Responder tu consulta</td></tr>
          </tbody>
        </table>
        <p>No pedimos carnet de identidad, fecha de nacimiento ni dirección. No vendemos ni cedemos tus datos a terceros ni los usamos para publicidad.</p>
      </>],
      ['4. Registro de actividad y seguridad', <>Por seguridad, la plataforma registra cada acción: fecha y hora, cuenta, dirección IP, navegador y la acción realizada (por ejemplo, iniciar sesión o inscribirse). No se registran contraseñas ni el contenido de los formularios. Este registro solo lo consulta la Dirección y no se puede modificar. Las contraseñas se guardan cifradas y la conexión al sitio es segura (HTTPS).</>],
      ['5. Constancia de tu aceptación', <>Cuando marcas las casillas de un formulario guardamos la fecha y hora, el formulario, la versión del documento aceptado, tu IP y tu navegador, para poder demostrar cuándo y cómo diste tu consentimiento.</>],
      ['6. Con quién se comparten', <>Solo con proveedores necesarios para el servicio: el servidor donde se aloja la plataforma, el servicio de correo con el que te enviamos avisos y, si donas en línea, la pasarela de pago. Las cifras de transparencia se publican siempre sin nombres ni correos de donantes.</>],
      ['7. Cuánto tiempo se guardan', <>Los datos de tu cuenta, mientras la cuenta exista. Los registros de donaciones y su bitácora, el tiempo que exigen las normas contables y tributarias. El registro de actividad, hasta 2 años. Las consultas de contacto, 1 año.</>],
      ['8. Tus derechos', <>Puedes pedir acceso a tus datos, su corrección, su eliminación (salvo los que debamos conservar por ley, como las donaciones) o retirar tu consentimiento escribiendo a <a href={`mailto:${CONTACTO}`}>{CONTACTO}</a>. Respondemos en un plazo máximo de 15 días hábiles. Además, la Constitución te reconoce la Acción de Protección de Privacidad ante un juez.</>],
      ['9. Niñas, niños y adolescentes', <>Las cuentas de menores de 18 años deben crearse con autorización de su madre, padre o tutor. Las fotos en las que aparezcan se publican solo con esa autorización.</>],
      ['10. Cambios', <>Si esta política cambia, publicaremos la nueva versión con su fecha. Versión vigente: {VIGENTE}.</>],
    ],
  },
  cookies: {
    titulo: 'Política de cookies',
    secciones: [
      ['1. ¿Usamos cookies?', <>Este sitio <strong>no usa cookies propias de publicidad, analítica ni seguimiento</strong>. Por eso no te mostramos un aviso para aceptar cookies.</>],
      ['2. Lo que guardamos en tu navegador', <>Cuando inicias sesión, guardamos en el almacenamiento local de tu navegador (<em>localStorage</em>) un identificador de sesión y tu nombre, solo para mantenerte conectado. Es estrictamente necesario para que funcione tu cuenta y se borra al cerrar sesión.</>],
      ['3. Contenido de terceros', <>Los videos de la galería se insertan desde YouTube en su modo de privacidad mejorada (<em>youtube-nocookie.com</em>) o desde Vimeo con la opción de no seguimiento. Esos servicios solo se cargan cuando abres un video y se rigen por sus propias políticas.</>],
      ['4. Cómo borrar estos datos', <>Puedes cerrar sesión o borrar los datos del sitio desde la configuración de tu navegador en cualquier momento.</>],
      ['5. Cambios', <>Si en el futuro usamos cookies que no sean estrictamente necesarias, te pediremos tu consentimiento antes de activarlas y actualizaremos esta política. Versión vigente: {VIGENTE}.</>],
    ],
  },
  reembolsos: {
    titulo: 'Política de reembolsos de donaciones',
    secciones: [
      ['1. Las donaciones son voluntarias', <>Las donaciones a la {FUNDACION} son aportes voluntarios y, una vez recibidas, se destinan a sus programas. Por regla general no se devuelven.</>],
      ['2. Cuándo puedes pedir la devolución', <>
        <ul>
          <li>Se cobró dos veces la misma donación.</li>
          <li>El monto cobrado es distinto al que elegiste.</li>
          <li>Se usó tu tarjeta o cuenta sin tu autorización.</li>
          <li>Hubo un error técnico de la plataforma o de la pasarela de pago.</li>
        </ul>
      </>],
      ['3. Cómo pedirla', <>Escribe a <a href={`mailto:${CONTACTO}`}>{CONTACTO}</a> dentro de los <strong>30 días</strong> siguientes a la donación, con el número de comprobante, la fecha, el monto y el motivo. Revisamos el pedido en un máximo de 10 días hábiles y te respondemos por correo.</>],
      ['4. Cómo se devuelve', <>Si corresponde, se devuelve por el mismo medio de pago (o por transferencia si no es posible) en un plazo de hasta 15 días hábiles. La donación queda anulada en la bitácora de auditoría y su comprobante deja de ser válido.</>],
      ['5. Campañas', <>Si una campaña no alcanza su meta, lo recaudado se destina a la misma finalidad o a otro programa similar, y se informa en Transparencia. Versión vigente: {VIGENTE}.</>],
    ],
  },
};

export default function Legal({ documento }) {
  const doc = DOCUMENTOS[documento];
  return (
    <div className="container page legal">
      <h1>{doc.titulo}</h1>
      <p className="muted">Versión vigente: {VIGENTE} · {FUNDACION}</p>
      {doc.secciones.map(([titulo, contenido]) => (
        <section key={titulo}>
          <h2>{titulo}</h2>
          {typeof contenido === 'string' ? <p>{contenido}</p> : <div>{contenido}</div>}
        </section>
      ))}
      <nav className="legal-otros" aria-label="Otros documentos legales">
        {Object.entries(DOCUMENTOS).filter(([k]) => k !== documento).map(([k, d]) => (
          <Link key={k} to={`/${k}`}>{d.titulo}</Link>
        ))}
      </nav>
    </div>
  );
}
