-- JAG-88 / FUJ-125: bitácora de auditoría de las operaciones sobre donaciones
-- Cada alta, edición o anulación de una donación guarda exactamente un registro con quién la hizo,
-- cuándo, la acción y los valores anterior y nuevo (JSON).
-- La bitácora es de SOLO LECTURA: un trigger rechaza cualquier UPDATE, DELETE o TRUNCATE,
-- sin importar con qué usuario de base de datos se conecte la aplicación.
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS auditoria_donaciones (
  id                 UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  donacion_id        UUID         NOT NULL REFERENCES donaciones(id) ON DELETE RESTRICT,
  numero_comprobante BIGINT       NOT NULL,
  accion             VARCHAR(20)  NOT NULL CHECK (accion IN ('ALTA','EDICION','ANULACION')),
  usuario_id         UUID         REFERENCES usuarios(id) ON DELETE SET NULL,  -- NULL: donación en línea sin sesión
  usuario_nombre     VARCHAR(150) NOT NULL,
  rol                VARCHAR(40),
  motivo             VARCHAR(300),
  valor_anterior     JSONB,                                                     -- NULL en el alta
  valor_nuevo        JSONB        NOT NULL,
  creado_en          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_auditoria_donaciones_fecha    ON auditoria_donaciones (creado_en DESC);
CREATE INDEX IF NOT EXISTS ix_auditoria_donaciones_donacion ON auditoria_donaciones (donacion_id);
CREATE INDEX IF NOT EXISTS ix_auditoria_donaciones_usuario  ON auditoria_donaciones (usuario_id);

CREATE OR REPLACE FUNCTION auditoria_donaciones_solo_lectura() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION 'La bitácora de auditoría es de solo lectura: no se puede modificar ni borrar'
    USING ERRCODE = 'insufficient_privilege';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_auditoria_donaciones_solo_lectura ON auditoria_donaciones;
CREATE TRIGGER tr_auditoria_donaciones_solo_lectura
  BEFORE UPDATE OR DELETE ON auditoria_donaciones
  FOR EACH ROW EXECUTE FUNCTION auditoria_donaciones_solo_lectura();

DROP TRIGGER IF EXISTS tr_auditoria_donaciones_sin_truncate ON auditoria_donaciones;
CREATE TRIGGER tr_auditoria_donaciones_sin_truncate
  BEFORE TRUNCATE ON auditoria_donaciones
  FOR EACH STATEMENT EXECUTE FUNCTION auditoria_donaciones_solo_lectura();
