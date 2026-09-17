"""Bearer JWT + role helpers."""
from __future__ import annotations

from fastapi import HTTPException, Request

from app.tokens import decode


def bearer_user(request: Request) -> dict:
    header = request.headers.get("authorization") or ""
    if header.lower().startswith("bearer "):
        token = header.split(" ", 1)[1].strip()
        data = decode(token)
        if data:
            return {
                "username": data["sub"],
                "name": data.get("name") or data["sub"],
                "role": data.get("role") or "student",
                "id": data.get("uid"),
            }
    raise HTTPException(status_code=401, detail="token")
