import os
from dotenv import load_dotenv
from pydantic_settings import BaseSettings

load_dotenv()

class Settings(BaseSettings):
  PROJECT_NAME: str = "ONA Enterprise Engine"
  API_V1_STR: str = "/api/v1"
  GEMINI_API_KEY: str = os.getenv("GEMINI_API_KEY", "")

settings = Settings()