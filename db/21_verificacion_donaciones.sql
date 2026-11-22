-- Donación en línea con verificación: el donante paga por QR o transferencia, sube la foto/PDF del comprobante
-- del banco y el número de operación; la donación queda PENDIENTE hasta que Contabilidad o la Dirección
-- confirma en el extracto del banco que el dinero llegó (CONFIRMADA) o la rechaza (RECHAZADA).
-- Solo las CONFIRMADAS suman en reportes, campañas y transparencia, y solo ellas tienen comprobante de donación.
-- Ya no hay pasarela de pago. Idempotente.

-- 1) Estados y datos de la verificación
ALTER TABLE donaciones DROP CONSTRAINT IF EXISTS donaciones_estado_check;
ALTER TABLE donaciones ADD CONSTRAINT donaciones_estado_check
  CHECK (estado IN ('PENDIENTE','CONFIRMADA','RECHAZADA','ANULADA'));
ALTER TABLE donaciones ADD COLUMN IF NOT EXISTS verificado_por UUID REFERENCES usuarios(id) ON DELETE SET NULL;
ALTER TABLE donaciones ADD COLUMN IF NOT EXISTS verificado_en  TIMESTAMPTZ;
ALTER TABLE donaciones ADD COLUMN IF NOT EXISTS motivo_rechazo VARCHAR(300);

-- Un mismo número de operación del banco no se puede usar en dos donaciones (salvo las rechazadas)
CREATE UNIQUE INDEX IF NOT EXISTS ux_donaciones_operacion_banco
  ON donaciones (medio, lower(referencia))
  WHERE medio IN ('QR','TRANSFERENCIA') AND referencia IS NOT NULL AND estado <> 'RECHAZADA' AND registrado_por IS NULL;

CREATE INDEX IF NOT EXISTS ix_donaciones_pendientes ON donaciones (fecha) WHERE estado = 'PENDIENTE';

-- 2) Comprobante de pago que sube el donante. Va en la base de datos (privado, solo lo ven Contabilidad y la
--    Dirección) y no en /uploads, que es público y en el plan gratuito de la nube se borra en cada despliegue.
CREATE TABLE IF NOT EXISTS comprobantes_pago (
  donacion_id  UUID PRIMARY KEY REFERENCES donaciones(id) ON DELETE CASCADE,
  contenido    BYTEA        NOT NULL,
  tipo         VARCHAR(50)  NOT NULL CHECK (tipo IN ('image/jpeg','image/png','application/pdf')),
  nombre       VARCHAR(200) NOT NULL,
  creado_en    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- 3) La bitácora de auditoría registra también la verificación y el rechazo
ALTER TABLE auditoria_donaciones DROP CONSTRAINT IF EXISTS auditoria_donaciones_accion_check;
ALTER TABLE auditoria_donaciones ADD CONSTRAINT auditoria_donaciones_accion_check
  CHECK (accion IN ('ALTA','EDICION','ANULACION','VERIFICACION','RECHAZO'));

-- 4) Datos para pagar que la Dirección carga en "Quiénes somos": imagen del QR y cuenta bancaria
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS qr_donacion        BYTEA;
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS qr_donacion_tipo   VARCHAR(50);
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS banco_nombre       VARCHAR(100);
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS banco_tipo_cuenta  VARCHAR(50);
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS banco_cuenta       VARCHAR(50);
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS banco_titular      VARCHAR(150);
ALTER TABLE pagina_institucional ADD COLUMN IF NOT EXISTS banco_documento    VARCHAR(30);

-- 5) Permisos del usuario de la aplicación (si existe): la tabla nueva queda con lectura y escritura
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jaguares_app') THEN
    GRANT SELECT, INSERT, UPDATE, DELETE ON comprobantes_pago TO jaguares_app;
  END IF;
END $$;
