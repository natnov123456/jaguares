import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { usuariosApi } from '../../services/api';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aplicarFiltros } from '../../utils/filtros';
import { fecha } from '../../utils/formato';

// JAG-83 / FUJ-122: la Directora crea cuentas del personal, les asigna un rol y las activa o desactiva.
// No se envían correos: la Directora define la contraseña inicial y se la entrega a la persona.
const VACIO = { nombreCompleto: '', email: '', telefono: '', rol: 'CONTADOR', password: '' };

// Mismas reglas que el backend: al menos 8 caracteres combinando letras y números
const errorContrasena = (p) =>
  p.length < 8 ? 'Usa al menos 8 caracteres.' : !/\p{L}/u.test(p) || !/\d/.test(p) ? 'Combina letras y números.' : null;
const ESTADOS = [{ valor: 'ACTIVO', etiqueta: 'Activas' }, { valor: 'INACTIVO', etiqueta: 'Desactivadas' }];

export default function AdminUsuarios() {
  const { usuario } = useAuth();
  const [usuarios, setUsuarios] = useState([]);
  const [roles, setRoles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);
  const [form, setForm] = useState(VACIO);
  const [guardando, setGuardando] = useState(false);
  const [ocupado, setOcupado] = useState(null); // id del usuario que se está modificando
  const [cambiandoClave, setCambiandoClave] = useState(null); // { usuario, password }
  const { valores, cambiar, limpiar } = useFiltros(['q', 'estado', 'desde', 'hasta', 'rol']);

  const cargar = () =>
    usuariosApi.listar()
      .then(r => setUsuarios(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar la lista de usuarios' }))
      .finally(() => setLoading(false));

  useEffect(() => {
    cargar();
    usuariosApi.roles().then(r => setRoles(r.data)).catch(() => {});
  }, []);

  const nombreRol = (codigo) => roles.find(r => r.codigo === codigo)?.nombre ?? codigo;
  const internos = roles.filter(r => r.interno);

  const visibles = aplicarFiltros(usuarios, valores,
    { texto: u => [u.nombreCompleto, u.email, u.telefono], estado: u => (u.activo ? 'ACTIVO' : 'INACTIVO'), fecha: u => u.creadoEn })
    .filter(u => !valores.rol || u.rol === valores.rol);

  const error = (err, texto) => setMsg({ tipo: 'error', texto: err.response?.data?.error || texto });

  const crear = async (e) => {
    e.preventDefault();
    setMsg(null);
    const invalida = errorContrasena(form.password);
    if (invalida) { setMsg({ tipo: 'error', texto: `Contraseña inicial: ${invalida}` }); return; }
    setGuardando(true);
    try {
      const { data } = await usuariosApi.crear({ ...form, telefono: form.telefono.trim() || null });
      setUsuarios(lista => [...lista, data].sort((a, b) => a.nombreCompleto.localeCompare(b.nombreCompleto)));
      setForm(VACIO);
      setMsg({ tipo: 'success', texto: `Listo: cuenta de ${data.nombreCompleto} registrada. Ya puede iniciar sesión con su correo y la contraseña que definiste.` });
    } catch (err) {
      error(err, 'No se pudo crear la cuenta');
    } finally {
      setGuardando(false);
    }
  };

  const actualizar = (data) => setUsuarios(lista => lista.map(u => (u.id === data.id ? data : u)));

  const cambiarRol = async (u, rol) => {
    if (rol === u.rol) return;
    setOcupado(u.id);
    setMsg(null);
    try {
      actualizar((await usuariosApi.cambiarRol(u.id, rol)).data);
      setMsg({ tipo: 'success', texto: `${u.nombreCompleto} ahora es ${nombreRol(rol)}. Deberá volver a iniciar sesión.` });
    } catch (err) {
      error(err, 'No se pudo cambiar el rol');
    } finally {
      setOcupado(null);
    }
  };

  const cambiarActivo = async (u) => {
    if (u.activo && !window.confirm(`¿Desactivar la cuenta de ${u.nombreCompleto}? Ya no podrá iniciar sesión.`)) return;
    setOcupado(u.id);
    setMsg(null);
    try {
      actualizar((await usuariosApi.cambiarActivo(u.id, !u.activo)).data);
      setMsg({ tipo: 'success', texto: u.activo ? `Cuenta de ${u.nombreCompleto} desactivada.` : `Cuenta de ${u.nombreCompleto} activada.` });
    } catch (err) {
      error(err, 'No se pudo cambiar el estado de la cuenta');
    } finally {
      setOcupado(null);
    }
  };

  const guardarContrasena = async (e) => {
    e.preventDefault();
    const { usuario: u, password } = cambiandoClave;
    const invalida = errorContrasena(password);
    if (invalida) { setCambiandoClave({ ...cambiandoClave, error: invalida }); return; }
    setOcupado(u.id);
    setMsg(null);
    try {
      await usuariosApi.cambiarContrasena(u.id, password);
      setCambiandoClave(null);
      setMsg({ tipo: 'success', texto: `Listo: contraseña de ${u.nombreCompleto} actualizada.` });
    } catch (err) {
      setCambiandoClave({ ...cambiandoClave, error: err.response?.data?.error || 'No se pudo cambiar la contraseña' });
    } finally {
      setOcupado(null);
    }
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Usuarios y roles</h1>
          <p>Crea las cuentas del personal y elige qué puede hacer cada persona. Cada rol solo ve sus módulos.</p>
        </div>
        <div className="stat-box">
          <p>Cuentas activas</p>
          <h2>{usuarios.filter(u => u.activo).length} / {usuarios.length}</h2>
        </div>
      </div>

      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}

      <div className="split">
        <section className="panel">
          <h2>Cuentas</h2>
          <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Nombre, correo o teléfono"
                        estados={ESTADOS} etiquetaEstado="Cuenta" fechas="Creada" total={usuarios.length} visibles={visibles.length}>
            <label className="filtro">
              <span>Rol</span>
              <select value={valores.rol} onChange={e => cambiar('rol', e.target.value)}>
                <option value="">Todos</option>
                {roles.map(r => <option key={r.codigo} value={r.codigo}>{r.nombre}</option>)}
              </select>
            </label>
            <BotonExportar reporte="usuarios" filtros={valores} />
          </BarraFiltros>

          {loading ? <p>Cargando...</p> : visibles.length === 0 ? (
            <p className="muted" style={{ textAlign: 'center', padding: 24 }}>No hay cuentas que coincidan con los filtros.</p>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Persona</th><th>Rol</th><th>Estado</th><th>Creada</th><th>Acciones</th></tr></thead>
                <tbody>
                  {visibles.map(u => {
                    const yo = u.email === usuario?.email;
                    return (
                      <tr key={u.id}>
                        <td>
                          <strong>{u.nombreCompleto}</strong>
                          <div className="muted" style={{ fontSize: '0.85rem' }}>{u.email}</div>
                        </td>
                        <td>
                          <select value={u.rol} aria-label={`Rol de ${u.nombreCompleto}`} disabled={yo || ocupado === u.id}
                                  onChange={e => cambiarRol(u, e.target.value)}>
                            {roles.map(r => <option key={r.codigo} value={r.codigo}>{r.nombre}</option>)}
                          </select>
                        </td>
                        <td><span className={`badge ${u.activo ? 'badge-ok' : 'badge-off'}`}>{u.activo ? 'Activa' : 'Desactivada'}</span></td>
                        <td>{fecha(u.creadoEn)}</td>
                        <td>
                          {yo ? <span className="muted">Tu cuenta</span> : (
                            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                              <button type="button" className="btn-secondary" style={{ padding: '4px 10px' }}
                                      disabled={ocupado === u.id} onClick={() => cambiarActivo(u)}>
                                {u.activo ? 'Desactivar' : 'Activar'}
                              </button>
                              {u.activo && (
                                <button type="button" className="btn-secondary" style={{ padding: '4px 10px' }} title="Asignar una contraseña nueva"
                                        disabled={ocupado === u.id} onClick={() => setCambiandoClave({ usuario: u, password: '' })}>
                                  Contraseña
                                </button>
                              )}
                            </div>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="panel">
          <h2>Nueva cuenta del personal</h2>
          <form onSubmit={crear} className="form-stack">
            <div><label htmlFor="u-nombre">Nombre completo</label>
              <input id="u-nombre" required minLength={3} maxLength={150} value={form.nombreCompleto}
                     onChange={e => setForm({ ...form, nombreCompleto: e.target.value })} /></div>
            <div><label htmlFor="u-email">Correo</label>
              <input id="u-email" type="email" required maxLength={150} value={form.email}
                     onChange={e => setForm({ ...form, email: e.target.value })} /></div>
            <div><label htmlFor="u-tel">Teléfono (opcional)</label>
              <input id="u-tel" maxLength={20} value={form.telefono} onChange={e => setForm({ ...form, telefono: e.target.value })} /></div>
            <div><label htmlFor="u-rol">Rol</label>
              <select id="u-rol" value={form.rol} onChange={e => setForm({ ...form, rol: e.target.value })}>
                {internos.map(r => <option key={r.codigo} value={r.codigo}>{r.nombre}</option>)}
              </select>
              <p className="muted" style={{ fontSize: '0.85rem', marginTop: 4 }}>
                {roles.find(r => r.codigo === form.rol)?.descripcion}
              </p>
            </div>
            <div><label htmlFor="u-clave">Contraseña inicial</label>
              <input id="u-clave" type="text" required minLength={8} maxLength={100} autoComplete="new-password" value={form.password}
                     onChange={e => setForm({ ...form, password: e.target.value })} />
              <p className="muted" style={{ fontSize: '0.85rem', marginTop: 4 }}>
                Al menos 8 caracteres con letras y números. Entrégasela a la persona; no se envía ningún correo.
              </p>
            </div>
            <button type="submit" className="btn-primary" disabled={guardando}>{guardando ? 'Creando...' : 'Crear cuenta'}</button>
          </form>
        </section>
      </div>

      {cambiandoClave && (
        <div className="modal-fondo" role="presentation" onClick={() => setCambiandoClave(null)}>
          <div className="modal" role="dialog" aria-modal="true" aria-labelledby="clave-titulo" onClick={e => e.stopPropagation()}>
            <button type="button" className="modal-cerrar" aria-label="Cerrar" onClick={() => setCambiandoClave(null)}>×</button>
            <h2 id="clave-titulo" style={{ marginBottom: 8 }}>Asignar contraseña</h2>
            <p className="muted" style={{ marginBottom: 16 }}>
              Nueva contraseña para <strong>{cambiandoClave.usuario.nombreCompleto}</strong>. La anterior deja de servir.
            </p>
            <form onSubmit={guardarContrasena} className="form-stack">
              {cambiandoClave.error && <div className="error-msg" role="alert">{cambiandoClave.error}</div>}
              <div><label htmlFor="u-nueva-clave">Nueva contraseña</label>
                <input id="u-nueva-clave" type="text" autoFocus required minLength={8} maxLength={100} autoComplete="new-password"
                       value={cambiandoClave.password}
                       onChange={e => setCambiandoClave({ ...cambiandoClave, password: e.target.value, error: null })} /></div>
              <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                <button type="button" className="btn-secondary" onClick={() => setCambiandoClave(null)}>Cancelar</button>
                <button type="submit" className="btn-primary" disabled={ocupado === cambiandoClave.usuario.id}>Guardar</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
