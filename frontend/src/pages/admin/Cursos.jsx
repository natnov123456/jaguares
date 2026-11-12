import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { cursosApi, encuestasApi, plantillasApi } from '../../services/api';
import Estado from '../../components/Estado';
import EditorEncuesta from '../../components/EditorEncuesta';
import SelectorPlantilla from '../../components/SelectorPlantilla';
import CambioActividad from '../../components/CambioActividad';
import { fecha, fechaHora } from '../../utils/formato';

export default function AdminCursos() {
  const { usuario } = useAuth();
  const [cursos, setCursos] = useState([]);
  const [plantillas, setPlantillas] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);
  const [finalizando, setFinalizando] = useState(null);
  const [cola, setCola] = useState(null); // { curso, personas } (FUJ-111)
  const [confirmando, setConfirmando] = useState(null); // curso a finalizar (FUJ-120)
  const [encuestas, setEncuestas] = useState(new Map()); // cursoId -> resumen de su encuesta
  const [cambio, setCambio] = useState(null); // { modo: 'reprogramar' | 'cancelar', curso } (FUJ-119)
  const [docentes, setDocentes] = useState([]);
  const esDirectora = usuario?.rol === 'DIRECTORA';

  const cargar = () =>
    Promise.all([cursosApi.gestion(), encuestasApi.listar()])
      .then(([cu, en]) => {
        setCursos(cu.data);
        setEncuestas(new Map(en.data.filter(x => x.tipoActividad === 'CURSO').map(x => [x.actividadId, x])));
      })
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar los cursos' }))
      .finally(() => setLoading(false));

  useEffect(() => {
    cargar();
    plantillasApi.listar()
      .then(r => setPlantillas(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar las plantillas de certificado' }));
    if (esDirectora)
      cursosApi.docentes().then(r => setDocentes(r.data))
        .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar la lista de docentes' }));
  }, [esDirectora]);

  const asignarDocente = async (c, docenteId) => {
    if (!docenteId) return;
    setMsg(null);
    try {
      await cursosApi.asignarDocente(c.id, docenteId);
      const nombre = docentes.find(d => d.id === docenteId)?.nombre;
      setCursos(lista => lista.map(x => x.id === c.id ? { ...x, docenteId, docenteNombre: nombre } : x));
      setMsg({ tipo: 'success', texto: `${nombre} quedó a cargo de "${c.titulo}" y se le avisó por email.` });
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo asignar el docente' });
    }
  };

  const asignarPlantilla = async (c, plantillaId) => {
    setMsg(null);
    try {
      await cursosApi.asignarPlantilla(c.id, plantillaId);
      setCursos(lista => lista.map(x => x.id === c.id ? { ...x, plantillaId } : x));
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo asignar la plantilla' });
    }
  };

  const verCola = async (c) => {
    setMsg(null);
    try {
      const { data } = await cursosApi.listaEspera(c.id);
      setCola({ curso: c, personas: data });
    } catch {
      setMsg({ tipo: 'error', texto: 'No se pudo cargar la lista de espera' });
    }
  };

  // FUJ-120: al confirmar se emiten los certificados y se envía la encuesta (editable) a los inscritos
  const finalizar = async (preguntas) => {
    const c = confirmando;
    setFinalizando(c.id);
    setMsg(null);
    try {
      const { data } = await cursosApi.finalizar(c.id, { preguntas });
      setMsg({
        tipo: 'success',
        texto: `Curso finalizado: ${data.certificados_generados} certificado(s) emitido(s) y ${data.encuestas_enviadas} encuesta(s) de satisfacción enviada(s).`,
      });
      setConfirmando(null);
      cargar();
    } catch (err) {
      const texto = err.response?.status === 403
        ? 'Solo puedes finalizar los cursos que te asignaron.'
        : err.response?.data?.error || 'No se pudo finalizar el curso';
      setMsg({ tipo: 'error', texto });
    } finally {
      setFinalizando(null);
    }
  };

  // FUJ-119: el backend avisa a inscritos y lista de espera; si falla, el diálogo muestra el error
  const aplicarCambio = async (datos) => {
    const { modo, curso } = cambio;
    const { data } = modo === 'reprogramar'
      ? await cursosApi.reprogramar(curso.id, datos)
      : await cursosApi.cancelar(curso.id, datos);
    setCambio(null);
    setMsg({
      tipo: 'success',
      texto: `${modo === 'reprogramar' ? 'Curso reprogramado' : 'Curso cancelado'}: "${curso.titulo}". ` +
             `Se avisó por correo a ${data.notificados} persona(s).`,
    });
    cargar();
  };

  const activos = cursos.filter(c => c.estado !== 'FINALIZADO' && c.estado !== 'CANCELADO');
  const totalInscritos = activos.reduce((n, c) => n + c.inscritos, 0);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>{esDirectora ? 'Cursos de la fundación' : 'Mis cursos'}</h1>
          <p>{esDirectora
            ? 'Todos los cursos de la fundación. Tú los creas y eliges al docente a cargo.'
            : 'Los cursos que la dirección te asignó. Entra a cada uno para registrar la asistencia y editar sus datos.'}</p>
        </div>
        <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="stat-box">
            <p>Inscritos en cursos activos</p>
            <h2>{totalInscritos}</h2>
          </div>
          {esDirectora && <Link to="/admin/crear-curso" className="btn-primary">+ Crear curso</Link>}
        </div>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <section className="panel">
        {loading ? (
          <p>Cargando...</p>
        ) : cursos.length === 0 ? (
          <p className="muted" style={{ textAlign: 'center', padding: 24 }}>
            {esDirectora
              ? <>Aún no hay cursos. <Link to="/admin/crear-curso" style={{ color: '#b45309' }}>Crea el primero →</Link></>
              : 'Todavía no tienes cursos asignados. La dirección te avisará por email cuando te asigne uno.'}
          </p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>Curso</th>{esDirectora && <th>Docente</th>}<th>Fechas</th><th>Inscritos / Cupo</th><th>En espera</th><th>Estado</th><th>Plantilla de certificado</th><th>Acciones</th></tr>
              </thead>
              <tbody>
                {cursos.map(c => (
                  <tr key={c.id}>
                    <td style={{ fontWeight: 600 }}>
                      <Link to={`/admin/cursos/${c.id}`} style={{ color: 'inherit', textDecoration: 'underline' }}>{c.titulo}</Link>
                    </td>
                    {esDirectora && (
                      <td>
                        <select value={c.docenteId ?? ''} aria-label={`Docente de ${c.titulo}`}
                                disabled={c.estado === 'FINALIZADO' || c.estado === 'CANCELADO'}
                                onChange={e => asignarDocente(c, e.target.value)}>
                          {!c.docenteId && <option value="">⚠ Sin docente</option>}
                          {c.docenteId && !docentes.some(d => d.id === c.docenteId) && (
                            <option value={c.docenteId}>{c.docenteNombre ?? 'Docente inactivo'}</option>
                          )}
                          {docentes.map(d => <option key={d.id} value={d.id}>{d.nombre}</option>)}
                        </select>
                      </td>
                    )}
                    <td>{fecha(c.fechaInicio)} – {fecha(c.fechaFin)}</td>
                    <td>{c.inscritos} / {c.cupoMaximo}</td>
                    <td>
                      {c.enEspera > 0 ? (
                        <button className="btn-secondary" style={{ padding: '4px 10px' }} onClick={() => verCola(c)}>
                          {c.enEspera} en espera
                        </button>
                      ) : <span className="muted">—</span>}
                    </td>
                    <td><Estado valor={c.estado} /></td>
                    <td>
                      <SelectorPlantilla plantillas={plantillas} value={c.plantillaId}
                                         disabled={c.estado === 'FINALIZADO' || c.estado === 'CANCELADO'}
                                         aria-label={`Plantilla de ${c.titulo}`}
                                         onChange={id => asignarPlantilla(c, id)} />
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      <Link to={`/admin/cursos/${c.id}`} className="btn-secondary" style={{ padding: '6px 12px' }}>
                        {c.estado === 'FINALIZADO' || c.estado === 'CANCELADO' ? 'Ver asistencia' : 'Asistencia y datos'}
                      </Link>
                      {c.estado === 'FINALIZADO' ? (
                        encuestas.has(c.id) ? (
                          <Link to={`/admin/encuestas/${encuestas.get(c.id).id}`} className="btn-secondary">
                            Encuesta ({encuestas.get(c.id).respuestas}/{encuestas.get(c.id).invitados})
                          </Link>
                        ) : <span className="muted">Certificados emitidos</span>
                      ) : c.estado !== 'CANCELADO' && (
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                          <button className="btn-primary" style={{ padding: '6px 12px' }}
                                  disabled={finalizando === c.id} onClick={() => setConfirmando(c)}>
                            {finalizando === c.id ? 'Emitiendo...' : 'Finalizar y certificar'}
                          </button>
                          <button className="btn-secondary" style={{ padding: '4px 10px' }}
                                  onClick={() => setCambio({ modo: 'reprogramar', curso: c })}>Reprogramar</button>
                          <button className="btn-secondary" style={{ padding: '4px 10px', color: '#991b1b', borderColor: '#fca5a5' }}
                                  onClick={() => setCambio({ modo: 'cancelar', curso: c })}>Cancelar</button>
                        </div>
                      )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {cola && (
        <div className="modal-fondo" onClick={() => setCola(null)}>
          <div className="modal" role="dialog" aria-modal="true" aria-label="Lista de espera" onClick={e => e.stopPropagation()}>
            <button className="modal-cerrar" onClick={() => setCola(null)} aria-label="Cerrar">×</button>
            <h2>Lista de espera</h2>
            <p className="muted" style={{ marginBottom: 12 }}>
              {cola.curso.titulo}. Cuando alguien cancela, la primera persona queda inscrita automáticamente y se le avisa por email.
            </p>
            {cola.personas.length === 0 ? (
              <p className="muted">La lista de espera está vacía.</p>
            ) : (
              <ol style={{ paddingLeft: 20, display: 'flex', flexDirection: 'column', gap: 6 }}>
                {cola.personas.map(p => (
                  <li key={p.posicion}>
                    <strong>{p.nombre}</strong> <span className="muted">· {p.email} · desde {fechaHora(p.anotadoEn)}</span>
                  </li>
                ))}
              </ol>
            )}
          </div>
        </div>
      )}

      {cambio && (
        <CambioActividad modo={cambio.modo} tipo="CURSO" actividad={cambio.curso}
                         onConfirmar={aplicarCambio} onCerrar={() => setCambio(null)} />
      )}

      {confirmando && (
        <EditorEncuesta
          titulo={`Finalizar: ${confirmando.titulo}`}
          aviso={`Se emitirá un certificado a los inscritos que alcancen la asistencia mínima del curso (si no se registró asistencia, a los ${confirmando.inscritos} inscritos) y se les notificará por email junto con el enlace a esta encuesta de satisfacción. Esta acción no se puede deshacer.`}
          textoConfirmar="Finalizar, certificar y enviar encuesta"
          procesando={finalizando === confirmando.id}
          onConfirmar={finalizar}
          onCerrar={() => setConfirmando(null)}
        />
      )}
    </div>
  );
}
