import React, { useEffect, useState } from "react";
import { Container, Button, Row, Col, Spinner } from "react-bootstrap";
import NavbarComponent from "./NavbarComponent";
import { CONSTANTS } from "../constants";
import { Navigate } from "react-router-dom";
const HomePage = () => {
	const [user, setUser] = useState(null);
	const [loading, setLoading] = useState(true);
	const [authenticated, setAuthenticated] = useState(undefined);
	const url = `${CONSTANTS.api_base_url}/auth/token/verify`;

	useEffect(() => {
		async function verify_token() {
			try {
				const response = await fetch(url, {
					method: "POST",
					credentials: "include"
				});

				setAuthenticated(response.ok);
			} catch (err) {
				setLoading = false;
			}
		}

		verify_token();
	}, []);

	if (loading) {
		return (
			<Container className="text-center mt-5">
				<Spinner animation="border" />
			</Container>
		);
	}

	if (!authenticated) {
		return <Navigate to="/login" />;
	}

	return (
		<>
			<NavbarComponent />
			<Container className="mt-5 text-center">
				<Row className="justify-content-center">
					<Col md={8}>
						<h1>Welcome to Life Capital 👋</h1>
						{user && (
							<p>
								Hello, <strong>{user.username}</strong>!
							</p>
						)}

						<div className="mt-4">
							<Button variant="primary" href="/profile" className="mx-2">
								Complete Your Profile
							</Button>

							{user?.role === "admin" && (
								<Button variant="warning" href="/list-users" className="mx-2">
									List Users
								</Button>
							)}
						</div>
					</Col>
				</Row>
			</Container>
		</>
	);
};

export default HomePage;
