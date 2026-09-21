"""Chuỗi song ngữ cho MOS-KulKul.

Quy ước: rubric lưu MỘT bản duy nhất cho mỗi project; trường hiển thị có thể là

    "prompt": "Tìm từ to từ Navigation pane."              # cũ, coi như tiếng Việt
    "prompt": {"vi": "Tìm từ to…", "en": "Locate all…"}    # mới, song ngữ

Bộ truy xuất dưới đây nhận cả hai dạng, nên rubric cũ chạy nguyên không cần sửa.

Cách dùng: localize Ở THỜI ĐIỂM NẠP rubric, trước khi đưa vào grade.evaluate_facts()
hoặc qmatrix.attach(). Hai module đó đọc thẳng criterion["feedback"][status],
criterion["prompt"], criterion["help_steps"] như chuỗi — localize trước thì chúng
không phải sửa một dòng nào.

    from app import i18n
    rubric = i18n.localize_rubric(load_rubric(src), lang)
    result = grade.evaluate_facts(facts, rubric, evidence)
"""
from __future__ import annotations

from typing import Any

DEFAULT_LANG = "vi"
LANGS = ("vi", "en")

# Trường hiển thị của một criterion. Những trường còn lại (id, kind, weight,
# predicate, selector, evidence_policy…) là dữ liệu máy đọc, KHÔNG dịch.
CRITERION_TEXT_FIELDS = ("prompt",)
CRITERION_LIST_FIELDS = ("help_steps",)
CRITERION_MAP_FIELDS = ("feedback",)
RUBRIC_TEXT_FIELDS = ("title", "description")


def normalize_lang(lang: str | None) -> str:
    """Nhận 'vi', 'VI', 'vi-VN', 'en-US'… trả về 'vi' hoặc 'en'."""
    code = (lang or "").strip().casefold().replace("_", "-")
    code = code.split("-", 1)[0]
    return code if code in LANGS else DEFAULT_LANG


def is_bundle(value: Any) -> bool:
    """True nếu value là khối song ngữ {'vi': …, 'en': …}."""
    return isinstance(value, dict) and any(k in value for k in LANGS)


def pick(value: Any, lang: str = DEFAULT_LANG) -> str:
    """Lấy chuỗi theo ngôn ngữ. Thiếu bản dịch thì lùi về ngôn ngữ mặc định,
    thiếu nữa thì lùi về bất kỳ bản nào có — UI không bao giờ trống."""
    lang = normalize_lang(lang)
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    if is_bundle(value):
        for key in (lang, DEFAULT_LANG, *LANGS):
            text = value.get(key)
            if isinstance(text, str) and text.strip():
                return text
        return ""
    return str(value)


def has(value: Any, lang: str) -> bool:
    """True nếu CÓ THẬT bản dịch cho ngôn ngữ này (không tính lùi về bản khác).
    Dùng để đếm phần còn thiếu, và để UI gắn nhãn 'chưa dịch'."""
    lang = normalize_lang(lang)
    if isinstance(value, str):
        return lang == DEFAULT_LANG
    if is_bundle(value):
        text = value.get(lang)
        return isinstance(text, str) and bool(text.strip())
    return False


def pick_list(value: Any, lang: str = DEFAULT_LANG) -> list[str]:
    """help_steps: chấp nhận list[str], list[bundle], hoặc bundle của list.

        ["Mở tab **Home**.", …]                                  # cũ
        [{"vi": "Mở tab **Home**.", "en": "Open the **Home** tab."}, …]
        {"vi": ["…"], "en": ["…"]}                               # cũng được
    """
    if value is None:
        return []
    if is_bundle(value):
        lang = normalize_lang(lang)
        for key in (lang, DEFAULT_LANG, *LANGS):
            items = value.get(key)
            if isinstance(items, list) and items:
                return [pick(item, lang) for item in items]
        return []
    if isinstance(value, list):
        return [pick(item, lang) for item in value]
    return [pick(value, lang)]


