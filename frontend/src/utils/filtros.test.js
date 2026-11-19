// Pruebas de combinaciones de filtros (se ejecutan con: npm test)
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { aplicarFiltros, aParametros, errorDeFiltros, fechaBolivia, hayFiltros, normalizar } from './filtros.js';

// 03:00 UTC del día 11 todavía es el día 10 en Bolivia
const items = [
  { nombre: 'Ana Pérez', correo: 'ana@correo.com', estado: 'CONFIRMADA', fecha: '2026-11-10T15:00:00Z' },
  { nombre: 'José Núñez', correo: 'jose@correo.com', estado: 'CANCELADA', fecha: '2026-11-11T03:00:00Z' },
  { nombre: 'Beto Ramos', correo: 'beto@ramos.bo', estado: 'CONFIRMADA', fecha: '2026-11-20T12:00:00Z' },
  { nombre: 'Carla Ana Ruiz', correo: 'carla@correo.com', estado: 'ASISTIO', fecha: null },
];
const opciones = { texto: (x) => [x.nombre, x.correo], estado: (x) => x.estado, fecha: (x) => x.fecha };
const nombres = (valores) => aplicarFiltros(items, valores, opciones).map((x) => x.nombre);

test('sin filtros devuelve todo', () => assert.equal(nombres({}).length, 4));

test('texto sin tildes ni mayúsculas en cualquier campo', () => {
  assert.deepEqual(nombres({ q: 'ana' }), ['Ana Pérez', 'Carla Ana Ruiz']);
  assert.deepEqual(nombres({ q: 'NUNEZ' }), ['José Núñez']);
  assert.deepEqual(nombres({ q: 'ramos.bo' }), ['Beto Ramos']);
  assert.equal(normalizar('  Ñandú '), 'nandu');
});

test('texto y estado se combinan con Y', () => {
  assert.deepEqual(nombres({ q: 'ana', estado: 'confirmada' }), ['Ana Pérez']);
  assert.deepEqual(nombres({ q: 'ana', estado: 'CANCELADA' }), []);
});

test('rango inclusivo en hora de Bolivia y excluye sin fecha', () => {
  assert.deepEqual(nombres({ desde: '2026-11-10', hasta: '2026-11-10' }), ['Ana Pérez', 'José Núñez']);
  assert.deepEqual(nombres({ desde: '2026-11-11' }), ['Beto Ramos']);
  assert.equal(fechaBolivia('2026-11-11T03:00:00Z'), '2026-11-10');
  assert.equal(fechaBolivia('2026-11-11'), '2026-11-11');
});

test('los tres filtros juntos', () => {
  assert.deepEqual(nombres({ q: 'o', estado: 'CONFIRMADA', desde: '2026-11-15', hasta: '2026-11-30' }), ['Beto Ramos']);
});

test('validación y parámetros', () => {
  assert.match(errorDeFiltros({ desde: '2026-12-01', hasta: '2026-11-01' }), /posterior/);
  assert.equal(errorDeFiltros({ desde: '2026-11-01', hasta: '2026-11-01' }), null);
  assert.deepEqual(aParametros({ q: 'ana', estado: '', desde: null, hasta: '2026-11-30' }), { q: 'ana', hasta: '2026-11-30' });
  assert.equal(hayFiltros({ q: '', estado: '' }), false);
});
