import { createContext, useContext, useState, useEffect } from 'react';
import { authApi } from '../services/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('token');
    const user = localStorage.getItem('usuario');
    if (token && user) {
      try {
        const parsed = JSON.parse(user);
        if (parsed?.rol) setUsuario(parsed);
        else throw new Error('usuario inválido');
      } catch {
        localStorage.removeItem('token');
        localStorage.removeItem('usuario');
      }
    }
    setLoading(false);
  }, []);

  const login = async (email, password) => {
    const { data } = await authApi.login(email, password);
    localStorage.setItem('token', data.token);
    const user = { nombreCompleto: data.nombreCompleto, rol: data.rol, email: email.trim().toLowerCase() };
    localStorage.setItem('usuario', JSON.stringify(user));
    setUsuario(user);
    return user;
  };

  const logout = () => {
    // Queda en el registro de actividad; si falla (sin conexión, sesión vencida) se cierra igual
    const token = localStorage.getItem('token');
    if (token) authApi.salir(token).catch(() => {});
    localStorage.removeItem('token');
    localStorage.removeItem('usuario');
    setUsuario(null);
  };

  return (
    <AuthContext.Provider value={{ usuario, login, logout, loading }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  return useContext(AuthContext);
}