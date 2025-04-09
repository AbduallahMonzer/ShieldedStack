import React, { useEffect, useState } from "react";
import { Container, Table } from "react-bootstrap";
import { CONSTANTS } from "../constants";
const ListUsers = () => {
  const [users, setUsers] = useState([]);
  const url = `${CONSTANTS.api_base_url}${CONSTANTS.api_listUser_url}`;
  useEffect(() => {
    const fetchUsers = async () => {
      const response = await fetch(url, {
        method: "GET",
        credentials: "include",
      });

      if (response.ok) {
        const data = await response.json();
        setUsers(data);
      } else if (response.status === 401) {
        alert("Unauthorized: You must be an admin to view this page.");
      } else {
        alert("Failed to load users.");
      }
    };

    fetchUsers();
  });

  return (
    <Container className="mt-5">
      <h2>List of Users</h2>
      <Table striped bordered hover>
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
              <td>{user.username}</td>
              <td>{user.email}</td>
              <td>{user.phone_number}</td>
              <td>{user.role}</td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Container>
  );
};

export default ListUsers;
