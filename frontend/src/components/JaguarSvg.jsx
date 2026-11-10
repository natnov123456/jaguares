import { useId } from 'react';

// Ilustración propia del jaguar de la fundación (vista lateral, mirando a la derecha).
// Las patas y la cola tienen clases para animarlas al caminar (ver JaguarScroll.css).
const ROSETAS = [
  [30, 22, 3.2], [40, 18, 2.8], [50, 21, 3.4], [60, 17, 2.6], [68, 23, 3],
  [36, 30, 2.6], [47, 31, 3], [58, 29, 2.8], [26, 29, 2.2], [72, 31, 2.2],
];

export default function JaguarSvg({ className, titulo = 'Jaguar' }) {
  // Id único: el jaguar puede aparecer más de una vez en la página
  const pelaje = `jag-pelaje-${useId().replace(/:/g, '')}`;
  return (
    <svg className={className} viewBox="0 0 124 62" role="img" aria-label={titulo}>
      <defs>
        <linearGradient id={pelaje} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#f2b544" />
          <stop offset="1" stopColor="#d98a1f" />
        </linearGradient>
      </defs>

      {/* Cola */}
      <path className="jag-cola" d="M20 25 C 10 24, 6 16, 4 8 C 3 5, 7 4, 8 7 C 10 14, 13 19, 21 20 Z"
            fill={`url(#${pelaje})`} stroke="#7c4a12" strokeWidth="0.8" />
      <circle cx="5.2" cy="6.4" r="2.2" fill="#2b1a0b" />

      {/* Patas traseras (detrás del cuerpo) */}
      <g className="jag-pata jag-pata-b">
        <rect x="27" y="31" width="7" height="22" rx="3.5" fill="#d08420" />
        <ellipse cx="30.5" cy="53" rx="5" ry="2.6" fill="#7c4a12" />
      </g>
      <g className="jag-pata jag-pata-d">
        <rect x="71" y="31" width="7" height="22" rx="3.5" fill="#d08420" />
        <ellipse cx="74.5" cy="53" rx="5" ry="2.6" fill="#7c4a12" />
      </g>

      {/* Cuerpo */}
      <ellipse cx="50" cy="26" rx="33" ry="13.5" fill={`url(#${pelaje})`} stroke="#7c4a12" strokeWidth="0.8" />
      <ellipse cx="50" cy="33" rx="24" ry="5.5" fill="#fbe3b0" opacity="0.85" />
      {ROSETAS.map(([x, y, r]) => (
        <g key={`${x}-${y}`}>
          <circle cx={x} cy={y} r={r} fill="none" stroke="#3b2410" strokeWidth="1.3" strokeDasharray="3 1.6" />
          <circle cx={x} cy={y} r={r * 0.35} fill="#3b2410" opacity="0.55" />
        </g>
      ))}

      {/* Patas delanteras (delante del cuerpo) */}
      <g className="jag-pata jag-pata-a">
        <rect x="37" y="32" width="7.5" height="21" rx="3.7" fill="#e9a23b" stroke="#7c4a12" strokeWidth="0.6" />
        <ellipse cx="40.7" cy="53" rx="5.2" ry="2.8" fill="#5c360d" />
      </g>
      <g className="jag-pata jag-pata-c">
        <rect x="62" y="32" width="7.5" height="21" rx="3.7" fill="#e9a23b" stroke="#7c4a12" strokeWidth="0.6" />
        <ellipse cx="65.7" cy="53" rx="5.2" ry="2.8" fill="#5c360d" />
      </g>

      {/* Cabeza */}
      <g className="jag-cabeza">
        <circle cx="84" cy="13" r="4" fill="#e9a23b" stroke="#7c4a12" strokeWidth="0.8" />
        <circle cx="84" cy="13" r="1.8" fill="#7c4a12" />
        <circle cx="95" cy="12" r="4" fill="#e9a23b" stroke="#7c4a12" strokeWidth="0.8" />
        <circle cx="95" cy="12" r="1.8" fill="#7c4a12" />
        <ellipse cx="91" cy="22" rx="13" ry="11.5" fill={`url(#${pelaje})`} stroke="#7c4a12" strokeWidth="0.8" />
        <circle cx="86" cy="17" r="1.3" fill="#3b2410" />
        <circle cx="90" cy="14.5" r="1.1" fill="#3b2410" />
        <circle cx="83" cy="23" r="1.4" fill="#3b2410" />
        <ellipse cx="100" cy="26" rx="7.5" ry="6" fill="#fbe3b0" />
        <path d="M103.5 22.5 l3.2 0.4 l-1.6 2.6 Z" fill="#3b2410" />
        <path d="M105 25.5 q-1.5 3 -4.5 2.4" fill="none" stroke="#3b2410" strokeWidth="0.9" strokeLinecap="round" />
        <ellipse className="jag-ojo" cx="96.5" cy="19" rx="2" ry="2.3" fill="#1f2937" />
        <circle cx="97.2" cy="18.2" r="0.7" fill="#fff" />
        <path d="M101 28 l7 -1 M101 29.5 l7 1" stroke="#7c4a12" strokeWidth="0.5" strokeLinecap="round" />
      </g>
    </svg>
  );
}

// Solo la cabeza, para el logo del navbar y el pie de página
export function JaguarCabeza({ className, titulo = 'Fundación Unión de los Jaguares' }) {
  return (
    <svg className={className} viewBox="0 0 40 40" role="img" aria-label={titulo}>
      <circle cx="9" cy="9" r="6" fill="#e9a23b" stroke="#7c4a12" strokeWidth="1.2" />
      <circle cx="9" cy="9" r="2.8" fill="#7c4a12" />
      <circle cx="31" cy="9" r="6" fill="#e9a23b" stroke="#7c4a12" strokeWidth="1.2" />
      <circle cx="31" cy="9" r="2.8" fill="#7c4a12" />
      <ellipse cx="20" cy="22" rx="16" ry="15" fill="#f2b544" stroke="#7c4a12" strokeWidth="1.2" />
      <circle cx="10" cy="17" r="1.6" fill="#3b2410" />
      <circle cx="30" cy="17" r="1.6" fill="#3b2410" />
      <circle cx="20" cy="11" r="1.5" fill="#3b2410" />
      <circle cx="9" cy="25" r="1.4" fill="#3b2410" />
      <circle cx="31" cy="25" r="1.4" fill="#3b2410" />
      <ellipse cx="14" cy="20" rx="2.2" ry="2.6" fill="#1f2937" />
      <ellipse cx="26" cy="20" rx="2.2" ry="2.6" fill="#1f2937" />
      <circle cx="14.7" cy="19.2" r="0.8" fill="#fff" />
      <circle cx="26.7" cy="19.2" r="0.8" fill="#fff" />
      <ellipse cx="20" cy="29" rx="8" ry="6" fill="#fbe3b0" />
      <path d="M17 25.5 h6 l-3 3 Z" fill="#3b2410" />
      <path d="M20 28.5 v1.5 M20 30 q-2.5 2 -4.5 0.5 M20 30 q2.5 2 4.5 0.5" fill="none" stroke="#3b2410"
            strokeWidth="1" strokeLinecap="round" />
    </svg>
  );
}
