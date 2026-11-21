import { test } from 'node:test';
import assert from 'node:assert/strict';
import { horas, horario, horasDeHorario } from './formato.js';

test('horas en horas y minutos', () => {
  assert.equal(horas(2.75), '2 h 45 min');
  assert.equal(horas(3), '3 h');
  assert.equal(horas(0.5), '30 min');
  assert.equal(horas(0), '0 h');
  assert.equal(horas(null), '0 h');
});

test('horario de 15:30 a 18:15 y por días', () => {
  assert.equal(horario('15:30', '18:15', 1), '15:30 a 18:15');
  assert.equal(horario('14:00:00', '15:45:00', 4), '14:00 a 15:45 × 4 días');
  assert.equal(horario(null, '10:00'), '');
});

test('horas calculadas desde el horario', () => {
  assert.equal(horasDeHorario('10:00', '12:00'), 2);
  assert.equal(horasDeHorario('15:30', '18:15'), 2.75);
  assert.equal(horasDeHorario('14:00', '15:45', 4), 7);
  assert.equal(horasDeHorario('12:00', '10:00'), null);
  assert.equal(horasDeHorario('', '10:00'), null);
});
