import { ESTADO_BADGE } from '../utils/formato';

export default function Estado({ valor }) {
  return <span className={`badge ${ESTADO_BADGE[valor] ?? ''}`}>{valor?.replace('_', ' ')}</span>;
}
