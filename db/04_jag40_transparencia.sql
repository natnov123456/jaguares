-- JAG-40 / FUJ-121: sección pública de transparencia sobre el uso de las donaciones
-- La Directora publica el resumen de un mes a partir del reporte financiero (FUJ-106):
-- se guarda una foto del total recaudado y su destino por categoría, SIN datos de donantes.
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS reportes_transparencia (
  id                  UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  anio                INT NOT NULL CHECK (anio BETWEEN 2000 AND 2100),
  mes                 INT NOT NULL CHECK (mes BETWEEN 1 AND 12),
  total_recaudado     NUMERIC(12,2) NOT NULL CHECK (total_recaudado >= 0),
  cantidad_donaciones INT NOT NULL CHECK (cantidad_donaciones >= 0),
  nota                VARCHAR(500),
  publicado_por       UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  publicado_en        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (anio, mes)
);

CREATE TABLE IF NOT EXISTS transparencia_destinos (
  id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  reporte_id  UUID NOT NULL REFERENCES reportes_transparencia(id) ON DELETE CASCADE,
  categoria   VARCHAR(80) NOT NULL,
  monto       NUMERIC(12,2) NOT NULL CHECK (monto >= 0),
  descripcion VARCHAR(300),
  UNIQUE (reporte_id, categoria)
);
