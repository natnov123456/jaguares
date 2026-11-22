-- Seguridad de la base de datos: usuario propio de la aplicación con el mínimo de permisos.
-- La API se conecta con "jaguares_app" (no con el dueño de la base):
--   * puede leer, crear, modificar y borrar datos de las tablas de la plataforma;
--   * NO puede crear, modificar ni borrar tablas, ni desactivar los triggers de solo lectura;
--   * en las tablas de constancias (registro de actividad, consentimientos, auditoría de donaciones y
--     exportaciones) solo puede leer y agregar: no puede modificar ni borrar.
-- Idempotente. La contraseña NO va en este archivo: se define aparte, una sola vez, con
--   ALTER ROLE jaguares_app LOGIN PASSWORD '...';
-- y se usa en la cadena de conexión de la API (ConnectionStrings__Default).

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'jaguares_app') THEN
    CREATE ROLE jaguares_app NOLOGIN;
  END IF;
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO jaguares_app', current_database());
END $$;

GRANT USAGE ON SCHEMA public TO jaguares_app;
REVOKE CREATE ON SCHEMA public FROM jaguares_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO jaguares_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO jaguares_app;

-- Constancias: solo lectura y alta
REVOKE UPDATE, DELETE, TRUNCATE ON registro_actividad, consentimientos, auditoria_donaciones, exportaciones FROM jaguares_app;

-- Tablas y secuencias que creen los próximos scripts (los ejecuta el dueño de la base)
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO jaguares_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO jaguares_app;
