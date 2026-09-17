from app.programs import MENU, normalize, resolve


def test_menu_has_three_office_apps():
    assert [p["id"] for p in MENU] == ["word", "excel", "powerpoint"]
    assert [p["name"] for p in MENU] == [
        "Microsoft Word",
        "Microsoft Excel",
        "Microsoft PowerPoint",
    ]


def test_normalize_aliases():
    assert normalize("xlsx") == "excel"
    assert normalize("ppt") == "powerpoint"
    assert normalize("WINWORD") == "word"
    assert resolve("excel")["protocol"] == "ms-excel:"
    assert resolve("powerpoint")["protocol"] == "ms-powerpoint:"
