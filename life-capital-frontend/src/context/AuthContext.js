// context/AuthContext.js
import React, { createContext, useContext, useEffect, useState } from "react";
import { CONSTANTS } from "../constants";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState({
    role: null,
    username: null,
    loading: true,
  });

  const fetchUser = async () => {
    try {
      const res = await fetch(
        `${CONSTANTS.api_base_url}/auth/token/verify-refresh`,
        {
          method: "POST",
          credentials: "include",
        }
      );

      if (res.ok) {
        const data = await res.json();
        setUser({ role: data.role, username: data.username, loading: false });
      } else {
        setUser({ role: null, username: null, loading: false });
      }
    } catch (err) {
      console.error("Failed to fetch user", err);
      setUser({ role: null, username: null, loading: false });
    }
  };

  const login = async (username, password) => {
    try {
      const res = await fetch(`${CONSTANTS.api_base_url}/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ username, password }),
      });

      if (res.ok) {
        await fetchUser();
        return { success: true };
      } else {
        const data = await res.json();
        return {
          success: false,
          message: data.message || "Failed to authenticate",
        };
      }
    } catch {
      return { success: false, message: "An unexpected error occurred" };
    }
  };

  useEffect(() => {
    fetchUser();
  }, []);

  return (
    <AuthContext.Provider value={{ ...user, login }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);
