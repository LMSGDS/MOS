from app.auth import authenticate, verify_password, find_user

def test_known_users_exist():
    assert find_user("admin")
    assert find_user("giaovien")
    assert find_user("hocsinh")

def test_login_ok():
    user = authenticate("admin", "Mos@Gds2026")
    assert user and user["username"] == "admin"

def test_login_bad_password():
    assert authenticate("admin", "sai-mat-khau") is None

def test_login_unknown():
    assert authenticate("khongco", "Mos@Gds2026") is None
