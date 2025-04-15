import React, { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Spinner, Alert, Container } from "react-bootstrap";
import { CONSTANTS } from "../constants";

const OAuthHandler = () => {
  const [searchParams] = useSearchParams();
  const [error, setError] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    const handleOAuthVerify = async () => {
      const code = searchParams.get("code");
      const state = searchParams.get("state");

      if (!code) {
        setError("Authorization code is missing.");
        return;
      }

      try {
        const res = await fetch(
          `${CONSTANTS.api_base_url}/auth/oauth/verify?code=${code}&state=${state}`,
          {
            method: "POST",
            credentials: "include",
          }
        );

        if (res.ok) {
          navigate("/");
        } else {
          const data = await res.json();
          setError(data.message || "OAuth verification failed.");
        }
      } catch (err) {
        setError("An error occurred while verifying OAuth.");
      }
    };

    handleOAuthVerify();
  }, [searchParams, navigate]);

  return (
    <Container className="d-flex flex-column align-items-center justify-content-center min-vh-100">
      {!error ? (
        <>
          <Spinner animation="border" role="status" />
          <div className="mt-3">Completing login with Life Capital...</div>
        </>
      ) : (
        <Alert variant="danger">{error}</Alert>
      )}
    </Container>
  );
};

export default OAuthHandler;
