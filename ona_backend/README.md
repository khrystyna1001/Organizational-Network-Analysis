# ONA Backend (Organizational Network Analyzer)

A FastAPI backend designed for HR analytics, organizational network analysis (ONA), and structural graph modeling. It supports CSV dataset ingestion, full CRUD operations for analysis boards, nodes, and connections, and automated testing.

---

## Tech Stack

* **Language:** Python 3.12+
* **Framework:** FastAPI
* **Package & Environment Manager:** `uv`
* **Data Processing:** Pandas
* **Data Validation:** Pydantic v2
* **Testing:** Pytest & FastAPI TestClient

---

## Architectural Overview

The project follows a strict **layered architecture** to maintain separation of concerns:
* **`app/main.py`**: Initializes the FastAPI application and includes API routers.
* **`app/api/v1/endpoints/`**: Defines HTTP routes, handling requests and HTTP status codes.
* **`app/api/v1/router.py`**: Aggregates all endpoint routers under a unified prefix.
* **`app/services/`**: Encapsulates all core business logic and in-memory data management.
* **`app/schemas/`**: Pydantic models for strict data validation and serialization.

---

## Project Directory Structure

```text
ona_backend/
├── app/
│   ├── __init__.py
│   ├── main.py                  # Application entry point
│   ├── core/
│   │   ├── __init__.py
│   │   └── config.py            # Global configuration & settings
│   ├── schemas/                 # Pydantic data models
│   │   ├── __init__.py
│   │   ├── board.py
│   │   ├── node.py
│   │   └── connection.py
│   ├── services/                # Business logic layer
│   │   ├── __init__.py
│   │   ├── org_service.py
│   │   ├── board_service.py
│   │   ├── node_service.py
│   │   └── connection_service.py
│   └── api/
│       ├── __init__.py
│       └── v1/
│           ├── router.py        # Route aggregator
│           └── endpoints/       # Route handlers
│               ├── __init__.py
│               ├── org.py
│               ├── boards.py
│               ├── nodes.py
│               └── connections.py
├── tests/                       # Test suite
│   ├── __init__.py
│   ├── test_boards.py
│   ├── test_nodes.py
│   ├── test_connections.py
│   └── test_org.py
├── conftest.py                  # Pytest fixtures & test isolation
├── pyproject.toml               # Project metadata & dependencies
└── README.md
```

---

## Getting Started

Start the local Uvicorn server with hot-reloading enabled:

```bash
uv run uvicorn app.main:app --reload
```

---

## API Endpoints Reference

### 1. Organization Data Ingestion

- **`POST /api/v1/data`**
  - **Description:** Accepts an uploaded corporate CSV dataset file (e.g., Kaggle employee exports), cleans missing values / `NaN` entries, and returns all records as structured JSON along with row counts.

---

### 2. Boards Management (`/api/v1/boards`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **POST** | `/` | Create a new ONA analysis board (with name and optional description). |
| **GET** | `/` | Retrieve a list of all analysis boards. |
| **GET** | `/{board_id}` | Retrieve a specific board by its ID. |
| **PUT** | `/{board_id}` | Update an existing board's details. |
| **DELETE** | `/{board_id}` | Delete a board by ID. |

---

### 3. Nodes Management (`/api/v1/nodes`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **POST** | `/` | Create a new organizational node linked to a board (`board_id`, `name`, `title`, `department`). |
| **GET** | `/` | Retrieve all nodes (supports optional `?board_id=` query filtering). |
| **GET** | `/{node_id}` | Retrieve a specific node by ID. |
| **PUT** | `/{node_id}` | Update node attributes. |
| **DELETE** | `/{node_id}` | Remove a node. |

---

### 4. Connections Management (`/api/v1/connections`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| **POST** | `/` | Create a relationship link between two nodes (`board_id`, `source_node_id`, `target_node_id`, and optional weight). |
| **GET** | `/` | Retrieve all connections (supports optional `?board_id=` query filtering). |
| **GET** | `/{connection_id}` | Retrieve a specific connection by ID. |
| **PUT** | `/{connection_id}` | Update connection weights or properties. |
| **DELETE** | `/{connection_id}` | Remove a connection. |

---

## Running Tests

To run the full automated test suite using `pytest`:

```bash
uv run pytest -v
```