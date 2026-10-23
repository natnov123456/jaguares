-- Usuarios de prueba (uno por rol). Contraseñas en README.md.
-- crypt(..., gen_salt('bf')) genera hashes BCrypt compatibles con BCrypt.Net del backend.
-- Se puede ejecutar varias veces: si el email ya existe, actualiza nombre, rol y contraseña.

INSERT INTO usuarios (nombre_completo, email, telefono, password_hash, rol) VALUES
  ('María Directora',      'directora@jaguares.org',  '70000001', crypt('Admin123!',   gen_salt('bf', 11)), 'DIRECTORA'),
  ('Carlos Docente',       'docente@jaguares.org',    '70000002', crypt('Docente123!', gen_salt('bf', 11)), 'DOCENTE'),
  ('Elena Encargada',      'eventos@jaguares.org',    '70000003', crypt('Eventos123!', gen_salt('bf', 11)), 'ENCARGADO_EVENTOS'),
  ('Valeria Voluntaria',   'voluntario@example.com',  '70000004', crypt('Vol123!',     gen_salt('bf', 11)), 'VOLUNTARIO'),
  ('Juan Pérez',           'juan@example.com',        '70000005', crypt('Juan123!',    gen_salt('bf', 11)), 'PARTICIPANTE')
ON CONFLICT (email) DO UPDATE
  SET nombre_completo = EXCLUDED.nombre_completo,
      telefono        = EXCLUDED.telefono,
      password_hash   = EXCLUDED.password_hash,
      rol             = EXCLUDED.rol,
      activo          = TRUE;

-- El usuario voluntario también figura en el registro único de voluntarios (RF-07)
INSERT INTO voluntarios (usuario_id, nombre_completo, email, telefono, estado)
SELECT id, nombre_completo, email, telefono, 'ACTIVO'
FROM usuarios WHERE email = 'voluntario@example.com'
ON CONFLICT (email) DO NOTHING;
