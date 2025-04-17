import React, { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Spinner, Alert, Container } from "react-bootstrap";
import { CONSTANTS } from "../constants";

const OAuthHandler = () => {
  const [searchParams] = useSearchParams();
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    const handleOAuthVerify = async () => {
      const token = document.cookie
        .split("; ")
        .find((row) => row.startsWith("access_token="));

      if (token) {
        navigate("/");
        setLoading(false);
        return;
      }

      const code = searchParams.get("code");
      const state = searchParams.get("state");

      if (!code || !state) {
        setError("Authorization code or state parameter is missing.");
        setLoading(false);
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
      } finally {
        setLoading(false);
      }
    };

    handleOAuthVerify();
  }, [searchParams, navigate]);

  return (
    <Container className="d-flex flex-column align-items-center justify-content-center min-vh-100">
      {loading ? (
        <>
          <Spinner animation="border" role="status" />
          <div className="mt-3">Completing login with Life Capital...</div>
        </>
      ) : error ? (
        <Alert variant="danger">{error}</Alert>
      ) : (
        <div className="mt-3">Redirecting...</div>
      )}
    </Container>
  );
};

export default OAuthHandler;
