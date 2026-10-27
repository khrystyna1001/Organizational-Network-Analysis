def test_create_connection(client):
  board_id = client.post("/api/v1/boards/", json={"name": "Net"}).json()["id"]

  conn_payload = {
      "board_id": board_id,
      "source_node_id": "node_1",
      "target_node_id": "node_2",
      "weight": 5.0,
  }
  response = client.post("/api/v1/connections/", json=conn_payload)
  assert response.status_code == 201
  data = response.json()
  assert data["weight"] == 5.0
  assert data["source_node_id"] == "node_1"