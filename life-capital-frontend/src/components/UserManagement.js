import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { Form, Container, Button, Alert } from "react-bootstrap";
import { CONSTANTS } from "../constants";

const UserManagement = () => {
  const { id } = useParams();
  const [user, setUser] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchUser = async () => {
      try {
        const res = await fetch(`${CONSTANTS.api_base_url}/api/user/${id}`, {
          method: "GET",
          credentials: "include",
        });

        if (res.ok) {
          const data = await res.json();
          setUser(data);
        } else {
          setError("User not found or you're not authorized.");
        }
      } catch (err) {
        setError("Something went wrong. Please try again.");
      } finally {
        setLoading(false);
      }
    };

    fetchUser();
  }, [id]);

  const handleChangeRole = async (newRole) => {
    try {
      const res = await fetch(`${CONSTANTS.api_base_url}/api/update_role`, {
        method: "POST",
        credentials: "include",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({ userId: id, newRole }),
      });

      if (res.ok) {
        setUser((prev) => ({ ...prev, role: newRole }));
        alert("Role updated successfully!");
      } else {
        alert("Failed to update role. Please try again.");
      }
    } catch (error) {
      alert("Error updating role.");
    }
  };

  if (loading) return <Container>Loading...</Container>;

  if (error)
    return (
      <Container>
        <Alert variant="danger">{error}</Alert>
      </Container>
    );

  return (
    <Container className="mt-5">
      <h2>User Management for: {user.username}</h2>
      <p>Email: {user.email}</p>
      <p>Phone: {user.phone_number}</p>
      <p>Current Role: {user.role}</p>

      <Form.Select
        value={user.role}
        onChange={(e) => handleChangeRole(e.target.value)}
      >
        <option value="user">User</option>
        <option value="admin">Admin</option>
      </Form.Select>

      <Button
        className="mt-3"
        onClick={() => handleChangeRole(user.role)}
        disabled={user.role === "admin"} // Prevent unnecessary requests if role is unchanged
      >
        Update Role
      </Button>
    </Container>
  );
};

export default UserManagement;
