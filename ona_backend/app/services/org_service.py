import io
import math
import pandas as pd
from fastapi import UploadFile


class OrgService:

  @staticmethod
  async def get_raw_data(file: UploadFile) -> list:
    """Reads an uploaded CSV file and converts it into a list of records."""
    contents = await file.read()
    df = pd.read_csv(io.BytesIO(contents))
    df = df.astype(object).where(pd.notnull(df), None)
    records = df.to_dict(orient="records")

    def sanitize(obj):
      if isinstance(obj, float) and math.isnan(obj):
        return None
      if isinstance(obj, dict):
        return {k: sanitize(v) for k, v in obj.items()}
      if isinstance(obj, list):
        return [sanitize(v) for v in obj]
      return obj

    return [sanitize(record) for record in records]