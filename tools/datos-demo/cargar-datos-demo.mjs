// Carga datos de prueba para demostrar la plataforma de la Fundación Unión de los Jaguares.
//
// Usa la API real (así se respetan todas las reglas: cupos, lista de espera, bitácora, certificados,
// encuestas, comprobantes) y la base de datos solo para mover fechas al pasado, simulando varios meses
// de uso: cursos terminados con certificados, eventos realizados con check-in, donaciones de 6 meses,
// transparencia publicada, horas de voluntariado, encuestas respondidas, galería, noticias.
//
// Uso (desde esta carpeta, con la API y la base en marcha):
//   npm install
//   node cargar-datos-demo.mjs --api http://localhost:5055 --db "postgresql://postgres:postgres@localhost:5432/fundacion_jaguares"
// Opciones: --inseguro (acepta el certificado local de https://localhost) · --sin-fotos (no descarga fotos)
//           --forzar (carga aunque ya haya datos de demostración)
//
// Cuentas que deja listas (contraseñas de demostración, NO usar en producción real):
//   directora@jaguares.org / Admin123!       contador@jaguares.org / Contador123!
//   docente@jaguares.org / Docente123!       lucia.docente@jaguares.org / Docente123!
//   eventos@jaguares.org / Eventos123!       coordinacion@jaguares.org / Coord123!
//   voluntario@example.com / Vol123!         juan@example.com / Juan123!
//   participantes: <nombre>@demo.jaguares.org / Demo2026!

import pg from 'pg';

// ---------- Opciones ----------
const args = process.argv.slice(2);
const opcion = (nombre, porDefecto) => {
  const i = args.indexOf(`--${nombre}`);
  return i >= 0 && args[i + 1] && !args[i + 1].startsWith('--') ? args[i + 1] : porDefecto;
};
const bandera = (nombre) => args.includes(`--${nombre}`);
const API = opcion('api', 'http://localhost:5055').replace(/\/$/, '') + '/api';
const DB = opcion('db', 'postgresql://postgres:postgres@localhost:5432/fundacion_jaguares');
if (bandera('inseguro')) process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const SIN_FOTOS = bandera('sin-fotos');

const db = new pg.Client({ connectionString: DB, ssl: /sslmode=require|neon\.tech|supabase/.test(DB) ? { rejectUnauthorized: false } : undefined });
const sql = (texto, valores = []) => db.query(texto, valores);
const uno = async (texto, valores) => (await sql(texto, valores)).rows[0];

// ---------- Utilidades ----------
let semilla = 20261120; // números "aleatorios" repetibles: cada carga produce los mismos datos
const azar = () => { semilla |= 0; semilla = (semilla + 0x6D2B79F5) | 0; let t = Math.imul(semilla ^ (semilla >>> 15), 1 | semilla);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
const entre = (min, max) => min + Math.floor(azar() * (max - min + 1));
const elegir = (lista) => lista[Math.floor(azar() * lista.length)];
const mezclar = (lista) => [...lista].sort(() => azar() - 0.5);

// Fechas en hora de Bolivia (UTC-4)
const HOY = new Date(Date.now() - 4 * 3600_000);
const dia = (offset) => { const d = new Date(Date.UTC(HOY.getUTCFullYear(), HOY.getUTCMonth(), HOY.getUTCDate() + offset)); return d.toISOString().slice(0, 10); };
const instante = (offset, hora = 10, minuto = 0) =>
  new Date(Date.UTC(HOY.getUTCFullYear(), HOY.getUTCMonth(), HOY.getUTCDate() + offset, hora + 4, minuto)).toISOString();

const paso = (texto) => console.log(`\n▶ ${texto}`);
const ok = (texto) => console.log(`  ✔ ${texto}`);

async function api(metodo, ruta, { token, cuerpo, form, aceptar = [] } = {}, intento = 1) {
  const r = await fetch(API + ruta, {
    method: metodo,
    headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(cuerpo !== undefined ? { 'Content-Type': 'application/json' } : {}) },
    body: form ?? (cuerpo !== undefined ? JSON.stringify(cuerpo) : undefined),
  });
  // Límite de solicitudes por IP (protección de la API): se espera y se reintenta
  if (r.status === 429 && intento <= 12) {
    console.log('  · límite de solicitudes alcanzado, esperando 60 s...');
    await new Promise(res => setTimeout(res, 60_000));
    return api(metodo, ruta, { token, cuerpo, form, aceptar }, intento + 1);
  }
  const texto = await r.text();
  let datos; try { datos = texto ? JSON.parse(texto) : null; } catch { datos = texto; }
  if (!r.ok && !aceptar.includes(r.status))
    throw new Error(`${metodo} ${ruta} → ${r.status}: ${typeof datos === 'string' ? datos.slice(0, 200) : JSON.stringify(datos)}`);
  return datos;
}

const sesiones = new Map();
async function entrar(email, password) {
  if (sesiones.has(email)) return sesiones.get(email);
  const { token } = await api('POST', '/Auth/login', { cuerpo: { email, password } });
  sesiones.set(email, token);
  return token;
}
const idDe = async (email) => (await uno('SELECT id FROM usuarios WHERE lower(email) = lower($1)', [email])).id;

