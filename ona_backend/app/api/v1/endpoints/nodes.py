from typing import Optional
from fastapi import APIRouter, HTTPException, status
from app.schemas.node import NodeCreate, NodeUpdate, NodeResponse
from app.services.node_service import NodeService

router = APIRouter()


@router.post(
    "/", response_model=NodeResponse, status_code=status.HTTP_201_CREATED
)
def create_node(node: NodeCreate):
  return NodeService.create(node)


@router.get("/", response_model=list[NodeResponse])
def get_nodes(board_id: Optional[str] = None):
  return NodeService.get_all(board_id)


@router.get("/{node_id}", response_model=NodeResponse)
def get_node(node_id: str):
  try:
    return NodeService.get_by_id(node_id)
  except KeyError:
    raise HTTPException(status_code=404, detail="Node not found")


@router.put("/{node_id}", response_model=NodeResponse)
def update_node(node_id: str, node: NodeUpdate):
  try:
    return NodeService.update(node_id, node)
  except KeyError:
    raise HTTPException(status_code=404, detail="Node not found")


@router.delete("/{node_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_node(node_id: str):
  success = NodeService.delete(node_id)
  if not success:
    raise HTTPException(status_code=404, detail="Node not found")
  return None