import { useNavigate } from "react-router-dom";
import { Container, Spinner, Row, Col, Card } from "react-bootstrap";
import NavbarComponent from "./NavbarComponent";
import { useAuth } from "../context/AuthContext";
import { useEffect } from "react";

const HomePage = () => {
  const { role, loading, username } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && !role) {
      navigate("/login");
    }
  }, [loading, role, navigate]);

  if (loading) {
    return (
      <Container className="text-center mt-5">
        <Spinner animation="border" variant="primary" />
      </Container>
    );
  }

  return (
    <>
      <NavbarComponent username={username || "User"} />{" "}
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
