-- JAG-73 / FUJ-126: galería multimedia institucional organizada en álbumes
-- Amplía la tabla galeria existente: cada elemento pertenece a un álbum y puede ser una
-- imagen subida (con miniatura) o un enlace de video (YouTube / Vimeo).
-- Script idempotente: se puede ejecutar más de una vez.

CREATE TABLE IF NOT EXISTS albumes (
  id             UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  titulo         VARCHAR(200) NOT NULL,
  descripcion    TEXT,
  publicado      BOOLEAN NOT NULL DEFAULT FALSE,
  creado_por     UUID REFERENCES usuarios(id) ON DELETE SET NULL,
  creado_en      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  actualizado_en TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE galeria ADD COLUMN IF NOT EXISTS album_id      UUID REFERENCES albumes(id) ON DELETE CASCADE;
ALTER TABLE galeria ADD COLUMN IF NOT EXISTS tipo          VARCHAR(10) NOT NULL DEFAULT 'IMAGEN';
ALTER TABLE galeria ADD COLUMN IF NOT EXISTS miniatura_url TEXT;
ALTER TABLE galeria ADD COLUMN IF NOT EXISTS video_url     TEXT;
ALTER TABLE galeria ALTER COLUMN imagen_url DROP NOT NULL;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_galeria_tipo') THEN
    ALTER TABLE galeria ADD CONSTRAINT ck_galeria_tipo CHECK (
      (tipo = 'IMAGEN' AND imagen_url IS NOT NULL) OR (tipo = 'VIDEO' AND video_url IS NOT NULL));
  END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_galeria_album ON galeria (album_id, orden);
