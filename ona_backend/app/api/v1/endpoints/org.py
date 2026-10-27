from fastapi import APIRouter, HTTPException, UploadFile, File
from app.services.org_service import OrgService

router = APIRouter()


@router.post("/data")
async def get_all_organization_data(file: UploadFile = File(...)):
  """Upload any CSV dataset and display all raw records."""
  try:
    records = await OrgService.get_raw_data(file)
    return {"status": "success", "total_rows": len(records), "data": records}
  except Exception as e:
    raise HTTPException(status_code=400, detail=f"Error reading file: {str(e)}")