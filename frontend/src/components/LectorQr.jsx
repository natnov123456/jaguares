import { useEffect, useRef, useState } from 'react';
import jsQR from 'jsqr';

// Lector de códigos QR con la cámara trasera del celular (FUJ-113).
// Llama a onDetectar(texto) cada vez que reconoce un QR; ignora el mismo código durante 3 s
// para no registrar dos veces mientras sigue frente a la cámara.
export default function LectorQr({ onDetectar, pausado = false }) {
  const videoRef = useRef(null);
  const canvasRef = useRef(null);
  const ultimoRef = useRef({ texto: null, en: 0 });
  const pausadoRef = useRef(pausado);
  const onDetectarRef = useRef(onDetectar);
  // La cámara solo está disponible en HTTPS o en localhost
  const [error, setError] = useState(() => (navigator.mediaDevices?.getUserMedia
    ? null
    : 'Este navegador no permite usar la cámara aquí (se requiere HTTPS). Ingresa el código manualmente.'));

  useEffect(() => { pausadoRef.current = pausado; }, [pausado]);
  useEffect(() => { onDetectarRef.current = onDetectar; }, [onDetectar]);

  useEffect(() => {
    let stream = null;
    let timer = null;
    let activo = true;

    if (!navigator.mediaDevices?.getUserMedia) return undefined;

    const escanear = () => {
      const video = videoRef.current;
      const canvas = canvasRef.current;
      if (activo && video && canvas && video.readyState === video.HAVE_ENOUGH_DATA && !pausadoRef.current) {
        // Se reduce el cuadro a 480 px de ancho: suficiente para leer el QR y mucho más rápido en celular
        const escala = Math.min(1, 480 / video.videoWidth);
        canvas.width = Math.round(video.videoWidth * escala);
        canvas.height = Math.round(video.videoHeight * escala);
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
        const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const qr = jsQR(img.data, img.width, img.height, { inversionAttempts: 'dontInvert' });
        if (qr?.data) {
          const ahora = Date.now();
          const { texto, en } = ultimoRef.current;
          if (qr.data !== texto || ahora - en > 3000) {
            ultimoRef.current = { texto: qr.data, en: ahora };
            onDetectarRef.current(qr.data);
          }
        }
      }
      if (activo) timer = setTimeout(escanear, 150);
    };

    navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false })
      .then(s => {
        if (!activo) { s.getTracks().forEach(t => t.stop()); return; }
        stream = s;
        const video = videoRef.current;
        video.srcObject = s;
        video.setAttribute('playsinline', 'true'); // iOS: no abrir el video a pantalla completa
        return video.play().then(escanear);
      })
      .catch(err => {
        setError(err?.name === 'NotAllowedError'
          ? 'Permiso de cámara denegado. Habilítalo en el navegador o ingresa el código manualmente.'
          : 'No se pudo acceder a la cámara. Ingresa el código manualmente.');
      });

    return () => {
      activo = false;
      clearTimeout(timer);
      stream?.getTracks().forEach(t => t.stop());
    };
  }, []);

  if (error) return <div className="info-msg">{error}</div>;

  return (
    <div className="lector-qr">
      <video ref={videoRef} muted playsInline />
      <div className="lector-qr-marco" aria-hidden="true" />
      <canvas ref={canvasRef} hidden />
    </div>
  );
}
