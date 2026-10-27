from fastapi import APIRouter
from app.api.v1.endpoints import boards, nodes, connections, org

api_router = APIRouter()

api_router.include_router(boards.router, prefix="/boards", tags=["Boards"])
api_router.include_router(nodes.router, prefix="/nodes", tags=["Nodes"])
api_router.include_router(
    connections.router, prefix="/connections", tags=["Connections"]
)
api_router.include_router(org.router, prefix="", tags=["Organization Data"])