// ---------- Datos base ----------
const PERSONAL = [
  ['María Directora', 'directora@jaguares.org', '70000001', 'Admin123!', 'DIRECTORA'],
  ['Carlos Docente', 'docente@jaguares.org', '70000002', 'Docente123!', 'DOCENTE'],
  ['Lucía Mamani Docente', 'lucia.docente@jaguares.org', '70000006', 'Docente123!', 'DOCENTE'],
  ['Elena Encargada', 'eventos@jaguares.org', '70000003', 'Eventos123!', 'ENCARGADO_EVENTOS'],
  ['Roberto Contador', 'contador@jaguares.org', '70000007', 'Contador123!', 'CONTADOR'],
  ['Patricia Coordinadora', 'coordinacion@jaguares.org', '70000008', 'Coord123!', 'COORDINADOR_VOLUNTARIADO'],
  ['Valeria Voluntaria', 'voluntario@example.com', '70000004', 'Vol123!', 'VOLUNTARIO'],
  ['Juan Pérez', 'juan@example.com', '70000005', 'Juan123!', 'PARTICIPANTE'],
];
const CLAVE = Object.fromEntries(PERSONAL.map(p => [p[1], p[3]]));

const PARTICIPANTES = [
  'Ana Quispe Mamani', 'Bruno Choque Flores', 'Camila Rojas Vargas', 'Diego Condori Limachi', 'Elena Gutiérrez Paz',
  'Fernando Huanca Ticona', 'Gabriela Mendoza Ríos', 'Héctor Apaza Chura', 'Isabel Cruz Medina', 'Jorge Villca Poma',
  'Karen Salazar Ortiz', 'Luis Mamani Copa', 'Mónica Aguilar Torrez', 'Nelson Callisaya Quispe', 'Olga Ramírez Soliz',
  'Pablo Tarqui Nina', 'Rosa Laura Chambi', 'Sergio Pinto Arce',
].map((nombre) => {
  const [n, a] = nombre.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').split(' ');
  return { nombre, email: `${n}.${a}@demo.jaguares.org`, telefono: `7${entre(1000000, 9999999)}` };
});
const CLAVE_DEMO = 'Demo2026!';
// Las dos casillas obligatorias de los formularios públicos (Términos y Privacidad)
const ACEPTA = { aceptaTerminos: true, aceptaPrivacidad: true };

const VOLUNTARIOS = [
  ['Andrés Patzi Ramos', 'andres.patzi@demo.jaguares.org', '71234501'],
  ['Beatriz Loza Quiroga', 'beatriz.loza@demo.jaguares.org', '71234502'],
  ['Cristian Yujra Alanoca', 'cristian.yujra@demo.jaguares.org', '71234503'],
  ['Daniela Fernández Castro', 'daniela.fernandez@demo.jaguares.org', '71234504'],
  ['Eduardo Ticona Mamani', 'eduardo.ticona@demo.jaguares.org', '71234505'],
  ['Fabiola Céspedes Arias', 'fabiola.cespedes@demo.jaguares.org', '71234506'],
  ['Gustavo Nina Cusi', 'gustavo.nina@demo.jaguares.org', '71234507'],
  ['Helen Vásquez Rocha', 'helen.vasquez@demo.jaguares.org', '71234508'],
];

// ---------- Secciones ----------
async function cuentas() {
  paso('Cuentas del personal y participantes');
  for (const [nombre, email, telefono, clave, rol] of PERSONAL) {
    await sql(`INSERT INTO usuarios (nombre_completo, email, telefono, password_hash, rol)
               VALUES ($1, $2, $3, crypt($4, gen_salt('bf', 11)), $5::rol_enum)
               ON CONFLICT (email) DO UPDATE SET password_hash = EXCLUDED.password_hash, rol = EXCLUDED.rol, activo = TRUE`,
      [nombre, email, telefono, clave, rol]);
  }
  ok(`${PERSONAL.length} cuentas del personal (una por rol)`);
  for (const p of PARTICIPANTES) {
    await api('POST', '/Auth/registro', { cuerpo: { nombreCompleto: p.nombre, email: p.email, password: CLAVE_DEMO, telefono: p.telefono, ...ACEPTA }, aceptar: [400] });
    p.id = await idDe(p.email);
    p.token = await entrar(p.email, CLAVE_DEMO);
  }
  // Fechas de registro repartidas en los últimos meses
  for (const p of PARTICIPANTES) await sql('UPDATE usuarios SET creado_en = $2 WHERE id = $1', [p.id, instante(-entre(20, 170), entre(8, 20))]);
  ok(`${PARTICIPANTES.length} participantes (contraseña ${CLAVE_DEMO})`);
}

