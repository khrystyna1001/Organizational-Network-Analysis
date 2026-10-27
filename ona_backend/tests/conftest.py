from app.main import app
from app.services.board_service import BOARDS_DB
from app.services.connection_service import CONNECTIONS_DB
from app.services.node_service import NODES_DB
from fastapi.testclient import TestClient
import pytest


@pytest.fixture(autouse=True)
def clear_databases():
  BOARDS_DB.clear()
  NODES_DB.clear()
  CONNECTIONS_DB.clear()


@pytest.fixture
def client():
  return TestClient(app)