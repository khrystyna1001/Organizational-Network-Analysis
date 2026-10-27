from typing import Optional
from pydantic import BaseModel


class BoardCreate(BaseModel):
  name: str
  description: Optional[str] = None


class BoardUpdate(BaseModel):
  name: Optional[str] = None
  description: Optional[str] = None


class BoardResponse(BoardCreate):
  id: str