async function voluntarios() {
  paso('Registro de voluntarios');
  for (const [nombre, email, telefono] of VOLUNTARIOS)
    await api('POST', '/Voluntarios/postular', { cuerpo: { nombreCompleto: nombre, email, telefono, ...ACEPTA }, aceptar: [400] });
  const filas = (await sql(`SELECT id, email FROM voluntarios WHERE email = ANY($1) OR email = 'voluntario@example.com'`,
    [VOLUNTARIOS.map(v => v[1])])).rows;
  for (const f of filas) await sql('UPDATE voluntarios SET postulado_en = $2 WHERE id = $1', [f.id, instante(-entre(60, 200))]);
  // Uno inactivo para que se vea el filtro por estado
  await sql(`UPDATE voluntarios SET estado = 'INACTIVO' WHERE email = $1`, [VOLUNTARIOS[7][1]]);
  ok(`${filas.length} voluntarios (uno inactivo)`);
  return filas.filter(f => f.email !== VOLUNTARIOS[7][1]).map(f => f.id);
}

async function institucional() {
  paso('Quiénes somos');
  const dir = await entrar('directora@jaguares.org', CLAVE['directora@jaguares.org']);
  await api('PUT', '/Institucional', { token: dir, cuerpo: {
    mision: 'Acompañar a niñas, niños, jóvenes y familias de La Paz y El Alto con educación, cultura y voluntariado, para que construyan oportunidades en su comunidad.',
    vision: 'Ser una fundación referente en Bolivia por su trabajo comunitario transparente, cercano y sostenible.',
    historia: 'La Fundación Unión de los Jaguares nació en 2015 como un grupo de jóvenes voluntarios que daban clases de apoyo escolar en la zona de Villa Fátima.\n\nCon los años sumó talleres de oficios, campañas de salud, ferias de reciclaje y un programa de voluntariado con más de cien personas. Hoy trabaja con escuelas, juntas vecinales y empresas aliadas.',
    equipo: [
      { nombre: 'María Directora', cargo: 'Directora Ejecutiva', descripcion: 'Coordina la fundación y sus alianzas.' },
      { nombre: 'Roberto Contador', cargo: 'Contador', descripcion: 'Administra las donaciones y la transparencia.' },
      { nombre: 'Elena Encargada', cargo: 'Encargada de operaciones', descripcion: 'Organiza eventos y actividades.' },
      { nombre: 'Patricia Coordinadora', cargo: 'Coordinadora de voluntariado', descripcion: 'Acompaña a los voluntarios.' },
    ],
    direccion: 'Av. Busch N° 1234, Miraflores, La Paz', telefono: '+591 2 2245678', emailContacto: 'contacto@jaguares.org', horario: 'Lunes a viernes de 9:00 a 18:00',
  } });
  ok('misión, visión, historia, equipo y datos de contacto');
}

async function noticias() {
  paso('Noticias');
  const dir = await entrar('directora@jaguares.org', CLAVE['directora@jaguares.org']);
  const lista = [
    ['Más de 120 familias participaron en la Feria de Reciclaje', 'La feria reunió a vecinos de toda la ciudad en la Plaza Murillo.', 1018, -58, true],
    ['Nuevo taller de computación para adultos mayores', 'Abrimos inscripciones para aprender a usar el celular y la computadora.', 0, -50, true],
    ['Gracias a su ayuda: 450 kits escolares entregados', 'La campaña de útiles escolares superó su meta.', 1060, -35, true],
    ['Campaña de salud visual en El Alto', 'Más de 200 controles de vista gratuitos en una jornada.', 1080, -29, true],
    ['Festival cultural Jaguares: música, danza y comida', 'Una tarde para celebrar con la comunidad.', 1043, -9, true],
    ['Abrimos convocatoria de voluntariado 2027', 'Súmate a nuestros programas de apoyo escolar y medio ambiente.', 1062, -2, true],
    ['Próximamente: ciclo de charlas de educación financiera', 'Borrador interno, todavía no publicado.', 0, 0, false],
  ];
  for (const [titulo, resumen, foto, offset, publicada] of lista) {
    await api('POST', '/Contenido/noticias', { token: dir, cuerpo: {
      titulo, resumen, publicada,
      contenido: `${resumen}\n\nAgradecemos a voluntarios, aliados y donantes que lo hicieron posible. Pronto compartiremos más novedades y fotos en nuestra galería.`,
      imagenUrl: foto ? `https://picsum.photos/id/${foto}/1200/700` : null,
    } });
    if (publicada) await sql('UPDATE noticias SET publicado_en = $2, creado_en = $2 WHERE titulo = $1', [titulo, instante(offset, 11)]);
  }
  ok(`${lista.length} noticias (una en borrador)`);
}

// Responde la encuesta pendiente de una actividad con puntajes de 3 a 5
async function responderEncuesta(p, actividad, fechaRespuesta) {
  const pendientes = await api('GET', '/Encuestas/mis-pendientes', { token: p.token });
  const pendiente = pendientes.find(x => x.actividad === actividad);
  if (!pendiente) return false;
  const encuesta = await api('GET', `/Encuestas/responder/${pendiente.token}`);
  const comentarios = ['Muy buena actividad, aprendí mucho.', 'Me gustaría que dure más días.', 'Excelente organización.', 'Los facilitadores fueron muy amables.', ''];
  await api('POST', `/Encuestas/responder/${pendiente.token}`, { cuerpo: { respuestas: encuesta.preguntas.map(q => ({
    preguntaId: q.id, valor: q.tipo === 'ESCALA' ? entre(3, 5) : null, texto: q.tipo === 'TEXTO' ? elegir(comentarios) || null : null,
  })) } });
  return true; // la fecha de respuesta se ajusta después, junto con las demás de la actividad
}

