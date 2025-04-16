import React from "react";
import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import OAuthHandler from "./components/OAuthHandler";
import HomePage from "./components/HomePage";
import Login from "./components/Login";
import Signup from "./components/Signup";
import ListUsers from "./components/ListUsers";
import UserManagement from "./components/UserManagement";

function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/login" element={<Login isLogin={true} />} />
        <Route path="/signup" element={<Signup isLogin={false} />} />
        <Route path="/oauth/callback" element={<OAuthHandler />} />
        <Route path="/listUsers" element={<ListUsers />} />
        <Route path="/user-management/:id" element={<UserManagement />} />
      </Routes>
    </Router>
  );
}

export default App;
