"""Session auth for pyapi — reuses the PHP `sessions` table so the app and the
existing web login share one source of truth (no new token stack needed).

- Admin/CP: cookie `admin_session` + `admin_u_id`, validated as type=1
  (mirrors epc_cp_auth_gate_is_admin in cp/epc_cp_auth_gate.php).
- Storefront customer: cookie `session` + `u_id`.
"""

from __future__ import annotations

from fastapi import HTTPException, Request

from . import db


def _valid_session(session: str, user_id: int, admin: bool) -> bool:
    if not session or user_id <= 0:
        return False
    try:
        if admin:
            row = db.fetch_one(
                "SELECT COUNT(*) AS c FROM `sessions` "
                "WHERE `session` = %s AND `type` = 1 AND `user_id` = %s",
                (session, user_id),
            )
        else:
            row = db.fetch_one(
                "SELECT COUNT(*) AS c FROM `sessions` "
                "WHERE `session` = %s AND `user_id` = %s",
                (session, user_id),
            )
    except Exception:
        return False
    return bool(row and int(row.get("c", 0)) == 1)


def require_admin(request: Request) -> int:
    """Return the authenticated admin user id or raise 401."""
    session = request.cookies.get("admin_session", "")
    try:
        user_id = int(request.cookies.get("admin_u_id", "0") or "0")
    except ValueError:
        user_id = 0
    if not _valid_session(session, user_id, admin=True):
        raise HTTPException(status_code=401, detail="Admin session required")
    return user_id


def require_customer(request: Request) -> int:
    """Return the authenticated storefront user id or raise 401."""
    session = request.cookies.get("session", "")
    try:
        user_id = int(request.cookies.get("u_id", "0") or "0")
    except ValueError:
        user_id = 0
    if not _valid_session(session, user_id, admin=False):
        raise HTTPException(status_code=401, detail="Login required")
    return user_id


def optional_customer(request: Request) -> int:
    """Return customer id if a valid session cookie is present, else 0."""
    session = request.cookies.get("session", "")
    try:
        user_id = int(request.cookies.get("u_id", "0") or "0")
    except ValueError:
        user_id = 0
    return user_id if _valid_session(session, user_id, admin=False) else 0