async function registrarHoras(coord, filtro, inicio, fin, dias = 1) {
  const asignaciones = await api('GET', `/Voluntarios/asignaciones?${filtro}`, { token: coord });
  for (const a of asignaciones.filter(x => x.estado === 'ASIGNADA')) {
    const [h, m] = inicio.split(':').map(Number);
    const desfase = elegir([0, 0, 15, 30]); // no todos llegan a la misma hora
    const ini = `${String(h).padStart(2, '0')}:${String(m + desfase).padStart(2, '0')}`;
    await api('PUT', `/Voluntarios/asignaciones/${a.id}/horas`, { token: coord, cuerpo: { horaInicio: ini, horaFin: fin, dias } });
  }
  return asignaciones.length;
}

const CURSOS = [
  // titulo, descripción, docente, inicio (días desde hoy), duración, cupo, inscritos, ¿finalizar?, voluntarios
  ['Taller de reciclaje creativo', 'Aprende a transformar residuos en objetos útiles y artesanías.', 'docente@jaguares.org', -75, 5, 15, 10, true, 2],
  ['Computación básica para adultos mayores', 'Uso del celular, correo electrónico y videollamadas paso a paso.', 'lucia.docente@jaguares.org', -46, 8, 15, 12, true, 2],
  ['Primeros auxilios comunitarios', 'Qué hacer ante cortes, quemaduras, desmayos y emergencias en casa.', 'docente@jaguares.org', -21, 4, 12, 9, true, 1],
  ['Huerto urbano en casa', 'Cultiva verduras en espacios pequeños con materiales reciclados.', 'lucia.docente@jaguares.org', -4, 10, 12, 8, false, 1],
  ['Liderazgo juvenil y oratoria', 'Habla en público con confianza y organiza proyectos en tu barrio.', 'docente@jaguares.org', 0, 6, 15, 5, false, 0],
  ['Inglés básico para emprendedores', 'Vocabulario para atender clientes y vender por internet.', 'lucia.docente@jaguares.org', 7, 12, 20, 6, false, 0],
  ['Lengua de señas boliviana – nivel 1', 'Comunicación básica con personas sordas.', 'docente@jaguares.org', 12, 8, 4, 4, false, 0],
  ['Repostería para emprendimientos', 'Recetas, costos y presentación para vender tus productos.', 'lucia.docente@jaguares.org', 20, 6, 12, 0, false, 0],
];

