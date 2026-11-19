-- JAG-86 / FUJ-124: registro de cada reporte exportado (PDF / Excel) y de los errores al generarlo
-- (corrección de la evaluación: "log de cada PDF generado con usuario, fecha y filtros usados, y de los errores de render").
-- No guarda el archivo ni datos personales de quien lo pidió: solo su id y su rol.
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS exportaciones (
  id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  usuario_id  UUID         NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  rol         VARCHAR(40)  NOT NULL,
  reporte     VARCHAR(60)  NOT NULL,
  formato     VARCHAR(10)  NOT NULL,
  filtros     JSONB        NOT NULL DEFAULT '{}'::jsonb,
  filas       INT          NOT NULL DEFAULT 0,
  exito       BOOLEAN      NOT NULL,
  error       VARCHAR(500),
  duracion_ms INT          NOT NULL DEFAULT 0,
  creado_en   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_exportaciones_fecha ON exportaciones (creado_en DESC);
CREATE INDEX IF NOT EXISTS ix_exportaciones_usuario ON exportaciones (usuario_id, creado_en DESC);
