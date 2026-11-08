import { useCallback, useEffect, useRef, useState } from 'react';
import { archivoUrl, plantillasApi } from '../../services/api';
import { TIPOS_ACTIVIDAD } from '../../config/certificados';

// FUJ-115: la Directora configura logo, firmas y texto de los certificados por tipo de actividad

const MAX_BYTES = 5 * 1024 * 1024;
const VACIA = {
  nombre: '', tipoActividad: 'TALLER', titulo: 'CERTIFICADO',
  texto: 'La Fundación Unión de los Jaguares certifica que {participante} completó satisfactoriamente la actividad {actividad}, el {fecha}.',
  firmaUnoNombre: '', firmaUnoCargo: '', firmaDosNombre: '', firmaDosCargo: '',
};
const IMAGENES = [
  { campo: 'logo', url: 'logoUrl', etiqueta: 'Logo' },
  { campo: 'firmaUno', url: 'firmaUnoUrl', etiqueta: 'Firma 1' },
  { campo: 'firmaDos', url: 'firmaDosUrl', etiqueta: 'Firma 2' },
];

const aFormulario = (p) => Object.fromEntries(Object.keys(VACIA).map(k => [k, p[k] ?? '']));

export default function PlantillasCertificado() {
  const [plantillas, setPlantillas] = useState([]);
  const [selId, setSelId] = useState(null);
  const [form, setForm] = useState(VACIA);
  const [vista, setVista] = useState(null);
  const [subiendo, setSubiendo] = useState(null);
  const [msg, setMsg] = useState(null);

  // URL del PDF en memoria y número de la última petición (una respuesta atrasada no pisa a la más nueva)
  const vistaUrl = useRef(null);
  const peticion = useRef(0);

  const seleccionada = plantillas.find(p => p.id === selId) ?? null;
  const error = (err, texto) => setMsg({ tipo: 'error', texto: err.response?.data?.error || texto });
  const campo = (k) => ({ value: form[k], onChange: e => setForm({ ...form, [k]: e.target.value }) });

  const mostrarVista = useCallback((url) => {
    if (vistaUrl.current) URL.revokeObjectURL(vistaUrl.current);
    vistaUrl.current = url;
    setVista(url);
  }, []);

  const cargarVista = useCallback(async (id) => {
    const n = ++peticion.current;
    try {
      const { data } = await plantillasApi.vistaPrevia(id);
      if (n === peticion.current) mostrarVista(URL.createObjectURL(data));
    } catch {
      if (n === peticion.current) setMsg({ tipo: 'error', texto: 'No se pudo generar la vista previa' });
    }
  }, [mostrarVista]);

  useEffect(() => {
    plantillasApi.listar()
      .then(r => setPlantillas(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar las plantillas' }));
    return () => { if (vistaUrl.current) URL.revokeObjectURL(vistaUrl.current); };
  }, []);

  const seleccionar = (p) => {
    setMsg(null);
    setSelId(p.id);
    setForm(aFormulario(p));
    cargarVista(p.id);
  };

  const nueva = () => {
    setMsg(null);
    setSelId(null);
    setForm(VACIA);
    peticion.current++;
    mostrarVista(null);
  };

  // Reemplaza la plantilla en la lista con la versión que devolvió la API
  const actualizarEnLista = (p) =>
    setPlantillas(lista => [...lista.filter(x => x.id !== p.id), p]
      .sort((a, b) => a.tipoActividad.localeCompare(b.tipoActividad) || a.nombre.localeCompare(b.nombre)));

  const guardar = async (e) => {
    e.preventDefault();
    setMsg(null);
    try {
      const { data } = selId ? await plantillasApi.actualizar(selId, form) : await plantillasApi.crear(form);
      actualizarEnLista(data);
      setSelId(data.id);
      setForm(aFormulario(data));
      cargarVista(data.id);
      setMsg({ tipo: 'success', texto: selId ? '✅ Plantilla guardada' : '✅ Plantilla creada. Ahora puedes subir el logo y las firmas.' });
    } catch (err) { error(err, 'No se pudo guardar la plantilla'); }
  };

  const eliminar = async () => {
    if (!window.confirm(`¿Eliminar la plantilla "${seleccionada.nombre}"? Los certificados ya emitidos no cambian.`)) return;
    try {
      await plantillasApi.eliminar(selId);
      setPlantillas(lista => lista.filter(p => p.id !== selId));
      nueva();
      setMsg({ tipo: 'success', texto: 'Plantilla eliminada' });
    } catch (err) { error(err, 'No se pudo eliminar la plantilla'); }
  };

  const subir = async (e, img) => {
    const archivo = e.target.files[0];
    e.target.value = '';
    if (!archivo) return;
    setMsg(null);
    if (archivo.size > MAX_BYTES) {
      setMsg({ tipo: 'error', texto: `Pesa ${(archivo.size / 1024 / 1024).toFixed(1)} MB y el máximo es 5 MB` });
      return;
    }
    setSubiendo(img.campo);
    try {
      const { data } = await plantillasApi.subirImagen(selId, img.campo, archivo);
      actualizarEnLista(data);
      cargarVista(data.id);
      setMsg({ tipo: 'success', texto: `✅ ${img.etiqueta} actualizado` });
    } catch (err) { error(err, 'No se pudo subir la imagen'); }
    finally { setSubiendo(null); }
  };

  const quitar = async (img) => {
    setMsg(null);
    try {
      const { data } = await plantillasApi.quitarImagen(selId, img.campo);
      actualizarEnLista(data);
      cargarVista(data.id);
    } catch (err) { error(err, 'No se pudo quitar la imagen'); }
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Plantillas de certificado</h1>
          <p>Define el logo, las firmas y el texto por tipo de actividad. Se asignan a cada curso desde "Mis cursos".</p>
        </div>
        <button className="btn-primary" onClick={nueva}>+ Nueva plantilla</button>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <div className="split">
        <div>
          <section className="panel">
            <h2>{selId ? 'Editar plantilla' : 'Nueva plantilla'}</h2>
            <form onSubmit={guardar} className="form-stack">
              <div>
                <label htmlFor="p-nombre">Nombre</label>
                <input id="p-nombre" maxLength={120} required {...campo('nombre')} />
              </div>
              <div>
                <label htmlFor="p-tipo">Tipo de actividad</label>
                <select id="p-tipo" {...campo('tipoActividad')}>
                  {Object.entries(TIPOS_ACTIVIDAD).map(([v, t]) => <option key={v} value={v}>{t}</option>)}
                </select>
              </div>
              <div>
                <label htmlFor="p-titulo">Título del certificado</label>
                <input id="p-titulo" maxLength={120} required {...campo('titulo')} />
              </div>
              <div>
                <label htmlFor="p-texto">Texto</label>
                <textarea id="p-texto" rows="4" maxLength={1000} required {...campo('texto')} />
                <small className="muted">
                  Variables: <code>{'{participante}'}</code> (obligatoria), <code>{'{actividad}'}</code>, <code>{'{fecha}'}</code> y <code>{'{codigo}'}</code>.
                </small>
              </div>
              <div>
                <label htmlFor="p-f1n">Firma 1: nombre y cargo</label>
                <input id="p-f1n" maxLength={120} placeholder="Nombre" {...campo('firmaUnoNombre')} />
                <input maxLength={120} placeholder="Cargo" aria-label="Cargo de la firma 1" style={{ marginTop: 6 }} {...campo('firmaUnoCargo')} />
              </div>
              <div>
                <label htmlFor="p-f2n">Firma 2: nombre y cargo (opcional)</label>
                <input id="p-f2n" maxLength={120} placeholder="Nombre" {...campo('firmaDosNombre')} />
                <input maxLength={120} placeholder="Cargo" aria-label="Cargo de la firma 2" style={{ marginTop: 6 }} {...campo('firmaDosCargo')} />
              </div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                <button type="submit" className="btn-primary">Guardar</button>
                {selId && <button type="button" className="btn-danger" onClick={eliminar}>Eliminar</button>}
              </div>
            </form>
          </section>

          {seleccionada && (
            <section className="panel">
              <h2>Logo y firmas</h2>
              <div className="form-stack">
                {IMAGENES.map(img => (
                  <div key={img.campo}>
                    <label htmlFor={`p-${img.campo}`}>{img.etiqueta} (JPG o PNG, máximo 5 MB)</label>
                    {seleccionada[img.url] && (
                      <div style={{ display: 'flex', alignItems: 'center', gap: 12, margin: '6px 0' }}>
                        <img src={archivoUrl(seleccionada[img.url])} alt={img.etiqueta} style={{ maxHeight: 56, maxWidth: 160 }} />
                        <button type="button" className="btn-secondary" onClick={() => quitar(img)}>Quitar</button>
                      </div>
                    )}
                    <input id={`p-${img.campo}`} type="file" accept="image/jpeg,image/png"
                           disabled={subiendo !== null} onChange={e => subir(e, img)} />
                  </div>
                ))}
              </div>
            </section>
          )}

          {vista && (
            <section className="panel">
              <h2>Vista previa</h2>
              <iframe src={vista} title="Vista previa del certificado" style={{ width: '100%', height: 480, border: '1px solid #e5e7eb' }} />
              <p className="muted" style={{ marginTop: 8 }}>
                Muestra la última versión guardada con datos de ejemplo. <a href={vista} target="_blank" rel="noreferrer">Abrir en otra pestaña</a>
              </p>
            </section>
          )}
        </div>

        <div>
          <section className="panel">
            <h2>Plantillas</h2>
            {plantillas.length === 0 ? <p className="muted">Todavía no hay plantillas.</p> : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {plantillas.map(p => (
                  <button key={p.id} className={p.id === selId ? 'btn-primary' : 'btn-secondary'}
                          style={{ textAlign: 'left' }} onClick={() => seleccionar(p)}>
                    <strong>{p.nombre}</strong><br /><small>{TIPOS_ACTIVIDAD[p.tipoActividad]}</small>
                  </button>
                ))}
              </div>
            )}
          </section>
        </div>
      </div>
    </div>
  );
}