async function cursos(idsVoluntarios) {
  paso('Cursos, inscripciones, asistencia, certificados y encuestas');
  const dir = await entrar('directora@jaguares.org', CLAVE['directora@jaguares.org']);
  const coord = await entrar('coordinacion@jaguares.org', CLAVE['coordinacion@jaguares.org']);
  let certificados = 0, encuestas = 0;
  for (const [titulo, descripcion, docenteEmail, inicio, dias, cupo, nInscritos, finalizar, nVol] of CURSOS) {
    const docenteId = await idDe(docenteEmail);
    const yaEmpezo = inicio < 0;
    // Se crea en el futuro para poder inscribir y asignar; luego se mueve a su fecha real
    const curso = await api('POST', '/Cursos', { token: dir, cuerpo: {
      titulo, descripcion, docenteId, cupoMaximo: cupo, asistenciaMinimaPct: 75,
      fechaInicio: dia(yaEmpezo ? 30 : inicio), fechaFin: dia((yaEmpezo ? 30 : inicio) + dias - 1),
    } });
    const inscritos = mezclar(PARTICIPANTES).slice(0, nInscritos);
    for (const p of inscritos) await api('POST', `/Cursos/${curso.id}/inscribir`, { token: p.token });
    if (nVol > 0) await api('POST', '/Voluntarios/asignaciones', { token: coord, cuerpo: {
      voluntarioIds: mezclar(idsVoluntarios).slice(0, nVol), cursoId: curso.id, tarea: elegir(['Apoyo en el aula', 'Registro de asistencia', 'Preparación de materiales']) } });

    if (yaEmpezo) {
      await sql('UPDATE cursos SET fecha_inicio = $2, fecha_fin = $3, creado_en = $4 WHERE id = $1',
        [curso.id, dia(inicio), dia(inicio + dias - 1), instante(inicio - 20, 9)]);
      await sql('UPDATE inscripciones SET fecha_inscripcion = $2 WHERE curso_id = $1', [curso.id, instante(inicio - entre(3, 15), 15)]);
      // El docente pasa lista cada día hasta hoy (asistencia del 60 % al 100 % según la persona)
      const docente = await entrar(docenteEmail, CLAVE[docenteEmail]);
      const constancia = new Map(inscritos.map(p => [p.id, 0.6 + azar() * 0.4]));
      for (let d = 0; d < dias && inicio + d <= 0; d++) {
        await api('POST', `/Cursos/${curso.id}/asistencia`, { token: docente, cuerpo: {
          fecha: dia(inicio + d), registros: inscritos.map(p => ({ participanteId: p.id, presente: azar() < constancia.get(p.id) })) } });
      }
      if (finalizar) {
        const fin = await api('POST', `/Cursos/${curso.id}/finalizar`, { token: docente });
        certificados += fin.certificados_generados ?? 0;
        const cierre = instante(inicio + dias - 1, 18);
        await sql('UPDATE certificados SET emitido_en = $2 WHERE curso_id = $1', [curso.id, cierre]);
        await sql('UPDATE encuestas SET creada_en = $2 WHERE curso_id = $1', [curso.id, cierre]);
        await sql('UPDATE encuesta_invitaciones SET enviada_en = $2 FROM encuestas e WHERE encuesta_invitaciones.encuesta_id = e.id AND e.curso_id = $1', [curso.id, cierre]);
        await registrarHoras(coord, `cursoId=${curso.id}`, '14:00', '17:30', dias);
        for (const p of inscritos.slice(0, 4))
          if (await responderEncuesta(p, titulo, instante(inicio + dias + entre(0, 3), entre(9, 21)))) encuestas++;
        await sql(`UPDATE encuesta_invitaciones SET respondida_en = enviada_en + (random() * interval '3 days')
                   FROM encuestas e WHERE encuesta_invitaciones.encuesta_id = e.id AND e.curso_id = $1 AND respondida_en IS NOT NULL`, [curso.id]);
      }
    }
    // Curso lleno: tres personas más quedan en la lista de espera
    if (cupo === nInscritos && !yaEmpezo && nInscritos > 0)
      for (const p of PARTICIPANTES.filter(x => !inscritos.includes(x)).slice(0, 3))
        await api('POST', `/Cursos/${curso.id}/lista-espera`, { token: p.token });
    ok(`${titulo} (${yaEmpezo ? (finalizar ? 'finalizado' : 'en curso') : inicio === 0 ? 'empieza hoy' : 'próximo'}, ${nInscritos} inscritos)`);
  }
  // Un curso cancelado
  const cancelado = await api('POST', '/Cursos', { token: dir, cuerpo: {
    titulo: 'Fotografía con celular', descripcion: 'Composición, luz y edición básica.', docenteId: await idDe('docente@jaguares.org'),
    cupoMaximo: 10, asistenciaMinimaPct: 75, fechaInicio: dia(25), fechaFin: dia(28) } });
  await api('POST', `/Cursos/${cancelado.id}/inscribir`, { token: PARTICIPANTES[0].token });
  await api('POST', `/Cursos/${cancelado.id}/cancelar`, { token: dir, cuerpo: { motivo: 'El docente invitado no podrá asistir en esas fechas.' } });
  ok('Fotografía con celular (cancelado)');
  ok(`${certificados} certificados emitidos y ${encuestas} encuestas de cursos respondidas`);
}

const EVENTOS = [
  // titulo, descripción, lugar, días desde hoy, hora inicio, hora fin, inscritos, % que asiste, voluntarios
  ['Feria de reciclaje en la Plaza Murillo', 'Intercambio de residuos por plantas y talleres para niños.', 'Plaza Murillo, La Paz', -60, 9, 13, 14, 0.85, 3],
  ['Campaña de salud visual', 'Controles de vista y entrega de lentes a bajo costo.', 'Sede social Villa Adela, El Alto', -30, 8, 14, 12, 0.9, 2],
  ['Festival cultural Jaguares', 'Música, danza y comida tradicional con la comunidad.', 'Coliseo de Villa Fátima', -10, 15, 20, 16, 0.75, 3],
  ['Jornada de limpieza del río Choqueyapu', 'Recojo de residuos con vecinos y colegios.', 'Puente de las Américas', -3, 8, 12, 10, 0.8, 2],
  ['Charla: educación financiera para familias', 'Ahorro, presupuesto y crédito responsable.', 'Auditorio de la fundación', 3, 18, 20, 7, 0, 1],
  ['Cine foro juvenil', 'Película y conversación sobre proyectos de vida.', 'Casa de la Cultura', 9, 16, 19, 5, 0, 0],
  ['Maratón solidaria 5K', 'Carrera familiar a beneficio de la campaña de útiles.', 'Av. 6 de Agosto', 16, 7, 11, 9, 0, 2],
];

