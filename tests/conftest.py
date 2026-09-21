"""Cấu hình chung cho pytest.

TestClient nói chuyện qua http://testserver, nên cookie phiên gắn cờ Secure sẽ
không được gửi lại → mọi test đăng nhập gãy. Tắt cờ này riêng cho bộ test;
máy chủ thật giữ mặc định MOS_HTTPS_ONLY=1.
"""
import os

os.environ.setdefault("MOS_HTTPS_ONLY", "0")
