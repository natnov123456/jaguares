-- FUJ-115: plantillas configurables de certificados (logo, firmas y texto) por tipo de actividad
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS plantillas_certificado (
  id               UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  nombre           VARCHAR(120) NOT NULL,
  tipo_actividad   VARCHAR(20)  NOT NULL
                   CHECK (tipo_actividad IN ('TALLER','CAPACITACION','VOLUNTARIADO','EVENTO','OTRO')),
  titulo           VARCHAR(120) NOT NULL DEFAULT 'CERTIFICADO',
  texto            TEXT         NOT NULL,  -- admite {participante}, {actividad}, {fecha} y {codigo}
  logo_url         TEXT,
  firma_uno_url    TEXT,
  firma_uno_nombre VARCHAR(120),
  firma_uno_cargo  VARCHAR(120),
  firma_dos_url    TEXT,
  firma_dos_nombre VARCHAR(120),
  firma_dos_cargo  VARCHAR(120),
  creado_en        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  actualizado_en   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_plantillas_certificado_nombre ON plantillas_certificado (lower(nombre));

-- Plantilla asignada a cada curso. Sin plantilla, el certificado usa el diseño estándar.
ALTER TABLE cursos
  ADD COLUMN IF NOT EXISTS plantilla_id UUID REFERENCES plantillas_certificado(id) ON DELETE SET NULL;

-- Plantilla con la que se emitió cada certificado (el PDF ya generado no cambia si luego se edita la plantilla)
ALTER TABLE certificados
  ADD COLUMN IF NOT EXISTS plantilla_id UUID REFERENCES plantillas_certificado(id) ON DELETE SET NULL;