async function eventos(idsVoluntarios) {
  paso('Eventos, check-in con QR, cierre de asistencia y encuestas');
  const enc = await entrar('eventos@jaguares.org', CLAVE['eventos@jaguares.org']);
  const coord = await entrar('coordinacion@jaguares.org', CLAVE['coordinacion@jaguares.org']);
  let checkins = 0;
  for (const [titulo, descripcion, lugar, offset, hIni, hFin, nInscritos, asistencia, nVol] of EVENTOS) {
    const pasado = offset < 0;
    const evento = await api('POST', '/Eventos', { token: enc, cuerpo: {
      titulo, descripcion, lugar, fechaInicio: instante(pasado ? 40 : offset, hIni), fechaFin: instante(pasado ? 40 : offset, hFin) } });
    const inscritos = mezclar(PARTICIPANTES).slice(0, nInscritos);
    const codigos = new Map();
    for (const p of inscritos) codigos.set(p, (await api('POST', `/Eventos/${evento.id}/inscribir`, { token: p.token })).codigoQr);
    if (nVol > 0) await api('POST', '/Voluntarios/asignaciones', { token: coord, cuerpo: {
      voluntarioIds: mezclar(idsVoluntarios).slice(0, nVol), eventoId: evento.id, tarea: elegir(['Logística', 'Recepción y registro', 'Apoyo a los asistentes']) } });

    if (pasado) {
      // El check-in solo se habilita el día del evento: se hace "hoy" y después el evento pasa a su fecha real
      await sql(`UPDATE eventos SET fecha_inicio = now() - interval '1 hour', fecha_fin = now() + interval '2 hours' WHERE id = $1`, [evento.id]);
      const presentes = [];
      for (const p of inscritos) {
        if (azar() > asistencia) continue;
        await api('POST', `/Eventos/${evento.id}/checkin`, { token: enc, cuerpo: { codigo: codigos.get(p) } });
        presentes.push(p);
      }
      await sql('UPDATE eventos SET fecha_inicio = $2, fecha_fin = $3, creado_en = $4 WHERE id = $1',
        [evento.id, instante(offset, hIni), instante(offset, hFin), instante(offset - 25, 10)]);
      await sql('UPDATE inscripciones_evento SET inscrito_en = $2 WHERE evento_id = $1', [evento.id, instante(offset - entre(2, 12), 19)]);
      // Hora de llegada de cada asistente (escaneo del QR)
      for (const p of presentes) {
        await sql('UPDATE inscripciones_evento SET checkin_en = $3 WHERE evento_id = $1 AND participante_id = $2',
          [evento.id, p.id, instante(offset, hIni, entre(0, 50))]);
        checkins++;
      }
      await api('POST', `/Eventos/${evento.id}/cerrar-asistencia`, { token: enc });
      await sql('UPDATE encuestas SET creada_en = $2 WHERE evento_id = $1', [evento.id, instante(offset, hFin)]);
      await sql('UPDATE encuesta_invitaciones SET enviada_en = $2 FROM encuestas e WHERE encuesta_invitaciones.encuesta_id = e.id AND e.evento_id = $1',
        [evento.id, instante(offset, hFin)]);
      await registrarHoras(coord, `eventoId=${evento.id}`, `${String(hIni - 1).padStart(2, '0')}:00`, `${hFin}:15`);
      for (const p of presentes.slice(0, 4)) await responderEncuesta(p, titulo, instante(offset + 1, 10));
      await sql(`UPDATE encuesta_invitaciones SET respondida_en = enviada_en + (random() * interval '2 days')
                 FROM encuestas e WHERE encuesta_invitaciones.encuesta_id = e.id AND e.evento_id = $1 AND respondida_en IS NOT NULL`, [evento.id]);
    }
    ok(`${titulo} (${pasado ? 'realizado' : 'próximo'}, ${nInscritos} inscritos)`);
  }
  // La maratón se reprograma (los inscritos lo ven y reciben aviso) y un concierto se cancela
  const maraton = (await api('GET', '/Eventos/gestion', { token: enc })).find(e => e.titulo === 'Maratón solidaria 5K');
  if (maraton) await api('PUT', `/Eventos/${maraton.id}/reprogramar`, { token: enc, cuerpo: {
    fechaInicio: instante(18, 7), fechaFin: instante(18, 11), lugar: 'Av. 6 de Agosto (salida en la Plaza del Estudiante)', motivo: 'Cambio de fecha por el permiso municipal.' } });
  const concierto = await api('POST', '/Eventos', { token: enc, cuerpo: {
    titulo: 'Concierto benéfico de fin de año', descripcion: 'Bandas locales a beneficio de la fundación.', lugar: 'Teatro Municipal', fechaInicio: instante(30, 19), fechaFin: instante(30, 22) } });
  await api('POST', `/Eventos/${concierto.id}/cancelar`, { token: enc, cuerpo: { motivo: 'El teatro no estará disponible.' } });
  ok(`${checkins} check-in con QR; maratón reprogramada y concierto cancelado`);
}

