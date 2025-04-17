import React from "react";
import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import OAuthHandler from "./components/OAuthHandler";
import HomePage from "./components/HomePage";
import Login from "./components/Login";
import Signup from "./components/Signup";
import ListUsers from "./components/ListUsers";
import UserManagement from "./components/UserManagement";
import AuthRoute from "./components/AuthRoute";

function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/login" element={<Login />} />
          <Route path="/signup" element={<Signup />} />
          <Route path="/oauth/callback" element={<OAuthHandler />} />
          <Route
            path="/listUsers"
            element={
              <AuthRoute allowedRoles={["admin"]}>
                <ListUsers />
              </AuthRoute>
            }
          />
          <Route
            path="/user-management/:id"
            element={
              <AuthRoute allowedRoles={["admin"]}>
                <UserManagement />
              </AuthRoute>
            }
          />
        </Routes>
      </Router>
    </AuthProvider>
  );
}

export default App;
