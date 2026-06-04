"""Client CV demo — FastAPI entrypoint.

Serves three things:
  1. Mock "system of record" REST APIs   (/mock/*)        — the data layer
  2. Agentic endpoints                    (/api/agent/*)   — the agent layer
  3. The static cockpit frontend          (/)              — the experience layer

Run:  uvicorn api.main:app --reload --port 8000
Then open http://localhost:8000/
"""
from pathlib import Path

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from api.routers import mock, agents

app = FastAPI(title="Client CV — Muni Sales Agentic Demo", version="0.1.0")

# Open CORS for local demo convenience.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_methods=["*"], allow_headers=["*"],
)

app.include_router(mock.router)
app.include_router(agents.router)

# Serve the cockpit. Keep this mount LAST so it doesn't shadow the API routes.
FRONTEND_DIR = Path(__file__).resolve().parent.parent / "frontend"
if FRONTEND_DIR.exists():
    app.mount("/", StaticFiles(directory=str(FRONTEND_DIR), html=True), name="frontend")


@app.get("/healthz")
def healthz():
    return {"status": "ok"}