async function participacionesManual(idsVoluntarios) {
  paso('Actividades de voluntariado fuera de la plataforma (con horario)');
  const coord = await entrar('coordinacion@jaguares.org', CLAVE['coordinacion@jaguares.org']);
  const actividades = [['Apoyo escolar en la Unidad Educativa Bolivia', '15:00', '18:00'],
    ['Clasificación de donaciones de ropa', '09:30', '12:45'], ['Visita al hogar de ancianos San Ramón', '10:00', '12:30']];
  for (const [descripcion, horaInicio, horaFin] of actividades)
    await api('POST', `/Voluntarios/${elegir(idsVoluntarios)}/participaciones`, { token: coord, cuerpo: { eventoId: null, descripcion, horaInicio, horaFin } });
  await sql(`UPDATE participaciones_voluntario SET fecha = now() - (random() * interval '90 days') WHERE asignacion_id IS NULL AND evento_id IS NULL AND curso_id IS NULL`);
  ok(`${actividades.length} actividades registradas`);
}

async function donaciones() {
  paso('Campañas, donaciones, correcciones, anulación y transparencia');
  const cont = await entrar('contador@jaguares.org', CLAVE['contador@jaguares.org']);
  const campanas = [];
  for (const [nombre, descripcion, meta, limite] of [
    ['Útiles escolares 2027', 'Mochilas y útiles para 500 niñas y niños.', 15000, 60],
    ['Abrigo para el invierno', 'Chamarras, gorros y frazadas para familias del altiplano.', 8000, 25],
    ['Equipamiento del aula de computación', 'Diez computadoras para los cursos de la fundación.', 20000, 90],
  ]) campanas.push(await api('POST', '/Campanas', { token: cont, cuerpo: { nombre, descripcion, meta, fechaLimite: dia(limite), activa: true } }));
  ok(`${campanas.length} campañas con meta`);

  const donantes = [['Empresa Andina SRL', 'contacto@andina.com.bo'], ['Familia Rodríguez', null], ['Club Rotario La Paz', 'rotario@demo.bo'],
    ['Panadería El Trigal', null], ['Colegio San Andrés', 'direccion@sanandres.edu.bo'], ['Anónimo', null], ['Laura Pacheco', 'laura.p@demo.bo'],
    ['Farmacias Vida', 'rse@vida.com.bo'], ['Iglesia San Francisco', null], ['Marco Antonio Gisbert', 'mgisbert@demo.bo']];
  const medios = ['EFECTIVO', 'TRANSFERENCIA', 'QR', 'TRANSFERENCIA', 'QR'];
  const creadas = [];
  for (let i = 0; i < 42; i++) {
    const [donanteNombre, donanteEmail] = elegir(donantes);
    const medio = elegir(medios);
    const d = await api('POST', '/Donaciones', { token: cont, cuerpo: {
      monto: elegir([50, 100, 150, 200, 250, 300, 500, 800, 1000, 1500, 2500]), medio,
      referencia: medio === 'EFECTIVO' ? `REC-${1000 + i}` : `${medio === 'QR' ? 'QR' : 'TRF'}-${entre(100000, 999999)}`,
      donanteId: null, donanteNombre, donanteEmail, campanaId: azar() < 0.55 ? elegir(campanas).id : null } });
    creadas.push(d);
    // Repartidas en los últimos 6 meses (incluido el actual)
    const offset = i < 6 ? -entre(0, Math.max(0, HOY.getUTCDate() - 1)) : -entre(1, 175);
    await sql('UPDATE donaciones SET fecha = $2 WHERE id = $1', [d.id, instante(offset, entre(9, 19), entre(0, 59))]);
  }
  // Donaciones en línea de Juan (se ven en "Mis donaciones" con su comprobante)
  const juan = await entrar('juan@example.com', CLAVE['juan@example.com']);
  for (const monto of [100, 250])
    await api('POST', '/Donaciones/en-linea', { token: juan, cuerpo: { monto, medio: 'QR', referencia: null, donanteId: null, campanaId: campanas[0].id, ...ACEPTA } });
  ok(`${creadas.length + 2} donaciones en 6 meses`);

  // Bitácora: dos correcciones y una anulación (quedan con usuario, hora, antes y después)
  const [a, b, c] = creadas;
  const detalle = async (id) => uno('SELECT monto, medio, referencia, campana_id FROM donaciones WHERE id = $1', [id]);
  const da = await detalle(a.id);
  await api('PUT', `/Donaciones/${a.id}`, { token: cont, cuerpo: { monto: Number(da.monto) + 50, medio: da.medio, referencia: da.referencia, campanaId: da.campana_id, motivo: 'El recibo físico decía 50 Bs. más.' } });
  const db2 = await detalle(b.id);
  await api('PUT', `/Donaciones/${b.id}`, { token: cont, cuerpo: { monto: Number(db2.monto), medio: 'TRANSFERENCIA', referencia: `TRF-${entre(100000, 999999)}`, campanaId: db2.campana_id, motivo: 'Se registró como efectivo por error.' } });
  await api('POST', `/Donaciones/${c.id}/anular`, { token: cont, cuerpo: { motivo: 'Donación duplicada: ya estaba registrada.' } });
  ok('2 correcciones y 1 anulación en la bitácora');

  // Transparencia de los 3 meses anteriores
  for (let m = 1; m <= 3; m++) {
    const f = new Date(Date.UTC(HOY.getUTCFullYear(), HOY.getUTCMonth() - m, 1));
    const [anio, mes] = [f.getUTCFullYear(), f.getUTCMonth() + 1];
    const previa = await api('GET', `/Transparencia/vista-previa/${anio}/${mes}`, { token: cont });
    const total = Number(previa.totalRecaudado);
    if (!total) continue;
    const r = (pct) => Math.floor(total * pct * 100) / 100;
    await api('PUT', `/Transparencia/${anio}/${mes}`, { token: cont, cuerpo: { nota: 'Gracias a todas las personas e instituciones que donaron este mes.', destinos: [
      { categoria: 'Talleres y materiales educativos', monto: r(0.45), descripcion: 'Materiales de los cursos y apoyo escolar.' },
      { categoria: 'Campañas sociales', monto: r(0.35), descripcion: 'Útiles, abrigo y salud visual.' },
      { categoria: 'Gastos operativos', monto: r(0.15), descripcion: 'Transporte y servicios básicos.' },
    ] } });
  }
  ok('transparencia publicada de los 3 meses anteriores');
}