def pick_map(value: Any, lang: str = DEFAULT_LANG) -> dict[str, str]:
    """feedback: {'pass': str|bundle, 'fail': …, 'unverified': …, 'error': …}."""
    if not isinstance(value, dict):
        return {}
    if is_bundle(value):
        # {"vi": {"pass": …}, "en": {"pass": …}} — dạng ít dùng nhưng vẫn nhận
        lang = normalize_lang(lang)
        for key in (lang, DEFAULT_LANG, *LANGS):
            inner = value.get(key)
            if isinstance(inner, dict) and inner:
                return {k: pick(v, lang) for k, v in inner.items()}
        return {}
    return {k: pick(v, lang) for k, v in value.items()}


def localize_criterion(criterion: dict, lang: str = DEFAULT_LANG) -> dict:
    """Trả về bản sao của criterion với mọi trường hiển thị đã dẹp thành chuỗi."""
    if not isinstance(criterion, dict):
        return criterion
    out = dict(criterion)
    for field in CRITERION_TEXT_FIELDS:
        if field in out:
            out[field] = pick(out[field], lang)
    for field in CRITERION_LIST_FIELDS:
        if field in out:
            out[field] = pick_list(out[field], lang)
    for field in CRITERION_MAP_FIELDS:
        if field in out:
            out[field] = pick_map(out[field], lang)
    return out


def localize_rubric(rubric: dict, lang: str = DEFAULT_LANG) -> dict:
    """Dẹp toàn bộ rubric về một ngôn ngữ. Gọi hàm này TRƯỚC khi chấm."""
    if not isinstance(rubric, dict):
        return rubric
    lang = normalize_lang(lang)
    out = dict(rubric)
    for field in RUBRIC_TEXT_FIELDS:
        if field in out:
            out[field] = pick(out[field], lang)
    criteria = out.get("criteria")
    if isinstance(criteria, list):
        out["criteria"] = [localize_criterion(c, lang) for c in criteria]
    out["lang"] = lang
    return out


def missing(rubric: dict, lang: str) -> list[str]:
    """Liệt kê đường dẫn tới các trường CHƯA có bản dịch cho ngôn ngữ này.
    Dùng trong CI để chặn rubric nửa vời lọt vào bản phát hành."""
    lang = normalize_lang(lang)
    gaps: list[str] = []
    if not isinstance(rubric, dict):
        return gaps

    for field in RUBRIC_TEXT_FIELDS:
        if field in rubric and not has(rubric[field], lang):
            gaps.append(field)

    for index, criterion in enumerate(rubric.get("criteria") or []):
        if not isinstance(criterion, dict):
            continue
        cid = criterion.get("id") or f"[{index}]"
        for field in CRITERION_TEXT_FIELDS:
            if field in criterion and not has(criterion[field], lang):
                gaps.append(f"{cid}.{field}")
        for field in CRITERION_MAP_FIELDS:
            block = criterion.get(field)
            if isinstance(block, dict) and not is_bundle(block):
                for key, value in block.items():
                    if not has(value, lang):
                        gaps.append(f"{cid}.{field}.{key}")
            elif field in criterion and not has(block, lang):
                gaps.append(f"{cid}.{field}")
        for field in CRITERION_LIST_FIELDS:
            steps = criterion.get(field)
            if is_bundle(steps):
                if not has(steps, lang):
                    gaps.append(f"{cid}.{field}")
            elif isinstance(steps, list):
                for i, step in enumerate(steps):
                    if not has(step, lang):
                        gaps.append(f"{cid}.{field}[{i}]")
    return gaps


def coverage(rubric: dict, lang: str) -> float:
    """Tỉ lệ phần trăm trường hiển thị đã có bản dịch. Dùng cho bảng theo dõi."""
    gaps = len(missing(rubric, lang))
    total = _text_field_count(rubric)
    if total <= 0:
        return 100.0
    return round(100.0 * (total - gaps) / total, 1)


def _text_field_count(rubric: dict) -> int:
    count = sum(1 for f in RUBRIC_TEXT_FIELDS if f in rubric)
    for criterion in rubric.get("criteria") or []:
        if not isinstance(criterion, dict):
            continue
        count += sum(1 for f in CRITERION_TEXT_FIELDS if f in criterion)
        block = criterion.get("feedback")
        if isinstance(block, dict) and not is_bundle(block):
            count += len(block)
        elif block is not None:
            count += 1
        steps = criterion.get("help_steps")
        if is_bundle(steps):
            count += 1
        elif isinstance(steps, list):
            count += len(steps)
    return count
