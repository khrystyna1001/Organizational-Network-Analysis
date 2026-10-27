import uuid
from app.schemas.node import NodeCreate, NodeUpdate

NODES_DB = {}


class NodeService:

  @staticmethod
  def create(data: NodeCreate) -> dict:
    node_id = str(uuid.uuid4())
    node = {"id": node_id, **data.model_dump()}
    NODES_DB[node_id] = node
    return node

  @staticmethod
  def get_all(board_id: str = None) -> list:
    if board_id:
      return [n for n in NODES_DB.values() if n["board_id"] == board_id]
    return list(NODES_DB.values())

  @staticmethod
  def get_by_id(node_id: str) -> dict:
    if node_id not in NODES_DB:
      raise KeyError("Node not found")
    return NODES_DB[node_id]

  @staticmethod
  def update(node_id: str, data: NodeUpdate) -> dict:
    node = NodeService.get_by_id(node_id)
    update_data = data.model_dump(exclude_unset=True)
    node.update(update_data)
    NODES_DB[node_id] = node
    return node

  @staticmethod
  def delete(node_id: str) -> bool:
    if node_id in NODES_DB:
      del NODES_DB[node_id]
      return True
    return False