-- JAG-32 / FUJ-113: check-in a eventos con código QR
-- Cada participante que se inscribe a un evento recibe un código QR único para ese evento.
-- El check-in valida el código y crea la fila en asistencias_evento (que ya impide duplicados).
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS inscripciones_evento (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  evento_id       UUID NOT NULL REFERENCES eventos(id) ON DELETE CASCADE,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  -- Token aleatorio de 128 bits en base64url: no se puede adivinar ni derivar del id
  codigo_qr       VARCHAR(40) NOT NULL UNIQUE,
  estado          VARCHAR(20) NOT NULL DEFAULT 'CONFIRMADA'
                  CHECK (estado IN ('CONFIRMADA','CANCELADA','ASISTIO')),
  inscrito_en     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  checkin_en      TIMESTAMPTZ,
  UNIQUE (evento_id, participante_id)
);

CREATE INDEX IF NOT EXISTS ix_inscripciones_evento_participante
  ON inscripciones_evento (participante_id);
