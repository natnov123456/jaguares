-- Asistencia diaria a cursos
-- La directora crea los cursos y asigna un docente. El docente registra la asistencia de cada
-- clase (un día = una fila por inscrito, presente o ausente). El porcentaje de asistencia de un
-- participante = días presente / días en que se tomó asistencia en el curso.
-- Al finalizar, solo reciben certificado quienes alcanzan la asistencia mínima del curso.
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS asistencias_curso (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  curso_id        UUID NOT NULL REFERENCES cursos(id) ON DELETE CASCADE,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  fecha           DATE NOT NULL,
  presente        BOOLEAN NOT NULL,
  registrado_por  UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  registrado_en   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (curso_id, participante_id, fecha)
);

CREATE INDEX IF NOT EXISTS ix_asistencias_curso_fecha ON asistencias_curso (curso_id, fecha);
