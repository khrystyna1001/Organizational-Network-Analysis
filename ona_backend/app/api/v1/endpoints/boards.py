from fastapi import APIRouter, HTTPException, status
from app.schemas.board import BoardCreate, BoardUpdate, BoardResponse
from app.services.board_service import BoardService

router = APIRouter()


@router.post(
    "/",
    response_model=BoardResponse,
    status_code=status.HTTP_201_CREATED,
)
def create_board(board: BoardCreate):
  return BoardService.create(board)


@router.get("/", response_model=list[BoardResponse])
def get_boards():
  return BoardService.get_all()


@router.get("/{board_id}", response_model=BoardResponse)
def get_board(board_id: str):
  try:
    return BoardService.get_by_id(board_id)
  except KeyError:
    raise HTTPException(status_code=404, detail="Board not found")


@router.put("/{board_id}", response_model=BoardResponse)
def update_board(board_id: str, board: BoardUpdate):
  try:
    return BoardService.update(board_id, board)
  except KeyError:
    raise HTTPException(status_code=404, detail="Board not found")


@router.delete("/{board_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_board(board_id: str):
  success = BoardService.delete(board_id)
  if not success:
    raise HTTPException(status_code=404, detail="Board not found")
  return None