-- JAG-81 / FUJ-122: usuarios internos, roles y enlace para definir la contraseña
-- La Directora crea las cuentas del personal y les asigna un rol. Cada persona recibe por correo un
-- enlace de un solo uso (válido 24 h) para definir su contraseña.
-- Cada usuario tiene un único rol (usuarios.rol); la tabla "roles" es el catálogo con el nombre visible
-- y la descripción de lo que puede hacer cada uno.
-- Script idempotente: se puede ejecutar más de una vez.

-- Roles nuevos del personal (Enc. de operaciones = ENCARGADO_EVENTOS, ya existente)
ALTER TYPE rol_enum ADD VALUE IF NOT EXISTS 'CONTADOR';
ALTER TYPE rol_enum ADD VALUE IF NOT EXISTS 'COORDINADOR_VOLUNTARIADO';

CREATE TABLE IF NOT EXISTS roles (
  codigo      VARCHAR(40)  PRIMARY KEY,
  nombre      VARCHAR(80)  NOT NULL,
  descripcion VARCHAR(300) NOT NULL,
  interno     BOOLEAN      NOT NULL          -- TRUE: personal de la fundación (la Directora lo asigna)
);

INSERT INTO roles (codigo, nombre, descripcion, interno) VALUES
  ('DIRECTORA',                'Dirección',                     'Acceso completo: usuarios, indicadores, cursos, eventos, finanzas y contenidos.', TRUE),
  ('CONTADOR',                 'Contador/a',                    'Donaciones, reporte financiero, campañas, transparencia y bitácora de auditoría.', TRUE),
  ('ENCARGADO_EVENTOS',        'Encargado/a de operaciones',    'Eventos, check-in QR, asistencia, voluntariado y registro de efectivo.', TRUE),
  ('COORDINADOR_VOLUNTARIADO', 'Coordinador/a de voluntariado', 'Registro de voluntarios, asignaciones a actividades y horas.', TRUE),
  ('DOCENTE',                  'Docente',                       'Los cursos que la Dirección le asigna: asistencia, datos y certificados.', TRUE),
  ('PARTICIPANTE',             'Participante',                  'Inscripción a cursos y eventos, su panel y sus certificados.', FALSE),
  ('VOLUNTARIO',               'Voluntario/a',                  'Su portal de voluntariado, asignaciones y horas.', FALSE)
ON CONFLICT (codigo) DO UPDATE
  SET nombre = EXCLUDED.nombre, descripcion = EXCLUDED.descripcion, interno = EXCLUDED.interno;

-- Enlaces para definir la contraseña: se guarda solo el hash SHA-256 del token (el token viaja únicamente en el correo)
CREATE TABLE IF NOT EXISTS tokens_contrasena (
  id         UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  usuario_id UUID        NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  token_hash CHAR(64)    NOT NULL UNIQUE,
  expira_en  TIMESTAMPTZ NOT NULL,
  usado_en   TIMESTAMPTZ,
  creado_en  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_tokens_contrasena_usuario ON tokens_contrasena (usuario_id);
