import React, { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { Form, Container } from "react-bootstrap";
import { CONSTANTS } from "../constants";

const UserManagement = () => {
  const { id } = useParams();
  const [user, setUser] = useState(null);

  useEffect(() => {
    const fetchUser = async () => {
      const res = await fetch(`${CONSTANTS.api_base_url}/api/user/${id}`, {
        method: "GET",
        credentials: "include",
      });

      if (res.ok) {
        const data = await res.json();
        setUser(data);
      } else {
        alert("User not found or you're not authorized.");
      }
    };

    fetchUser();
  }, [id]);

  const handleChangeRole = async (newRole) => {
    const res = await fetch(`${CONSTANTS.api_base_url}/api/update_role`, {
      method: "POST",
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ userId: id, newRole }),
    });

    if (res.ok) {
      alert("Role updated");
      setUser((prev) => ({ ...prev, role: newRole }));
    } else {
      alert("Failed to update role");
    }
  };

  if (!user) return <Container>Loading...</Container>;

  return (
    <Container className="mt-5">
      <h2>User: {user.username}</h2>
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
    </Container>
  );
};

export default UserManagement;
