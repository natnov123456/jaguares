CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE TYPE rol_enum AS ENUM
  ('VISITANTE','PARTICIPANTE','DOCENTE','ENCARGADO_EVENTOS','VOLUNTARIO','DIRECTORA');

CREATE TYPE medio_pago_enum AS ENUM ('EFECTIVO','TRANSFERENCIA','QR','PASARELA');

CREATE TABLE usuarios (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  nombre_completo VARCHAR(150) NOT NULL,
  email           VARCHAR(150) NOT NULL UNIQUE,
  telefono        VARCHAR(30),
  password_hash   TEXT NOT NULL,
  rol             rol_enum NOT NULL DEFAULT 'PARTICIPANTE',
  activo          BOOLEAN NOT NULL DEFAULT TRUE,
  creado_en       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  actualizado_en  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE cursos (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  titulo          VARCHAR(200) NOT NULL,
  descripcion     TEXT,
  docente_id      UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  fecha_inicio    DATE NOT NULL,
  fecha_fin       DATE NOT NULL,
  cupo_maximo     INT NOT NULL CHECK (cupo_maximo > 0),
  cupo_disponible INT NOT NULL CHECK (cupo_disponible >= 0),
  asistencia_minima_pct INT NOT NULL DEFAULT 80,
  estado          VARCHAR(20) NOT NULL DEFAULT 'BORRADOR'
                  CHECK (estado IN ('BORRADOR','PUBLICADO','EN_CURSO','FINALIZADO','CANCELADO')),
  creado_en       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE inscripciones (
  id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  curso_id      UUID NOT NULL REFERENCES cursos(id) ON DELETE CASCADE,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  estado        VARCHAR(20) NOT NULL DEFAULT 'CONFIRMADA'
                CHECK (estado IN ('CONFIRMADA','CANCELADA','ASISTIO','NO_ASISTIO')),
  fecha_inscripcion TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (curso_id, participante_id)
);

CREATE TABLE eventos (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  titulo       VARCHAR(200) NOT NULL,
  descripcion  TEXT,
  lugar        VARCHAR(200),
  fecha_inicio TIMESTAMPTZ NOT NULL,
  fecha_fin    TIMESTAMPTZ,
  encargado_id UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  estado       VARCHAR(20) NOT NULL DEFAULT 'BORRADOR'
               CHECK (estado IN ('BORRADOR','PUBLICADO','EN_CURSO','FINALIZADO','CANCELADO')),
  creado_en    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE asistencias_evento (
  id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  evento_id     UUID NOT NULL REFERENCES eventos(id) ON DELETE CASCADE,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  registrado_en TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  registrado_por UUID REFERENCES usuarios(id),
  UNIQUE (evento_id, participante_id)
);

CREATE TABLE certificados (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  codigo_unico    VARCHAR(40) NOT NULL UNIQUE,
  curso_id        UUID REFERENCES cursos(id) ON DELETE SET NULL,
  participante_id UUID NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  nombre_participante VARCHAR(150) NOT NULL,
  nombre_curso    VARCHAR(200) NOT NULL,
  emitido_en      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  url_pdf         TEXT,
  valido          BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE donantes (
  id             UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  nombre         VARCHAR(150) NOT NULL,
  email          VARCHAR(150),
  telefono       VARCHAR(30),
  es_anonimo     BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE TABLE donaciones (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  donante_id   UUID REFERENCES donantes(id) ON DELETE SET NULL,
  monto        NUMERIC(12,2) NOT NULL CHECK (monto > 0),
  moneda       CHAR(3) NOT NULL DEFAULT 'BOB',
  medio        medio_pago_enum NOT NULL,
  referencia   VARCHAR(100),
  estado       VARCHAR(20) NOT NULL DEFAULT 'CONFIRMADA'
               CHECK (estado IN ('PENDIENTE','CONFIRMADA','ANULADA')),
  registrado_por UUID REFERENCES usuarios(id),
  fecha        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE voluntarios (
  id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  usuario_id      UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  nombre_completo VARCHAR(150) NOT NULL,
  email           VARCHAR(150) NOT NULL UNIQUE,
  telefono        VARCHAR(30),
  estado          VARCHAR(20) NOT NULL DEFAULT 'ACTIVO'
                  CHECK (estado IN ('ACTIVO','INACTIVO','PENDIENTE')),
  postulado_en    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE participaciones_voluntario (
  id            UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  voluntario_id UUID NOT NULL REFERENCES voluntarios(id) ON DELETE CASCADE,
  evento_id     UUID REFERENCES eventos(id) ON DELETE SET NULL,
  descripcion   VARCHAR(300),
  fecha         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE noticias (
  id           UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  titulo       VARCHAR(200) NOT NULL,
  resumen      VARCHAR(400),
  contenido    TEXT NOT NULL,
  imagen_url   TEXT,
  publicada    BOOLEAN NOT NULL DEFAULT FALSE,
  autor_id     UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  publicado_en TIMESTAMPTZ,
  creado_en    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE galeria (
  id         UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  titulo     VARCHAR(200),
  imagen_url TEXT NOT NULL,
  orden      INT NOT NULL DEFAULT 0,
  creado_en  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE notificaciones_log (
  id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  destinatario VARCHAR(150) NOT NULL,
  canal       VARCHAR(20) NOT NULL CHECK (canal IN ('EMAIL','WHATSAPP')),
  asunto      VARCHAR(200),
  contenido   TEXT,
  enviado_en  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  exito       BOOLEAN NOT NULL DEFAULT TRUE
);
