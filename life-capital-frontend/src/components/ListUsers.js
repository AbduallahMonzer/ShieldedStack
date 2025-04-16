import React, { useEffect, useState } from "react";
import { Container, Table, Spinner } from "react-bootstrap";
import { Link } from "react-router-dom";
import { CONSTANTS } from "../constants";

const ListUsers = () => {
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchUsers = async () => {
      try {
        const response = await fetch(
          `${CONSTANTS.api_base_url}/api/list_users`,
          {
            method: "GET",
            credentials: "include",
          }
        );

        if (response.ok) {
          const data = await response.json();
          setUsers(data);
        } else if (response.status === 401) {
          alert("Unauthorized: You must be an admin to view this page.");
        } else {
          alert("Failed to load users.");
        }
      } catch (error) {
        alert("Network error: " + error.message);
      } finally {
        setLoading(false);
      }
    };

    fetchUsers();
  }, []);

  const handleRoleChange = async (id, newRole) => {
    const user = users.find((u) => u.id === id);
    if (!user || newRole === user.role) {
      alert("This user already has that role.");
      return;
    }

    try {
      const response = await fetch(
        `${CONSTANTS.api_base_url}/api/update_role`,
        {
          method: "POST",
          credentials: "include",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ userId: id, newRole }),
        }
      );

      if (response.ok) {
        setUsers((prevUsers) =>
          prevUsers.map((user) =>
            user.id === id ? { ...user, role: newRole } : user
          )
        );
      } else {
        alert("Failed to update role.");
      }
    } catch (error) {
      alert("Network error while updating role.");
    }
  };

  return (
    <Container className="mt-5">
      <h2>User Management</h2>
      {loading ? (
        <div className="text-center mt-4">
          <Spinner animation="border" />
        </div>
      ) : (
        <Table striped bordered hover responsive>
          <thead>
            <tr>
              <th>#</th>
              <th>Username</th>
              <th>Email</th>
              <th>Phone Number</th>
              <th>Role</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.id}</td>
                <td>
                  <Link
                    to={`/user-management/${user.id}`}
                    style={{ textDecoration: "none" }}
                  >
                    {user.username}
                  </Link>
                </td>
                <td>{user.email}</td>
                <td>{user.phone_number}</td>
                <td></td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Container>
  );
};

export default ListUsers;
