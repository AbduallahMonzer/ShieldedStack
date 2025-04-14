import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { Container, Spinner, Alert, Row, Col, Card } from "react-bootstrap";
import { CONSTANTS } from "../constants";
import NavbarComponent from "./NavbarComponent";

const HomePage = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const navigate = useNavigate();

  const onInvalidToken = useCallback(() => {
    navigate("/login");
  }, [navigate]);

  useEffect(() => {
    const verifyToken = async () => {
      let validToken = false;

      try {
        const response = await fetch(
          `${CONSTANTS.api_base_url}/auth/token/verify`,
          {
            method: "POST",
            credentials: "include",
          }
        );

        if (response.ok) {
          validToken = true;
        }
      } catch (err) {
        console.error(err);
        setError("Something went wrong.");
      } finally {
        setLoading(false);

        if (!validToken) onInvalidToken();
      }
    };

    verifyToken();
  }, [onInvalidToken]);

  if (loading) {
    return (
      <Container className="text-center mt-5">
        <Spinner animation="border" variant="primary" />
      </Container>
    );
  }

  if (error) {
    return (
      <Container className="mt-5">
        <Alert variant="danger" className="text-center fw-bold fs-5 shadow">
          {error}
        </Alert>
      </Container>
    );
  }

  return (
    <>
      <NavbarComponent username="User" />
      <Container className="d-flex justify-content-center align-items-center min-vh-100">
        <Row>
          <Col>
            <Card className="p-5 text-center shadow rounded-4 border-0">
              <h1 className="display-4 fw-bold mb-3 text-primary">
                Welcome to <span className="text-dark">Life Capital</span>
              </h1>
              <p className="lead text-muted">
                Building a healthier tomorrow, starting today.🚀
              </p>
            </Card>
          </Col>
        </Row>
      </Container>
    </>
  );
};

export default HomePage;
