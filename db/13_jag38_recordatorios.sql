-- JAG-38 / FUJ-119: recordatorios automáticos 24 h antes de cada actividad
-- Marca cuándo se envió el recordatorio a cada inscrito para no enviarlo dos veces.
-- Si la actividad se reprograma, la marca vuelve a NULL y se envía un recordatorio para la fecha nueva.
-- Script idempotente: se puede ejecutar más de una vez.

ALTER TABLE inscripciones        ADD COLUMN IF NOT EXISTS recordatorio_enviado_en TIMESTAMPTZ;
ALTER TABLE inscripciones_evento ADD COLUMN IF NOT EXISTS recordatorio_enviado_en TIMESTAMPTZ;

-- La tarea programada solo busca inscripciones confirmadas que todavía no recibieron su recordatorio
CREATE INDEX IF NOT EXISTS ix_inscripciones_recordatorio_pendiente
  ON inscripciones (curso_id) WHERE estado = 'CONFIRMADA' AND recordatorio_enviado_en IS NULL;
CREATE INDEX IF NOT EXISTS ix_inscripciones_evento_recordatorio_pendiente
  ON inscripciones_evento (evento_id) WHERE estado = 'CONFIRMADA' AND recordatorio_enviado_en IS NULL;
