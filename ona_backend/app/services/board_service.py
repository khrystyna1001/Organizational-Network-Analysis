import uuid
from app.schemas.board import BoardCreate, BoardUpdate

# In-memory database storage
BOARDS_DB = {}


class BoardService:

  @staticmethod
  def create(data: BoardCreate) -> dict:
    board_id = str(uuid.uuid4())
    board = {"id": board_id, **data.model_dump()}
    BOARDS_DB[board_id] = board
    return board

  @staticmethod
  def get_all() -> list:
    return list(BOARDS_DB.values())

  @staticmethod
  def get_by_id(board_id: str) -> dict:
    if board_id not in BOARDS_DB:
      raise KeyError("Board not found")
    return BOARDS_DB[board_id]

  @staticmethod
  def update(board_id: str, data: BoardUpdate) -> dict:
    board = BoardService.get_by_id(board_id)
    update_data = data.model_dump(exclude_unset=True)
    board.update(update_data)
    BOARDS_DB[board_id] = board
    return board

  @staticmethod
  def delete(board_id: str) -> bool:
    if board_id in BOARDS_DB:
      del BOARDS_DB[board_id]
      return True
    return False