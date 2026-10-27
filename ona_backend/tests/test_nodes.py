def test_create_and_get_node(client):
  board_res = client.post("/api/v1/boards/", json={"name": "Org Chart"})
  board_id = board_res.json()["id"]

  node_payload = {
      "board_id": board_id,
      "name": "Alice Smith",
      "title": "Director",
      "department": "Engineering",
  }
  response = client.post("/api/v1/nodes/", json=node_payload)
  assert response.status_code == 201
  data = response.json()
  assert data["name"] == "Alice Smith"
  assert data["board_id"] == board_id
  assert "id" in data


def test_filter_nodes_by_board(client):
  b1 = client.post("/api/v1/boards/", json={"name": "B1"}).json()["id"]
  b2 = client.post("/api/v1/boards/", json={"name": "B2"}).json()["id"]

  client.post(
      "/api/v1/nodes/",
      json={
          "board_id": b1,
          "name": "Bob",
          "title": "Dev",
          "department": "IT",
      },
  )
  client.post(
      "/api/v1/nodes/",
      json={
          "board_id": b2,
          "name": "Charlie",
          "title": "Sales",
          "department": "Sales",
      },
  )

  res = client.get(f"/api/v1/nodes/?board_id={b1}")
  assert res.status_code == 200
  nodes = res.json()
  assert len(nodes) == 1
  assert nodes[0]["name"] == "Bob"