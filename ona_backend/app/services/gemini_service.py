import google as genai
from app.core.config import settings


class GeminiInsightService:

  def __init__(self):
    genai.configure(api_key=settings.GEMINI_API_KEY)
    self.model = genai.GenerativeModel("gemini-2.5-flash")

  def generate_hr_narrative(self, analytics_report: dict) -> str:
    """Takes NetworkX metrics and generates an executive HR report."""

    system_instruction = (
        "You are an elite Enterprise Organizational Psychologist and HR"
        " Analytics Consultant. Review structural network metrics from an"
        " org chart and provide strategic, executive-ready insights focused on"
        " risk management, manager burnout, and structural efficiency."
    )

    prompt = f"""
        {system_instruction}

        Here are the Organizational Network Analysis (ONA) metrics computed from the company graph:
        - Total Employees: {analytics_report.get('total_nodes')}
        - Total Reporting Links: {analytics_report.get('total_edges')}
        - Structural Flags/Anomalies Detected: {analytics_report.get('insights')}

        Provide an executive summary structured as follows:
        1. **Executive Overview**: A 2-sentence structural health snapshot.
        2. **Top Critical Risks**: Highlight severe bottlenecks or manager overloads.
        3. **Actionable Recommendations**: Give 2 concrete, step-by-step structural fixes for HR.
        """

    response = self.model.generate_content(prompt)
    return response.text