import io


def test_upload_csv_data(client):
  csv_content = b"EmpID,FirstName,LastName,DepartmentType\n1,John,Doe,Engineering\n2,Jane,Smith,Sales"
  csv_file = io.BytesIO(csv_content)

  response = client.post(
      "/api/v1/data",
      files={"file": ("employee_data.csv", csv_file, "text/csv")},
  )
  assert response.status_code == 200
  data = response.json()
  assert data["status"] == "success"
  assert data["total_rows"] == 2
  assert data["data"][0]["FirstName"] == "John"