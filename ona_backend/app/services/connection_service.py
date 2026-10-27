import uuid
from app.schemas.connection import ConnectionCreate, ConnectionUpdate

CONNECTIONS_DB = {}


class ConnectionService:

  @staticmethod
  def create(data: ConnectionCreate) -> dict:
    conn_id = str(uuid.uuid4())
    connection = {"id": conn_id, **data.model_dump()}
    CONNECTIONS_DB[conn_id] = connection
    return connection

  @staticmethod
  def get_all(board_id: str = None) -> list:
    if board_id:
      return [c for c in CONNECTIONS_DB.values() if c["board_id"] == board_id]
    return list(CONNECTIONS_DB.values())

  @staticmethod
  def get_by_id(conn_id: str) -> dict:
    if conn_id not in CONNECTIONS_DB:
      raise KeyError("Connection not found")
    return CONNECTIONS_DB[conn_id]

  @staticmethod
  def update(conn_id: str, data: ConnectionUpdate) -> dict:
    connection = ConnectionService.get_by_id(conn_id)
    update_data = data.model_dump(exclude_unset=True)
    connection.update(update_data)
    CONNECTIONS_DB[conn_id] = connection
    return connection

  @staticmethod
  def delete(conn_id: str) -> bool:
    if conn_id in CONNECTIONS_DB:
      del CONNECTIONS_DB[conn_id]
      return True
    return False