async function galeria() {
  paso('Galería');
  const dir = await entrar('directora@jaguares.org', CLAVE['directora@jaguares.org']);
  const albumes = [
    ['Feria de reciclaje 2026', 'Vecinos, colegios y voluntarios en la Plaza Murillo.', [1018, 1043, 1057, 1069, 1080], true, null],
    ['Campaña de salud visual', 'Controles de vista en El Alto.', [1025, 1062, 1074, 1084], true, 'https://www.youtube.com/watch?v=ysz5S6PUM-U'],
    ['Festival cultural Jaguares', 'Música, danza y comida tradicional.', [1015, 1016, 1036, 1039, 1040, 1044], true, null],
    ['Taller de reciclaje creativo (borrador)', 'Fotos por revisar antes de publicar.', [1060, 1067], false, null],
  ];
  let fotos = 0;
  for (const [titulo, descripcion, ids, publicar, video] of albumes) {
    const album = await api('POST', '/Galeria/albumes', { token: dir, cuerpo: { titulo, descripcion, publicado: false } });
    if (!SIN_FOTOS) for (const id of ids) {
      try {
        const img = await fetch(`https://picsum.photos/id/${id}/1600/1067.jpg`);
        if (!img.ok) continue;
        const form = new FormData();
        form.append('archivos', new Blob([await img.arrayBuffer()], { type: 'image/jpeg' }), `foto-${id}.jpg`);
        await api('POST', `/Galeria/albumes/${album.id}/imagenes`, { token: dir, form });
        fotos++;
      } catch (e) { console.log(`  · no se pudo descargar la foto ${id}: ${e.message}`); }
    }
    if (video) await api('POST', `/Galeria/albumes/${album.id}/videos`, { token: dir, cuerpo: { url: video, titulo: 'Resumen en video' } });
    if (publicar) await api('PUT', `/Galeria/albumes/${album.id}`, { token: dir, cuerpo: { titulo, descripcion, publicado: true } });
  }
  ok(`${albumes.length} álbumes (uno oculto), ${fotos} fotos${SIN_FOTOS ? ' (sin fotos: --sin-fotos)' : ''} y 1 video`);
}

async function contacto() {
  paso('Consultas del formulario de contacto');
  for (const [nombre, email, asunto, mensaje] of [
    ['Rosa Flores', 'rosa.flores@demo.bo', 'Quiero ser voluntaria', 'Hola, tengo 22 años y me gustaría apoyar en el programa de apoyo escolar los sábados. ¿Cómo me inscribo?'],
    ['Colegio Don Bosco', 'pastoral@donbosco.demo.bo', 'Alianza para la feria de reciclaje', 'Nos interesa participar con nuestros estudiantes en la próxima feria. ¿Podemos coordinar una reunión?'],
  ]) await api('POST', '/Contacto', { cuerpo: { nombre, email, asunto, mensaje, sitioWeb: null, ...ACEPTA } });
  ok('2 consultas enviadas al correo institucional');
}

// ---------- Programa ----------
async function principal() {
  console.log(`Cargando datos de prueba en ${API}`);
  await db.connect();
  const yaHay = await uno(`SELECT count(*)::int AS n FROM cursos WHERE titulo = $1`, [CURSOS[0][0]]);
  if (yaHay.n > 0 && !bandera('forzar')) {
    console.log('\nYa hay datos de demostración en esta base (existe el curso "' + CURSOS[0][0] + '"). Usa --forzar para cargar otra vez.');
    return;
  }
  await cuentas();
  const idsVoluntarios = await voluntarios();
  await institucional();
  await noticias();
  await cursos(idsVoluntarios);
  await eventos(idsVoluntarios);
  await participacionesManual(idsVoluntarios);
  await donaciones();
  await galeria();
  await contacto();
  console.log('\n✅ Datos de prueba cargados. Entra con directora@jaguares.org / Admin123! para ver el panel de indicadores.');
}

principal()
  .catch((e) => { console.error(`\n✘ ${e.message}`); process.exitCode = 1; })
  .finally(() => db.end());
