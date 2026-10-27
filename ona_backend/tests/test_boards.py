def test_create_board(client):
  response = client.post(
      "/api/v1/boards/",
      json={"name": "Sprint Board", "description": "Q1 ONA Analysis"},
  )
  assert response.status_code == 201
  data = response.json()
  assert data["name"] == "Sprint Board"
  assert "id" in data


def test_get_boards(client):
  client.post(
      "/api/v1/boards/", json={"name": "Board 1", "description": "Test 1"}
  )
  response = client.get("/api/v1/boards/")
  assert response.status_code == 200
  assert len(response.json()) == 1


def test_get_board_by_id(client):
  create_res = client.post(
      "/api/v1/boards/", json={"name": "Target Board"}
  )
  board_id = create_res.json()["id"]

  response = client.get(f"/api/v1/boards/{board_id}")
  assert response.status_code == 200
  assert response.json()["name"] == "Target Board"


def test_delete_board(client):
  create_res = client.post(
      "/api/v1/boards/", json={"name": "Delete Me"}
  )
  board_id = create_res.json()["id"]

  delete_res = client.delete(f"/api/v1/boards/{board_id}")
  assert delete_res.status_code == 204

  get_res = client.get(f"/api/v1/boards/{board_id}")
  assert get_res.status_code == 404