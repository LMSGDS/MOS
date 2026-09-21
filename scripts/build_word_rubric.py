"""Dựng bản song ngữ của word-objective-1-1.json.

Bản EN lấy theo đề gốc trong MOS Study Guide (Objective 1.1 practice tasks),
KHÔNG dịch ngược từ tiếng Việt — để học sinh đọc đúng thứ tiếng Anh sẽ gặp
trong phòng thi. Tên lệnh ribbon giữ nguyên tiếng Anh ở CẢ HAI ngôn ngữ.
"""
import json
from pathlib import Path

VI, EN = "vi", "en"


def t(vi: str, en: str) -> dict:
    return {VI: vi, EN: en}


def fb(pass_: tuple, fail: tuple, unver: tuple, err: tuple) -> dict:
    return {
        "pass": t(*pass_),
        "fail": t(*fail),
        "unverified": t(*unver),
        "error": t(*err),
    }


def steps(*pairs: tuple) -> list:
    return [t(vi, en) for vi, en in pairs]


UNVER_FIND = (
    "Chưa thu được bằng chứng Find trên Navigation pane. Không kết luận học sinh làm sai.",
    "No evidence captured for Find in the Navigation pane. This is not counted as a wrong answer.",
)

criteria = [
    {
        "id": "W11-S01",
        "objective": "1.1",
        "kc": ["KC-1.1.1"],
        "kind": "action_sequence",
        "weight": 4,
        "prompt": t(
            "Từ Navigation pane, tìm tất cả các chỗ xuất hiện của to.",
            "From the Navigation pane, locate all instances of to.",
        ),
        "selector": {"action": "find", "query": "to", "source": "navigation_pane"},
        "predicate": {"type": "search_query", "query": "to"},
        "evidence_policy": "required_method",
        "feedback": fb(
            ("Đã tìm to từ Navigation pane.", "Found to from the Navigation pane."),
            ("Chưa dùng Navigation pane để tìm to.", "You did not use the Navigation pane to search for to."),
            UNVER_FIND,
            ("Không ghi nhận được thao tác tìm kiếm.", "The search action could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Mở tab **Home**.", "Open the **Home** tab."),
            (
                "Nhóm **Editing**, bấm **Find** (hoặc Ctrl+F) để mở **Navigation** pane.",
                "In the **Editing** group, click **Find** (or press Ctrl+F) to open the **Navigation** pane.",
            ),
            ("Gõ to vào ô **Search Document**.", "Type to in the **Search Document** box."),
        ),
    },
    {
        "id": "W11-S02",
        "objective": "1.1",
        "kc": ["KC-1.1.1"],
        "kind": "action_sequence",
        "weight": 4,
        "prompt": t(
            "Xem lại kết quả tìm kiếm trên thẻ Results của Navigation pane.",
            "Review the search results on the Results tab of the Navigation pane.",
        ),
        "selector": {"action": "results_tab"},
        "predicate": {"type": "results_tab"},
        "evidence_policy": "required_method",
        "feedback": fb(
            ("Đã mở thẻ Results có kết quả.", "The Results tab was opened with results showing."),
            ("Chưa chuyển sang thẻ Results.", "You did not switch to the Results tab."),
            (
                "Chưa thu được bằng chứng thẻ Results. Không kết luận học sinh làm sai.",
                "No evidence captured for the Results tab. This is not counted as a wrong answer.",
            ),
            ("Không ghi nhận được thẻ Results.", "The Results tab could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Giữ **Navigation** pane đang mở.", "Keep the **Navigation** pane open."),
            ("Bấm thẻ **Results** dưới ô tìm kiếm.", "Click the **Results** tab below the search box."),
            (
                "Xem danh sách kết quả to trong tài liệu.",
                "Review the list of to results found in the document.",
            ),
        ),
    },
    {
        "id": "W11-S03",
        "objective": "1.1",
        "kc": ["KC-1.1.1"],
        "kind": "action_sequence",
        "weight": 4,
        "prompt": t(
            "Đổi từ khóa thành toy, rồi chuyển giữa các kết quả bằng nút điều hướng trên thẻ Results.",
            "Modify the search term to locate all instances of toy, then move between the search "
            "results by using the navigation buttons on the Results tab.",
        ),
        "selector": {"action": "find_navigate", "query": "toy"},
        "predicate": {"type": "search_navigate", "query": "toy", "min_hits": 2},
        "evidence_policy": "required_method",
        "feedback": fb(
            (
                "Đã tìm toy và chuyển giữa ít nhất hai kết quả.",
                "Searched for toy and moved between at least two results.",
            ),
            (
                "Chưa đổi sang toy hoặc chưa chuyển kết quả bằng nút điều hướng.",
                "You did not change the term to toy, or did not move between results using the "
                "navigation buttons.",
            ),
            (
                "Chưa thu được bằng chứng điều hướng kết quả tìm kiếm. Không kết luận học sinh làm sai.",
                "No evidence captured for moving between search results. This is not counted as a "
                "wrong answer.",
            ),
            ("Không ghi nhận được điều hướng kết quả.", "Result navigation could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            (
                "Trong **Navigation** pane, xóa to và gõ toy.",
                "In the **Navigation** pane, clear to and type toy.",
            ),
            (
                "Bấm **Next Search Result** để nhảy sang kết quả kế tiếp.",
                "Click **Next Search Result** to jump to the next hit.",
            ),
            (
                "Chuyển ít nhất hai lần giữa các kết quả toy.",
                "Move between the toy results at least twice.",
            ),
        ),
    },
    {
        "id": "W11-S04",
        "objective": "1.1",
        "kc": ["KC-1.1.1"],
        "kind": "action_sequence",
        "weight": 4,
        "prompt": t(
            "Đổi tùy chọn tìm kiếm để chỉ tìm đúng từ viết hoa Toymakers, rồi xem lại kết quả.",
            "Modify the search options to locate only instances of the capitalized word Toymakers, "
            "then review the results.",
        ),
        "selector": {"action": "find", "query": "Toymakers", "match_case": True, "whole_word": True},
        "predicate": {"type": "search_query", "query": "Toymakers", "match_case": True, "whole_word": True},
        "evidence_policy": "required_method",
        "feedback": fb(
            ("Đã tìm đúng từ Toymakers.", "Located the exact word Toymakers."),
            (
                "Chưa tìm Toymakers với Match case và Find whole words only.",
                "You did not search for Toymakers with Match case and Find whole words only enabled.",
            ),
            (
                "Chưa thu được bằng chứng Find Match case. Không kết luận học sinh làm sai.",
                "No evidence captured for a Match case search. This is not counted as a wrong answer.",
            ),
            ("Không ghi nhận được Find nâng cao.", "The advanced search could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            (
                "Mở **Find**, bấm mũi tên **Search for more things** rồi chọn **Options** hoặc **Advanced Find**.",
                "Open **Find**, click the **Search for more things** arrow, then choose **Options** or "
                "**Advanced Find**.",
            ),
            (
                "Gõ Toymakers. Bật **Match case** và **Find whole words only**.",
                "Type Toymakers. Select **Match case** and **Find whole words only**.",
            ),
            ("Bấm **Find Next**.", "Click **Find Next**."),
        ),
    },
    {
        "id": "W11-S05",
        "objective": "1.1",
        "kc": ["KC-1.1.1"],
        "kind": "action_sequence",
        "weight": 4,
        "prompt": t(
            "Dùng Advanced Find để tìm mọi chỗ xuất hiện của Toy hoặc toy có áp style Heading 2.",
            "Perform an advanced search for all instances of Toy or toy (either capitalized or "
            "lowercase) that have the Heading 2 style applied.",
        ),
        "selector": {"action": "advanced_find", "query": "toy", "style": "Heading 2"},
        "predicate": {"type": "advanced_find", "query": "toy", "style": "Heading 2"},
        "evidence_policy": "required_method",
        "feedback": fb(
            (
                "Đã dùng Advanced Find tìm toy trong Heading 2.",
                "Used Advanced Find to search for toy within Heading 2.",
            ),
            (
                "Chưa dùng Advanced Find kèm điều kiện style Heading 2.",
                "You did not use Advanced Find with the Heading 2 style condition.",
            ),
            (
                "Chưa thu được bằng chứng Advanced Find. Không kết luận học sinh làm sai.",
                "No evidence captured for Advanced Find. This is not counted as a wrong answer.",
            ),
            ("Không ghi nhận được Advanced Find.", "Advanced Find could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Mở **Advanced Find**.", "Open **Advanced Find**."),
            (
                "Gõ toy. Bấm **More**, vào **Format** > **Style** rồi chọn **Heading 2**.",
                "Type toy. Click **More**, then **Format** > **Style** and select **Heading 2**.",
            ),
            (
                "Bấm **Find Next** để tìm Toy/toy chỉ trong **Heading 2**.",
                "Click **Find Next** to locate Toy/toy only within **Heading 2**.",
            ),
        ),
    },
    {
        "id": "W11-B01",
        "objective": "1.1",
        "kc": ["KC-1.1.2"],
        "kind": "artifact",
        "weight": 10,
        "prompt": t(
            "Trong phần Contact Us, chọn tên Lola Jacobsen và chèn một bookmark tên SalesManager.",
            "In the Contact Us section, select the name Lola Jacobsen and insert a bookmark named "
            "SalesManager.",
        ),
        "selector": {"bookmark": "SalesManager"},
        "predicate": {"type": "bookmark_range", "name": "SalesManager", "text": "Lola Jacobsen"},
        "evidence_policy": "file",
        "feedback": fb(
            (
                "SalesManager gắn đúng tên Lola Jacobsen.",
                "SalesManager correctly covers the name Lola Jacobsen.",
            ),
            (
                "Bookmark SalesManager phải phủ đúng chữ Lola Jacobsen.",
                "The SalesManager bookmark must cover exactly the text Lola Jacobsen.",
            ),
            ("Chưa đọc được bookmark SalesManager.", "The SalesManager bookmark could not be read."),
            ("Không đọc được bookmark trong tệp.", "Bookmarks could not be read from the file."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Bôi đen đúng chữ Lola Jacobsen.", "Select exactly the text Lola Jacobsen."),
            (
                "Tab **Insert**, nhóm **Links**, bấm **Bookmark**.",
                "On the **Insert** tab, in the **Links** group, click **Bookmark**.",
            ),
            ("Đặt tên SalesManager rồi bấm **Add**.", "Enter SalesManager, then click **Add**."),
        ),
    },
    {
        "id": "W11-B02",
        "objective": "1.1",
        "kc": ["KC-1.1.2"],
        "kind": "artifact",
        "weight": 10,
        "prompt": t(
            "Gắn một bookmark tên DesignManager vào tên Sarah Jones.",
            "Attach a bookmark named DesignManager to the name Sarah Jones.",
        ),
        "selector": {"bookmark": "DesignManager"},
        "predicate": {"type": "bookmark_range", "name": "DesignManager", "text": "Sarah Jones"},
        "evidence_policy": "file",
        "feedback": fb(
            ("DesignManager gắn đúng tên Sarah Jones.", "DesignManager correctly covers the name Sarah Jones."),
            (
                "Bookmark DesignManager phải phủ đúng chữ Sarah Jones trong phần Contact Us.",
                "The DesignManager bookmark must cover exactly the text Sarah Jones in the Contact Us "
                "section.",
            ),
            ("Chưa đọc được bookmark DesignManager.", "The DesignManager bookmark could not be read."),
            ("Không đọc được bookmark trong tệp.", "Bookmarks could not be read from the file."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            (
                "Trong phần Contact Us, bôi đen đúng chữ Sarah Jones.",
                "In the Contact Us section, select exactly the text Sarah Jones.",
            ),
            ("**Insert** > **Bookmark**.", "**Insert** > **Bookmark**."),
            ("Đặt tên DesignManager rồi bấm **Add**.", "Enter DesignManager, then click **Add**."),
        ),
    },
]

TOC_LINKS = [
    ("W11-H01", "New Electronic Favorites"),
    ("W11-H02", "Why Buy Wingtip Toys?"),
    ("W11-H03", "Recognition"),
    ("W11-H04", "Make It Your Own"),
    ("W11-H05", "Hand-Carved Toys"),
    ("W11-H06", "Resources"),
]

for cid, label in TOC_LINKS:
    criteria.append(
        {
            "id": cid,
            "objective": "1.1",
            "kc": ["KC-1.1.2"],
            "kind": "artifact",
            "weight": 7,
            "prompt": t(
                f"Chèn siêu liên kết từ dòng mục lục {label} tới heading tương ứng trong tài liệu.",
                f"Insert a hyperlink from the table of contents entry {label} to the corresponding "
                f"heading in the document.",
            ),
            "selector": {"toc_label": label},
            "predicate": {"type": "internal_hyperlink", "text": label, "heading": label},
            "evidence_policy": "file",
            "feedback": fb(
                (
                    f"Mục lục dẫn đúng heading {label}.",
                    f"The table of contents entry links to the {label} heading.",
                ),
                (
                    f"{label} phải là liên kết nội bộ tới đúng heading cùng tên, "
                    "không phải liên kết web và không trỏ trùng đích với dòng khác.",
                    f"{label} must be an internal link to the heading of the same name — not a web "
                    "link, and not pointing to the same target as another entry.",
                ),
                ("Chưa đọc được siêu liên kết mục lục.", "The table of contents hyperlink could not be read."),
                ("Không đọc được siêu liên kết trong tệp.", "Hyperlinks could not be read from the file."),
            ),
            "version": "1.1.0",
            "help_steps": steps(
                (f"Bôi đen dòng mục lục {label}.", f"Select the table of contents entry {label}."),
                (
                    "**Insert** > **Link** > **Place in This Document**.",
                    "**Insert** > **Link** > **Place in This Document**.",
                ),
                (
                    f"Chọn heading {label} rồi bấm **OK**.",
                    f"Select the {label} heading, then click **OK**.",
                ),
            ),
        }
    )

criteria += [
    {
        "id": "W11-N01",
        "objective": "1.1",
        "kc": ["KC-1.1.3"],
        "kind": "action_sequence",
        "weight": 6,
        "prompt": t(
            "Về đầu tài liệu, rồi dùng Go To chuyển lần lượt giữa các graphic cho tới graphic cuối cùng.",
            "Return to the beginning of the document, then use the Go To function to move between "
            "graphics until you reach the end.",
        ),
        "selector": {"action": "goto_graphic"},
        "predicate": {"type": "goto_graphic"},
        "evidence_policy": "required_method",
        "feedback": fb(
            (
                "Đã dùng Go To Graphic từ đầu tài liệu đến đối tượng cuối.",
                "Used Go To Graphic from the start of the document to the last object.",
            ),
            (
                "Chưa dùng Go To Graphic; cuộn chuột hay bấm trực tiếp vào ảnh không tính.",
                "You did not use Go To Graphic; scrolling or clicking an image directly does not count.",
            ),
            (
                "Chưa thu được bằng chứng Go To Graphic. Không kết luận học sinh làm sai.",
                "No evidence captured for Go To Graphic. This is not counted as a wrong answer.",
            ),
            ("Không ghi nhận được Go To Graphic.", "Go To Graphic could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Đưa con trỏ về đầu tài liệu (Ctrl+Home).", "Move the cursor to the start of the document (Ctrl+Home)."),
            ("Nhấn Ctrl+G để mở **Go To**, chọn **Graphic**.", "Press Ctrl+G to open **Go To**, then select **Graphic**."),
            (
                "Bấm **Next** liên tiếp tới đối tượng **Graphic** cuối cùng.",
                "Click **Next** repeatedly until you reach the last **Graphic**.",
            ),
        ),
    },
    {
        "id": "W11-N02",
        "objective": "1.1",
        "kc": ["KC-1.1.3"],
        "kind": "action_sequence",
        "weight": 6,
        "prompt": t(
            "Từ graphic cuối cùng, dùng Go To để về đầu trang 3.",
            "From the last graphic, use Go To to move to the top of page 3.",
        ),
        "selector": {"action": "goto_page", "page": 3},
        "predicate": {"type": "goto_page", "page": 3},
        "evidence_policy": "required_method",
        "feedback": fb(
            ("Đã dùng Go To Page tới đầu trang 3.", "Used Go To Page to reach the top of page 3."),
            ("Chưa dùng Go To để tới đầu trang 3.", "You did not use Go To to reach the top of page 3."),
            (
                "Chưa thu được bằng chứng Go To Page. Không kết luận học sinh làm sai.",
                "No evidence captured for Go To Page. This is not counted as a wrong answer.",
            ),
            ("Không ghi nhận được Go To Page.", "Go To Page could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Từ vị trí graphic cuối, nhấn Ctrl+G.", "From the last graphic, press Ctrl+G."),
            ("Chọn **Page**, gõ 3.", "Select **Page** and type 3."),
            ("Bấm **Go To** để về đầu trang 3.", "Click **Go To** to move to the top of page 3."),
        ),
    },
    {
        "id": "W11-N03",
        "objective": "1.1",
        "kc": ["KC-1.1.3"],
        "kind": "action_sequence",
        "weight": 6,
        "prompt": t(
            "Từ đầu trang 3, dùng Go To để nhảy tới bookmark SalesManager.",
            "From the top of page 3, use Go To to move to the SalesManager bookmark.",
        ),
        "selector": {"action": "goto_bookmark", "bookmark": "SalesManager"},
        "predicate": {"type": "goto_bookmark", "name": "SalesManager"},
        "evidence_policy": "required_method",
        "feedback": fb(
            ("Đã dùng Go To Bookmark SalesManager.", "Used Go To Bookmark to reach SalesManager."),
            (
                "Chưa dùng Go To Bookmark; đứng sẵn ở vị trí bookmark thì chưa tính.",
                "You did not use Go To Bookmark; already having the cursor there does not count.",
            ),
            (
                "Chưa thu được bằng chứng Go To Bookmark. Không kết luận học sinh làm sai.",
                "No evidence captured for Go To Bookmark. This is not counted as a wrong answer.",
            ),
            ("Không ghi nhận được Go To Bookmark.", "Go To Bookmark could not be recorded."),
        ),
        "version": "1.1.0",
        "help_steps": steps(
            ("Từ đầu trang 3, nhấn Ctrl+G.", "From the top of page 3, press Ctrl+G."),
            ("Chọn **Bookmark**.", "Select **Bookmark**."),
            ("Chọn SalesManager rồi bấm **Go To**.", "Select SalesManager, then click **Go To**."),
        ),
    },
]

rubric = {
    "schema": "mos-kulkul-1",
    "project_id": "word-objective-1-1",
    "rubric_version": "1.1.0",
    "grader_version": "1.0.0",
    "langs": [VI, EN],
    "default_lang": VI,
    "title": t(
        "Word Objective 1.1 — Điều hướng trong tài liệu",
        "Word Objective 1.1 — Navigate within documents",
    ),
    "program": "word",
    "objective": "1.1",
    "source_file": "Word_1-1.docx",
    "max_score": 100,
    "capabilities": ["word.bookmarks", "word.internal_hyperlinks"],
    "criteria": criteria,
}

out = Path(__file__).resolve().parent.parent / "app" / "rubrics" / "word-objective-1-1.json"
out.write_text(json.dumps(rubric, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("wrote", out, out.stat().st_size, "bytes")
print("criteria:", len(criteria), "total weight:", sum(c["weight"] for c in criteria))
