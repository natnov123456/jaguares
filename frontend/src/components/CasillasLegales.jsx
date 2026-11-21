import { Link } from 'react-router-dom';
import '../pages/Legal.css';

// Dos casillas separadas y obligatorias en cada formulario que pide datos personales:
// Términos y condiciones, y Política de privacidad. El backend guarda cuándo y cómo se aceptaron.
export const SIN_ACEPTAR = { aceptaTerminos: false, aceptaPrivacidad: false };

export default function CasillasLegales({ valores, cambiar, errores = {}, prefijo = 'cl' }) {
  return (
    <div className="casillas-legales">
      <label htmlFor={`${prefijo}-terminos`}>
        <input id={`${prefijo}-terminos`} type="checkbox" required checked={valores.aceptaTerminos}
               onChange={e => cambiar({ ...valores, aceptaTerminos: e.target.checked })} />
        <span>
          He leído y acepto los <Link to="/terminos" target="_blank" rel="noopener">Términos y condiciones</Link>.
          Si soy menor de 18 años, cuento con la autorización de mi madre, padre o tutor.
        </span>
      </label>
      {errores.aceptaTerminos && <span className="error-casilla" role="alert">{errores.aceptaTerminos}</span>}
      <label htmlFor={`${prefijo}-privacidad`}>
        <input id={`${prefijo}-privacidad`} type="checkbox" required checked={valores.aceptaPrivacidad}
               onChange={e => cambiar({ ...valores, aceptaPrivacidad: e.target.checked })} />
        <span>
          He leído la <Link to="/privacidad" target="_blank" rel="noopener">Política de privacidad</Link> y
          autorizo el tratamiento de mis datos para este fin.
        </span>
      </label>
      {errores.aceptaPrivacidad && <span className="error-casilla" role="alert">{errores.aceptaPrivacidad}</span>}
    </div>
  );
}
