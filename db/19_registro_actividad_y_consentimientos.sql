-- Registro de actividad y consentimientos.
--
-- registro_actividad: una fila por cada acción en la plataforma (quién, cuándo, desde qué IP y
-- navegador, qué hizo y con qué resultado), incluidos los inicios y cierres de sesión y los intentos
-- fallidos. No guarda contraseñas ni el contenido de los formularios. Es de SOLO LECTURA: un trigger
-- rechaza cualquier UPDATE, DELETE o TRUNCATE, igual que la bitácora de donaciones.
--
-- consentimientos: cuándo y cómo cada persona aceptó los Términos y condiciones y la Política de
-- privacidad (formulario, versión del documento, IP y navegador). También es de solo lectura.
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS registro_actividad (
  id           BIGSERIAL    PRIMARY KEY,
  fecha        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  usuario_id   UUID,                         -- sin clave foránea: el registro conserva el id aunque la cuenta se borre
  email        VARCHAR(150),                 -- de la sesión, o el correo con el que se intentó entrar
  rol          VARCHAR(40),
  accion       VARCHAR(120) NOT NULL,        -- descripción legible: "Inicio de sesión", "Inscripción a un curso"...
  metodo       VARCHAR(10)  NOT NULL,
  ruta         VARCHAR(300) NOT NULL,        -- plantilla de la ruta (sin tokens)
  recurso      VARCHAR(300),                 -- identificadores de la ruta y filtros, si los hay
  estado       SMALLINT     NOT NULL,        -- código HTTP de la respuesta
  exito        BOOLEAN      NOT NULL,
  ip           VARCHAR(45),
  navegador    VARCHAR(300),
  duracion_ms  INTEGER
);

CREATE INDEX IF NOT EXISTS ix_registro_actividad_fecha   ON registro_actividad (fecha DESC);
CREATE INDEX IF NOT EXISTS ix_registro_actividad_usuario ON registro_actividad (usuario_id, fecha DESC);
CREATE INDEX IF NOT EXISTS ix_registro_actividad_email   ON registro_actividad (lower(email));

CREATE TABLE IF NOT EXISTS consentimientos (
  id           UUID         PRIMARY KEY DEFAULT uuid_generate_v4(),
  usuario_id   UUID,                         -- sin clave foránea: el registro conserva el id aunque la cuenta se borre
  email        VARCHAR(150),
  formulario   VARCHAR(40)  NOT NULL,        -- REGISTRO | VOLUNTARIADO | DONACION | CONTACTO
  documento    VARCHAR(20)  NOT NULL CHECK (documento IN ('TERMINOS','PRIVACIDAD')),
  version      VARCHAR(20)  NOT NULL,        -- versión del documento aceptado (fecha de publicación)
  aceptado_en  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  ip           VARCHAR(45),
  navegador    VARCHAR(300)
);

CREATE INDEX IF NOT EXISTS ix_consentimientos_email   ON consentimientos (lower(email));
CREATE INDEX IF NOT EXISTS ix_consentimientos_usuario ON consentimientos (usuario_id);

CREATE OR REPLACE FUNCTION registro_solo_lectura() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION 'Este registro es de solo lectura: no se puede modificar ni borrar'
    USING ERRCODE = 'insufficient_privilege';
END;
$$ LANGUAGE plpgsql;

DO $$
DECLARE t TEXT;
BEGIN
  FOREACH t IN ARRAY ARRAY['registro_actividad', 'consentimientos'] LOOP
    EXECUTE format('DROP TRIGGER IF EXISTS tr_%s_solo_lectura ON %I', t, t);
    EXECUTE format('CREATE TRIGGER tr_%s_solo_lectura BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION registro_solo_lectura()', t, t);
    EXECUTE format('DROP TRIGGER IF EXISTS tr_%s_sin_truncate ON %I', t, t);
    EXECUTE format('CREATE TRIGGER tr_%s_sin_truncate BEFORE TRUNCATE ON %I FOR EACH STATEMENT EXECUTE FUNCTION registro_solo_lectura()', t, t);
  END LOOP;
END $$;
