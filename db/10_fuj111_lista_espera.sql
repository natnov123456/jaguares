-- FUJ-111 (JAG-30): lista de espera automática en cursos llenos
-- Cuando un curso no tiene cupos, el participante se anota en la lista de espera.
-- Al cancelar un inscrito, el primero de la lista (por orden de llegada) queda inscrito
-- automáticamente y recibe una notificación.
-- La posición no se guarda: se calcula por orden de llegada entre los que siguen EN_ESPERA,
-- así nunca queda desactualizada cuando alguien sale de la lista.
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS lista_espera (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  curso_id        UUID NOT NULL REFERENCES cursos(id) ON DELETE CASCADE,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  estado          VARCHAR(20) NOT NULL DEFAULT 'EN_ESPERA'
                  CHECK (estado IN ('EN_ESPERA','PROMOVIDO','RETIRADO')),
  creado_en       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  promovido_en    TIMESTAMPTZ
);

-- Una persona solo puede estar una vez EN_ESPERA por curso (el historial de salidas se conserva)
CREATE UNIQUE INDEX IF NOT EXISTS ux_lista_espera_activa
  ON lista_espera (curso_id, participante_id) WHERE estado = 'EN_ESPERA';

-- Orden de la cola: el primero en llegar es el primero en ser promovido
CREATE INDEX IF NOT EXISTS ix_lista_espera_cola
  ON lista_espera (curso_id, creado_en) WHERE estado = 'EN_ESPERA';

CREATE INDEX IF NOT EXISTS ix_lista_espera_participante
  ON lista_espera (participante_id);
