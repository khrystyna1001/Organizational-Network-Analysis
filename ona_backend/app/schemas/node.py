from typing import Optional
from pydantic import BaseModel


class NodeCreate(BaseModel):
  board_id: str
  name: str
  title: str
  department: str


class NodeUpdate(BaseModel):
  name: Optional[str] = None
  title: Optional[str] = None
  department: Optional[str] = None


class NodeResponse(NodeCreate):
  id: str