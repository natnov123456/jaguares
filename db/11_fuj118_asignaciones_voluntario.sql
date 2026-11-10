-- FUJ-118 (JAG-37): asignación de voluntarios a actividades y registro de horas
-- El coordinador asigna voluntarios a un evento o a un curso publicado; cada voluntario
-- es notificado y ve la asignación en su portal. Al terminar la actividad se registran
-- las horas, que se acumulan en el historial (participaciones_voluntario).
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS asignaciones_voluntario (
  id                    UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  voluntario_id         UUID NOT NULL REFERENCES voluntarios(id) ON DELETE CASCADE,
  -- La actividad es un evento o un curso (exactamente uno de los dos)
  evento_id             UUID REFERENCES eventos(id) ON DELETE CASCADE,
  curso_id              UUID REFERENCES cursos(id) ON DELETE CASCADE,
  tarea                 VARCHAR(200),
  estado                VARCHAR(20) NOT NULL DEFAULT 'ASIGNADA'
                        CHECK (estado IN ('ASIGNADA','COMPLETADA','CANCELADA')),
  horas                 NUMERIC(5,2) CHECK (horas IS NULL OR (horas > 0 AND horas <= 200)),
  asignado_por          UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  asignado_en           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  horas_registradas_por UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  horas_registradas_en  TIMESTAMPTZ,
  CONSTRAINT ck_asignacion_una_actividad CHECK ((evento_id IS NULL) <> (curso_id IS NULL)),
  -- Una asignación completada siempre tiene horas
  CONSTRAINT ck_asignacion_completada_horas CHECK (estado <> 'COMPLETADA' OR horas IS NOT NULL)
);

-- Un voluntario no puede tener dos asignaciones vigentes a la misma actividad
CREATE UNIQUE INDEX IF NOT EXISTS ux_asignacion_evento
  ON asignaciones_voluntario (voluntario_id, evento_id) WHERE evento_id IS NOT NULL AND estado <> 'CANCELADA';
CREATE UNIQUE INDEX IF NOT EXISTS ux_asignacion_curso
  ON asignaciones_voluntario (voluntario_id, curso_id) WHERE curso_id IS NOT NULL AND estado <> 'CANCELADA';

CREATE INDEX IF NOT EXISTS ix_asignaciones_evento ON asignaciones_voluntario (evento_id) WHERE evento_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_asignaciones_curso ON asignaciones_voluntario (curso_id) WHERE curso_id IS NOT NULL;

-- El historial (RF-07) pasa a guardar horas. asignacion_id enlaza la participación generada
-- al registrar las horas de una asignación, para poder corregirlas sin duplicar la fila.
ALTER TABLE participaciones_voluntario
  ADD COLUMN IF NOT EXISTS horas NUMERIC(5,2) CHECK (horas IS NULL OR (horas > 0 AND horas <= 200));
ALTER TABLE participaciones_voluntario
  ADD COLUMN IF NOT EXISTS curso_id UUID REFERENCES cursos(id) ON DELETE SET NULL;
ALTER TABLE participaciones_voluntario
  ADD COLUMN IF NOT EXISTS asignacion_id UUID UNIQUE REFERENCES asignaciones_voluntario(id) ON DELETE SET NULL;
