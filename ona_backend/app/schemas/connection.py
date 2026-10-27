from typing import Optional
from pydantic import BaseModel


class ConnectionCreate(BaseModel):
  board_id: str
  source_node_id: str
  target_node_id: str
  weight: Optional[float] = 1.0


class ConnectionUpdate(BaseModel):
  weight: Optional[float] = None


class ConnectionResponse(ConnectionCreate):
  id: str