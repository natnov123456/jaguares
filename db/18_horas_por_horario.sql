-- Horas de voluntariado por horario (ej. de 15:30 a 18:15) en lugar de un número con decimales.
-- Se guarda el horario y los días con ese horario; la columna horas sigue siendo el total calculado
-- (para reportes y sumas). Las filas anteriores conservan sus horas sin horario.
-- Script idempotente: se puede ejecutar más de una vez.

ALTER TABLE asignaciones_voluntario ADD COLUMN IF NOT EXISTS hora_inicio TIME;
ALTER TABLE asignaciones_voluntario ADD COLUMN IF NOT EXISTS hora_fin TIME;
ALTER TABLE asignaciones_voluntario ADD COLUMN IF NOT EXISTS dias SMALLINT CHECK (dias IS NULL OR dias BETWEEN 1 AND 60);

ALTER TABLE participaciones_voluntario ADD COLUMN IF NOT EXISTS hora_inicio TIME;
ALTER TABLE participaciones_voluntario ADD COLUMN IF NOT EXISTS hora_fin TIME;
ALTER TABLE participaciones_voluntario ADD COLUMN IF NOT EXISTS dias SMALLINT CHECK (dias IS NULL OR dias BETWEEN 1 AND 60);

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_asignacion_horario') THEN
    ALTER TABLE asignaciones_voluntario
      ADD CONSTRAINT ck_asignacion_horario CHECK (hora_inicio IS NULL OR hora_fin IS NULL OR hora_fin > hora_inicio);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_participacion_horario') THEN
    ALTER TABLE participaciones_voluntario
      ADD CONSTRAINT ck_participacion_horario CHECK (hora_inicio IS NULL OR hora_fin IS NULL OR hora_fin > hora_inicio);
  END IF;
END $$;
