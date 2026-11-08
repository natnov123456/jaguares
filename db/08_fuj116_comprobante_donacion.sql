-- FUJ-116: comprobante digital de donación
-- Script idempotente: se puede ejecutar más de una vez.

-- Número correlativo del comprobante (las donaciones existentes también reciben el suyo)
ALTER TABLE donaciones
  ADD COLUMN IF NOT EXISTS numero_comprobante BIGINT GENERATED ALWAYS AS IDENTITY;

CREATE UNIQUE INDEX IF NOT EXISTS ux_donaciones_numero_comprobante ON donaciones (numero_comprobante);

-- Cuenta del donante: permite volver a descargar sus comprobantes con sesión iniciada
ALTER TABLE donaciones
  ADD COLUMN IF NOT EXISTS usuario_id UUID REFERENCES usuarios(id) ON DELETE SET NULL;

-- Null mientras el comprobante no se haya podido enviar por correo
ALTER TABLE donaciones
  ADD COLUMN IF NOT EXISTS comprobante_enviado_en TIMESTAMPTZ;

CREATE INDEX IF NOT EXISTS ix_donaciones_usuario_fecha ON donaciones (usuario_id, fecha DESC);
