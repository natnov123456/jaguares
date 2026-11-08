-- FUJ-117: campañas de donación con meta y progreso
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS campanas (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  nombre       VARCHAR(150)  NOT NULL,
  descripcion  TEXT,
  meta         NUMERIC(12,2) NOT NULL CHECK (meta > 0),
  fecha_limite DATE          NOT NULL,
  activa       BOOLEAN       NOT NULL DEFAULT TRUE, -- la Directora puede ocultarla antes de la fecha límite
  creado_por   UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  creado_en    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

-- Lo recaudado se calcula sumando las donaciones confirmadas de la campaña
ALTER TABLE donaciones
  ADD COLUMN IF NOT EXISTS campana_id UUID REFERENCES campanas(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_donaciones_campana ON donaciones (campana_id) WHERE campana_id IS NOT NULL;
