-- FUJ-120 (JAG-39): encuesta de satisfacción post-actividad
-- Al cerrar la asistencia de un evento (o finalizar un curso) se crea una encuesta de 3 a 5
-- preguntas y cada asistente recibe un enlace personal (token) para responderla una sola vez.
-- El administrador ve el promedio de satisfacción y los comentarios (sin nombres).
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS encuestas (
  id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  -- La actividad es un curso o un evento (exactamente uno de los dos)
  curso_id    UUID REFERENCES cursos(id) ON DELETE CASCADE,
  evento_id   UUID REFERENCES eventos(id) ON DELETE CASCADE,
  creada_por  UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  creada_en   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT ck_encuesta_una_actividad CHECK ((curso_id IS NULL) <> (evento_id IS NULL))
);

-- Una sola encuesta por actividad
CREATE UNIQUE INDEX IF NOT EXISTS ux_encuesta_curso  ON encuestas (curso_id)  WHERE curso_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_encuesta_evento ON encuestas (evento_id) WHERE evento_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS encuesta_preguntas (
  id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  encuesta_id UUID NOT NULL REFERENCES encuestas(id) ON DELETE CASCADE,
  orden       SMALLINT NOT NULL CHECK (orden BETWEEN 1 AND 5),
  texto       VARCHAR(200) NOT NULL,
  -- ESCALA: puntaje de 1 a 5 (cuenta para el promedio de satisfacción). TEXTO: comentario libre
  tipo        VARCHAR(10) NOT NULL CHECK (tipo IN ('ESCALA','TEXTO')),
  UNIQUE (encuesta_id, orden)
);

-- Enlace personal de cada asistente: token aleatorio de 128 bits (no se puede adivinar)
CREATE TABLE IF NOT EXISTS encuesta_invitaciones (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  encuesta_id     UUID NOT NULL REFERENCES encuestas(id) ON DELETE CASCADE,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  token           VARCHAR(40) NOT NULL UNIQUE,
  enviada_en      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  respondida_en   TIMESTAMPTZ,
  UNIQUE (encuesta_id, participante_id)
);

CREATE INDEX IF NOT EXISTS ix_encuesta_invitaciones_participante
  ON encuesta_invitaciones (participante_id) WHERE respondida_en IS NULL;

CREATE TABLE IF NOT EXISTS encuesta_respuestas (
  id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  invitacion_id UUID NOT NULL REFERENCES encuesta_invitaciones(id) ON DELETE CASCADE,
  pregunta_id   UUID NOT NULL REFERENCES encuesta_preguntas(id) ON DELETE CASCADE,
  valor         SMALLINT CHECK (valor BETWEEN 1 AND 5),
  texto         VARCHAR(1000),
  CONSTRAINT ck_respuesta_con_contenido CHECK (valor IS NOT NULL OR texto IS NOT NULL),
  UNIQUE (invitacion_id, pregunta_id)
);

CREATE INDEX IF NOT EXISTS ix_encuesta_respuestas_pregunta ON encuesta_respuestas (pregunta_id);
