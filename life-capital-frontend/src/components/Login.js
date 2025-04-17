// components/Login.js
import React, { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { Form, Button, Container, Row, Col, Alert } from "react-bootstrap";
import "bootstrap/dist/css/bootstrap.min.css";
import { useAuth } from "../context/AuthContext";

const Login = () => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [errorMessage, setErrorMessage] = useState("");
  const navigate = useNavigate();
  const { login } = useAuth();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setErrorMessage("");

    const result = await login(username, password);
    if (result.success) {
      navigate("/");
    } else {
      setErrorMessage(result.message);
    }
  };

  const generateRandomState = () => Math.random().toString(36).substring(2, 15);

  const handleOAuthLogin = () => {
    const state = generateRandomState();
    const params = new URLSearchParams({
      response_type: "code",
      client_id: "2076c247bac9",
      redirect_uri: `https://${window.location.host}/oauth/callback`,
      scope: "profile",
      state,
    });
    window.location.href = `https://mail.lifecapital.eg/oauth/authorize?${params.toString()}`;
  };

  return (
    <Container className="d-flex justify-content-center align-items-center min-vh-100">
      <Row className="w-100 justify-content-center">
        <Col md={6} lg={4}>
          <h2 className="text-center mb-4">Login</h2>
          {errorMessage && <Alert variant="danger">{errorMessage}</Alert>}

          <Form onSubmit={handleSubmit}>
            <Form.Group className="mb-3" controlId="formUsername">
              <Form.Label>Username</Form.Label>
              <Form.Control
                type="text"
                placeholder="Enter your username"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
              />
            </Form.Group>

            <Form.Group className="mb-3" controlId="formPassword">
              <Form.Label>Password</Form.Label>
              <Form.Control
                type="password"
                placeholder="Enter your password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </Form.Group>

            <Button variant="primary" type="submit" className="w-100">
              Login
            </Button>
          </Form>

          <div className="text-center mt-3">
            <Button
              variant="outline-secondary"
              className="w-100"
              onClick={handleOAuthLogin}
            >
              Login with Life Capital
            </Button>
          </div>

          <div className="text-center mt-3">
            Don't have an account? <Link to="/signup">Sign Up</Link>
          </div>
        </Col>
      </Row>
    </Container>
  );
};

export default Login;
