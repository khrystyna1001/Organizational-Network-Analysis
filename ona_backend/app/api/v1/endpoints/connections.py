from typing import Optional
from fastapi import APIRouter, HTTPException, status
from app.schemas.connection import (
    ConnectionCreate,
    ConnectionUpdate,
    ConnectionResponse,
)
from app.services.connection_service import ConnectionService

router = APIRouter()


@router.post(
    "/",
    response_model=ConnectionResponse,
    status_code=status.HTTP_201_CREATED,
)
def create_connection(connection: ConnectionCreate):
  return ConnectionService.create(connection)


@router.get("/", response_model=list[ConnectionResponse])
def get_connections(board_id: Optional[str] = None):
  return ConnectionService.get_all(board_id)


@router.get("/{connection_id}", response_model=ConnectionResponse)
def get_connection(connection_id: str):
  try:
    return ConnectionService.get_by_id(connection_id)
  except KeyError:
    raise HTTPException(status_code=404, detail="Connection not found")


@router.put("/{connection_id}", response_model=ConnectionResponse)
def update_connection(connection_id: str, connection: ConnectionUpdate):
  try:
    return ConnectionService.update(connection_id, connection)
  except KeyError:
    raise HTTPException(status_code=404, detail="Connection not found")


@router.delete("/{connection_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_connection(connection_id: str):
  success = ConnectionService.delete(connection_id)
  if not success:
    raise HTTPException(status_code=404, detail="Connection not found")
  return None