import React, { useState } from "react";
import { Navbar, Container, Offcanvas, Button } from "react-bootstrap";
import "bootstrap/dist/css/bootstrap.min.css";

const getRandomColor = () => {
  const colors = ["#007bff", "#28a745", "#dc3545", "#6f42c1", "#fd7e14"];
  return colors[Math.floor(Math.random() * colors.length)];
};

const NavbarComponent = () => {
  const username = localStorage.getItem("username") || "User";
  const avatarColor = getRandomColor();
  const [showDrawer, setShowDrawer] = useState(false);
  const handleToggleDrawer = () => setShowDrawer(!showDrawer);
  const handleCloseDrawer = () => setShowDrawer(false);

  return (
    <>
      <Navbar bg="light" expand="lg" className="shadow-sm">
        <Container fluid>
          <Navbar.Brand href="#" className="fw-bold text-primary">
            Life Capital
          </Navbar.Brand>

          {!showDrawer && (
            <div
              className="ms-auto d-flex align-items-center"
              onClick={handleToggleDrawer}
              style={{ cursor: "pointer" }}
            >
              <div
                className="rounded-circle me-2"
                style={{
                  width: "40px",
                  height: "40px",
                  backgroundColor: avatarColor,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  color: "#fff",
                  fontWeight: "bold",
                }}
              >
                {username.charAt(0).toUpperCase()}
              </div>
              <span className="fw-semibold">{username}</span>
            </div>
          )}
        </Container>
      </Navbar>

      <Offcanvas show={showDrawer} onHide={handleCloseDrawer} placement="start">
        <Offcanvas.Header closeButton>
          <Offcanvas.Title>
            <div
              className="rounded-circle"
              style={{
                width: "50px",
                height: "50px",
                backgroundColor: avatarColor,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: "#fff",
                fontWeight: "bold",
                marginRight: "10px",
              }}
            >
              {username.charAt(0).toUpperCase()}
            </div>
            <span className="fw-semibold">{username}</span>
          </Offcanvas.Title>
        </Offcanvas.Header>
        <Offcanvas.Body>
          <Button variant="link" href="/profile" className="w-100 text-start">
            Profile
          </Button>
          <Button variant="link" href="/settings" className="w-100 text-start">
            Settings
          </Button>
          <Button
            variant="link"
            onClick={() => {
              localStorage.clear();
              window.location.href = "/login";
            }}
            className="w-100 text-start"
          >
            Logout
          </Button>
        </Offcanvas.Body>
      </Offcanvas>
    </>
  );
};

export default NavbarComponent;
