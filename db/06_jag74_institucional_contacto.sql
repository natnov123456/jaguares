-- JAG-74 / FUJ-127: página "Quiénes somos" (editable por la Directora) y formulario de contacto
-- Script idempotente: se puede ejecutar más de una vez.

-- Una sola fila (id = 1) con el contenido institucional
CREATE TABLE IF NOT EXISTS pagina_institucional (
  id              INT PRIMARY KEY DEFAULT 1 CHECK (id = 1),
  mision          TEXT,
  vision          TEXT,
  historia        TEXT,
  equipo          JSONB NOT NULL DEFAULT '[]'::jsonb, -- [{ "nombre", "cargo", "descripcion" }]
  direccion       VARCHAR(300),
  telefono        VARCHAR(50),
  email_contacto  VARCHAR(150),                       -- correo institucional que recibe las consultas
  horario         VARCHAR(200),
  actualizado_por UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  actualizado_en  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO pagina_institucional (id, mision, vision, historia, direccion, horario)
VALUES (1,
  'Empoderar a las comunidades de La Paz a través de talleres, capacitaciones y voluntariado activo, con el espíritu del jaguar: fuerte, ágil y protector de su territorio.',
  'Ser un referente en conservación, educación y desarrollo comunitario en Bolivia.',
  'La Fundación Unión de los Jaguares nació en La Paz para unir a personas comprometidas con su comunidad. La Directora puede completar esta historia desde el panel.',
  'La Paz, Bolivia',
  'Lunes a viernes, 9:00 a 17:00')
ON CONFLICT (id) DO NOTHING;

-- Registro de consultas: sirve de respaldo si el correo falla y para limitar envíos por IP (anti-spam)
CREATE TABLE IF NOT EXISTS consultas_contacto (
  id        UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  nombre    VARCHAR(150) NOT NULL,
  email     VARCHAR(150) NOT NULL,
  asunto    VARCHAR(150),
  mensaje   TEXT NOT NULL,
  ip        VARCHAR(64),
  enviada   BOOLEAN NOT NULL DEFAULT FALSE,
  creado_en TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_consultas_contacto_ip_fecha ON consultas_contacto (ip, creado_en);
