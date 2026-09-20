"""Persona helpers: Super Admin, teacher, student. BGH/leadership maps to admin."""


def role_of(user: dict | None) -> str:
    return ((user or {}).get("role") or "").strip().lower()


def is_admin(user: dict | None) -> bool:
    return role_of(user) in ("admin", "leadership")


def is_teacher(user: dict | None) -> bool:
    return role_of(user) == "teacher"


def is_staff(user: dict | None) -> bool:
    return is_admin(user) or is_teacher(user)


def is_student(user: dict | None) -> bool:
    return bool(user) and not is_staff(user)


def persona(user: dict | None) -> str:
    if is_admin(user):
        return "admin"
    if is_teacher(user):
        return "teacher"
    if user:
        return "student"
    return ""
