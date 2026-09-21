# MOS-KulKul — cập nhật sau 5 quyết định

**Ngày:** 21/09/2026
**Tiếp nối:** `ra-soat-mos-kulkul.md`
**Kèm mã nguồn:** `app/i18n.py`, `app/rubrics/word-objective-1-1.json` (bản song ngữ), `scripts/rubric_tool.py`

---

## 1. Năm quyết định và hệ quả

| # | Quyết định | Hệ quả |
|---|---|---|
| 1 | Phòng máy **Windows đồng nhất** | Bỏ được mối lo bằng chứng trên macOS. Mở đường cho UI Automation để chấm Go To |
| 2 | **Word 2019 tiếng Anh** | P3-B (nhận dạng đối tượng theo tên) hạ từ "phải sửa" xuống "nên sửa" — tên tiếng Anh khớp heuristic hiện có |
| 3 | Phần mềm ôn tập **song ngữ Việt/Anh** | Việc mới. Đã dựng xong lớp nền, xem mục 4 |
| 4 | **Thi chạy offline** | Loại bỏ phương án đơn giản nhất cho P1-A. Phải thiết kế lại, xem mục 2 |
| 5 | Rubric Excel/PowerPoint **dùng thật** | P1-C chuyển từ "nên rà" thành **việc chặn** — không được dạy thi khi chưa rà |

Quyết định 2 cần một lưu ý: heuristic `"picture" in name` chạy đúng với Word 2019 English vì Word đặt tên `Picture 1`, `Diagram 1`, `3D Model 1`, `Text Box 1`. Nhưng nó vẫn xếp nhầm một ảnh học sinh đặt tên `model-nha.png` thành mô hình 3D. Sửa sang `a:graphicData/@uri` chỉ mất một buổi và khỏi lo về sau — cứ để trong danh sách, chỉ là không còn gấp.

---

## 2. Thi offline — thiết kế toàn vẹn bằng chứng

### 2.1 Nói trước điều quan trọng nhất

**Thi offline trên máy học sinh tự cầm thì không thể chống gian lận tuyệt đối.** Bất kỳ khóa bí mật nào nằm trên máy đó đều có thể bị moi ra bởi người dùng chính máy. Mục tiêu khả thi là: **làm cho gian lận vặt trở nên bất khả, và gian lận nghiêm túc để lại dấu vết**. Toàn bộ thiết kế dưới đây đặt trên mục tiêu đó, không hứa hẹn hơn.

Có một điều an ủi lớn: **62/100 điểm của Word 1.1 vốn đã an toàn.** Điểm `artifact` được server tính lại từ chính file .docx nộp lên — học sinh muốn ăn điểm thì phải làm ra tài liệu đúng, tức là phải làm đúng việc đang được đo. Chỉ 38 điểm `action_sequence` là phụ thuộc nhật ký do client ghi.

### 2.2 Bốn lớp, từ rẻ tới đắt

**Lớp 1 — Chặn đường demo (nửa ngày, làm ngay).**
`ActionEvidence.RecordRubric()` phải không chạy khi `ExamSession.Mode == "testing"` (**đính chính:** repo dùng `"testing"`, không phải `"exam"` — xem `ExamSession.cs:9`). Sự kiện có `detail == "demo_all"` bị cả client lẫn server loại bỏ. Thêm trường tường minh `synthetic: true` thay vì dựa vào chuỗi `detail`. Kèm một test hồi quy: nạp rubric Word 1.1, gọi `RecordRubric`, chấm ở chế độ thi → điểm action phải bằng 0, không phải 38.

Cũng ở lớp này: chế độ thi **cấm** thoái lui `legacy-text` trong `scoring.py`. Rubric thiếu `criteria` thì báo lỗi, không chấm.

**Lớp 2 — Chuỗi băm có khóa (3–4 ngày).**

Gói đề do server phát mang theo một khóa phiên `K` (32 byte ngẫu nhiên, sinh ở server, mỗi lần làm bài một khóa khác). Mỗi sự kiện được nối vào một chuỗi băm:

```
h₀ = HMAC(K, attempt_id ‖ nonce)
hᵢ = HMAC(K, hᵢ₋₁ ‖ canonical_json(eventᵢ))
```

Mỗi bản ghi lưu thêm `seq` và `mac = hᵢ`. Xóa, chèn, đổi thứ tự hay sửa bất kỳ sự kiện nào đều làm gãy chuỗi từ chỗ đó trở đi. Khi nộp, server dùng bản `K` của mình tính lại toàn chuỗi.

Khóa `K` **không** được nằm cạnh `actions.json` dưới dạng rõ. Trên Windows dùng DPAPI:

```csharp
ProtectedData.Protect(key, optionalEntropy: attemptIdBytes,
                      DataProtectionScope.CurrentUser);
```

DPAPI CurrentUser chặn được người dùng khác và chặn được việc bê ổ cứng sang máy khác đọc — nhưng **không** chặn được chính học sinh chạy một đoạn mã dưới tài khoản của mình. Đó là giới hạn thật của mọi giải pháp offline; nói rõ để không kỳ vọng sai.

**Lớp 3 — Neo chuỗi vào tài liệu (1 ngày).**

Tại mỗi checkpoint và khi nộp, chèn một sự kiện `{action: "snapshot", sha256: <băm của .docx hiện tại>}` vào chuỗi. Server băm lại file nhận được và đối chiếu với snapshot cuối. Việc này buộc "những gì đã thao tác" phải khớp với "những gì đã tạo ra" — một nhật ký đẹp kèm file trắng sẽ lộ ngay.

**Lớp 4 — Kiểm tra hợp lý phía server (2 ngày, không cần mật mã).**

Những luật này bắt được gần hết các trò vụng, và chạy được ngay cả khi chưa làm lớp 2:

- mốc thời gian phải tăng dần và nằm trong khung giờ làm bài;
- không có sự kiện nào sau `submitted_at`;
- tổng thời gian so với số nhiệm vụ — 7 nhiệm vụ trong 90 giây là cờ đỏ;
- `hits` của `find_navigate` cao bất thường;
- trùng `id` sự kiện;
- `attempt_id` ghi trong `docProps/custom.xml` của file nộp phải khớp file đã phát.

### 2.3 Chính sách quan trọng hơn mật mã

Hai nguyên tắc nên viết vào quy chế, không chỉ vào code:

**Một — điểm thao tác là điểm tạm tính.** Khi nộp offline xong và máy nối mạng lại, server mới xác minh chuỗi. Trước đó, màn hình hiện điểm `artifact` là **chính thức**, điểm `action_sequence` ghi **"đang xác minh"**. Chuỗi gãy thì **không tự động cho 0** — có thể do máy treo hay lệch đồng hồ — mà gắn cờ cho giáo viên xem lại. Đây đúng tinh thần `unverified` mà repo đã chọn: thiếu bằng chứng không đồng nghĩa với làm sai.

**Hai — hạ tỉ trọng điểm thao tác.** Đây là chỗ hai vấn đề gặp nhau: ba nhiệm vụ Go To (18 điểm) hiện **không có nguồn bằng chứng thật nào**, và điểm thao tác lại chính là phần dễ giả mạo nhất khi thi offline. Chuyển chúng sang dạng để lại dấu vết trong file thì giải quyết được cả hai:

> *"Dùng Go To để tới ảnh cuối cùng trong tài liệu, rồi chèn ngay dưới ảnh đó một chú thích **Hình cuối**."*

Kết quả kiểm bằng `artifact` — chắc chắn, không giả mạo được, không cần bằng chứng thao tác. Học sinh vẫn phải biết Go To để làm nhanh, chỉ là hệ thống đo cái để lại dấu vết. Làm vậy cho cả 3 nhiệm vụ Go To thì Word 1.1 đi từ **62 điểm chắc chắn** lên **80 điểm chắc chắn**, và phần dễ tấn công co lại còn 20.

Với những kỹ năng mà thao tác *chính là* nội dung cần đo và không thể chuyển thể (Find nâng cao, Navigation pane), dùng **UI Automation** trên Windows — `System.Windows.Automation` đọc được hộp thoại Find and Replace vì nó là cửa sổ Win32 thật. Không cần VSTO, không cần ký số add-in. Đây cũng là cách sửa triệt để lỗi `results_tab` suy diễn ở P2-A. Phòng máy đồng nhất Windows nên đường này khả thi.

---

## 3. Ngưỡng chất lượng rubric — nay là việc chặn

Vì Excel/PowerPoint sẽ dùng thật, `excel-objective-2-1` **không được phép** lên lớp ở trạng thái hiện tại: 50 điểm cho `contains_text: "Item 2"` (gõ tay là đỗ, mà lại còn là chuỗi con của "Item 20") và 50 điểm cho `sheet_named: "Sheet1"` (bấm dấu `+` là đỗ).

Tôi đã viết bộ luật thành công cụ chạy được — `scripts/rubric_tool.py`. Sáu luật:

| # | Luật | Mức |
|---|---|---|
| 1 | Tổng `weight` phải bằng `max_score` | lỗi |
| 2 | Không hai criterion nào trùng `predicate` | lỗi |
| 3 | `contains_text` / `not_contains_text` không gánh quá 20 điểm | lỗi |
| 4 | Rubric phải có `criteria` (nếu không sẽ rơi xuống `legacy-text`) | lỗi |
| 5 | `feedback.fail` không dùng chung giữa các criterion | cảnh báo |
| 6 | `action_sequence` phải khai `evidence_policy`; criterion phải gắn `kc[]` | cảnh báo |

Chạy thử trên hai rubric tái dựng đúng nguyên trạng repo, cộng bản Word 1.1 mới:

```
excel-objective-2-1.json
  ✗ E21-F01 dùng contains_text cho 50 điểm — chỉ dò chuỗi, học sinh gõ tay cũng đỗ (ngưỡng 20)
  ! feedback.fail dùng chung cho E21-F01, E21-S01: «Chưa khớp file kết quả Study Guide.»
  ! chưa gắn kc[]: E21-F01, E21-S01

powerpoint-objective-1-1.json
  ✗ P11-L02 trùng predicate với P11-L01 — không phân biệt được hai kỹ năng
  ✗ P11-L03 trùng predicate với P11-L01 — không phân biệt được hai kỹ năng

rubric                               EN %   lỗi  cảnh báo
---------------------------------------------------------
excel-objective-2-1.json              0.0     1         2
powerpoint-objective-1-1.json         0.0     2         2
word-objective-1-1.json             100.0     0         0
```

Đưa `python3 scripts/rubric_tool.py check app/rubrics --strict` vào CI thì rubric yếu không lọt được vào bản phát hành. Đây chính là thứ cho phép giao việc nhập liệu cho người khác mà không mất kiểm soát chất lượng — xem mục 5.

---

## 4. Lớp song ngữ

### 4.1 Nguyên tắc

**Tên lệnh ribbon giữ nguyên tiếng Anh ở cả hai ngôn ngữ.** Học sinh thi trên Word 2019 English; nếu bản tiếng Việt viết "vào thẻ Chèn > Liên kết" thì các em sẽ không tìm thấy gì trong phòng thi. Đúng cách:

> Tab **Insert**, nhóm **Links**, bấm **Bookmark**.

Repo đã sẵn phong cách này trong `help_steps` của Word 1.1 — tôi chỉ ghi nó thành quy tắc.

**Bản tiếng Anh lấy từ đề gốc, không dịch ngược từ tiếng Việt.** Study Guide đã có sẵn câu chữ tiếng Anh của chính những nhiệm vụ này. Dùng lại vừa nhanh hơn dịch, vừa cho học sinh tiếp xúc đúng thứ tiếng Anh sẽ gặp khi thi. Ví dụ W11-S05:

| | |
|---|---|
| VI | Dùng Advanced Find để tìm mọi chỗ xuất hiện của Toy hoặc toy có áp style Heading 2. |
| EN | Perform an advanced search for all instances of Toy or toy (either capitalized or lowercase) that have the Heading 2 style applied. |

**Chỉ dịch trường hiển thị.** `prompt`, `feedback.*`, `help_steps`, `title`. Còn `predicate`, `selector`, `id`, `weight`, `evidence_policy` là dữ liệu máy đọc — **không đụng vào**, kể cả `predicate.text` (đó là chuỗi cần tìm trong tài liệu tiếng Anh, không phải chuỗi hiển thị).

### 4.2 Cách lưu

Một rubric, một file, mỗi trường hiển thị là một khối song ngữ:

```json
"prompt": {
  "vi": "Từ Navigation pane, tìm tất cả các chỗ xuất hiện của to.",
  "en": "From the Navigation pane, locate all instances of to."
}
```

Không tách `rubrics/vi/` và `rubrics/en/` — hai thư mục song song chắc chắn sẽ lệch nhau sau vài tháng sửa đổi.

### 4.3 Cách nối vào hệ thống — một dòng

`app/i18n.py` nhận **cả chuỗi cũ lẫn khối mới**, nên 59 rubric chưa chuyển vẫn chạy nguyên. Localize **tại thời điểm nạp**, trước khi đưa vào chấm:

```python
from app import i18n

rubric = i18n.localize_rubric(load_rubric(src), lang)
result = grade.evaluate_facts(facts, rubric, evidence)
```

Sau lời gọi đó mọi trường hiển thị đã là chuỗi thuần, nên **phần chấm điểm của `grade.py` và toàn bộ `qmatrix.py` không phải sửa một dòng nào** — chúng đọc `criterion["feedback"][status]`, `criterion["prompt"]`, `criterion["help_steps"]` như chuỗi và vẫn nhận được chuỗi.

> **Đính chính khi nối dây thật.** Câu "không phải sửa dòng nào" chỉ đúng *sau khi* đã đặt lời gọi `localize_rubric` đúng chỗ — bản thân lời gọi đó là bắt buộc, không phải tùy chọn. Nạp rubric song ngữ mà chưa localize thì `qmatrix.py:131` gọi `.strip()` trên `dict` và **chấm bài văng lỗi `AttributeError`**, chứ không phải hiển thị xấu. Có **5 điểm nối** vì rubric đi vào hệ thống bằng 5 đường khác nhau:
>
> | Nơi nối | Vì sao |
> |---|---|
> | `grade.py: evaluate_facts()` | cổng chấm điểm duy nhất — che cho cả `grade.py` lẫn `qmatrix.py` |
> | `client_v1.py: _public_criteria()` | payload gửi cho client phải là chuỗi |
> | `bank.py` | `hint_tiers` và tên task lưu xuống DB |
> | `seed.py` | dựng `steps` hiển thị |
> | `demo_all.py` | tiêu đề trong báo cáo |
>
> **`load_rubric()` thì KHÔNG localize** — và đây là chỗ dễ sai nhất. `seed.py` lưu nguyên rubric vào `project_versions.rubric`; nếu localize ngay lúc nạp thì **bản EN bị ghi đè mất vĩnh viễn trong DB**. Nên `seed.py` giữ `rubric` nguyên khối song ngữ để lưu, và dùng một biến `display` riêng để dựng chuỗi.
>
> `evaluate_facts`, `grade_path`, `score_file` nay nhận thêm tham số `lang` (mặc định lấy `default_lang` của rubric, rồi mới tới `"vi"`). Đã kiểm: chấm cùng một file bằng `vi` và `en` cho **cùng điểm, cùng trạng thái**, chỉ khác câu chữ; và rubric nguồn không bị sửa tại chỗ.

API:

| Hàm | Việc |
|---|---|
| `localize_rubric(rubric, lang)` | Dẹp cả rubric về một ngôn ngữ. Đây là hàm duy nhất cần gọi |
| `pick(value, lang)` | Lấy một chuỗi, thiếu bản dịch thì lùi về tiếng Việt — giao diện không bao giờ trống |
| `missing(rubric, lang)` | Liệt kê đường dẫn tới trường chưa dịch, để CI chặn và để người nhập liệu biết còn thiếu gì |
| `coverage(rubric, lang)` | Phần trăm đã dịch, cho bảng theo dõi tiến độ |
| `normalize_lang(lang)` | `"en-US"`, `"VI"`, `"vi-VN"` → `"vi"` / `"en"` |

### 4.4 Đã làm và còn lại

`app/rubrics/word-objective-1-1.json` đã chuyển xong: 16 criterion, tổng trọng số 100, **EN 100%**, và tiện thể gắn luôn `kc[]` (`KC-1.1.1` Find, `KC-1.1.2` bookmark/hyperlink, `KC-1.1.3` Go To) — mở đường cho Bản đồ Năng lực.

59 rubric còn lại: chạy `python3 scripts/rubric_tool.py migrate app/rubrics`. Script dựng khung `{"vi": "…", "en": ""}`, thêm `kc: []`, **không ghi đè bản dịch đã có**, và chạy lại nhiều lần vẫn an toàn. Sau đó người nhập liệu chỉ việc điền vào chỗ trống, không phải viết JSON từ đầu.

### 4.5 Bảng thuật ngữ

Nên có `app/glossary.json` để 61 rubric gọi một khái niệm bằng đúng một từ:

```json
{ "bookmark":     { "vi": "bookmark (dấu trang)", "en": "bookmark",     "ribbon": "Insert > Links > Bookmark" },
  "section break":{ "vi": "ngắt section",          "en": "section break", "ribbon": "Layout > Breaks" } }
```

Giao diện hiện tooltip khi học sinh rê chuột vào thuật ngữ. Việc nhỏ, nhưng với học sinh lớp 11 lần đầu gặp giao diện tiếng Anh thì đây là thứ giảm tải nhận thức nhiều nhất.

---

## 5. Ai nhập liệu rubric

### 5.1 Khối lượng thật

Bản Word 1.1 song ngữ nặng 33 KB cho 16 criterion. Nhân cho **61 rubric** (**đính chính:** repo có 61 file, không phải 40 — 20 Word, 22 PowerPoint, 19 Excel), với yêu cầu predicate phải đo đúng kỹ năng chứ không dò chuỗi, đây là công việc **hàng trăm giờ**. Thầy làm một mình, song song với đứng lớp, việc tổ trưởng và nghiên cứu sinh — không khả thi, và nếu cố thì chất lượng sẽ rơi đúng vào kiểu `contains_text` đang thấy ở rubric Excel.

### 5.2 Đề nghị phân vai

| Vai | Ai | Làm gì |
|---|---|---|
| **Chủ biên** | Thầy | Sở hữu danh mục KC và Q-matrix; đặt ngưỡng chất lượng; duyệt. **Không** ôm việc nhập liệu |
| **Nhập liệu** | 2–3 giáo viên Tổ Tin học | Điền rubric qua công cụ, không đụng JSON |
| **Kiểm soát** | CI | `rubric_tool.py check --strict` chặn rubric yếu ở cổng merge |
| **Bản tiếng Anh** | Trích từ Study Guide | Không dịch từ tiếng Việt |

### 5.3 Thứ cần có trước: trình soạn rubric bằng diff

Giáo viên Tổ Tin học không viết được `predicate` OOXML, và cũng không nên phải học. Nhưng Drive đã có sẵn **cặp `Word_x-y.docx` và `Word_x-y_results.docx`** — trạng thái trước và sau của đúng bài đó. Đây là nguyên liệu để máy tự đề xuất:

1. Giáo viên tải lên 2 file.
2. Hệ thống giải nén, chuẩn hóa XML (bỏ `w:rsid*`, `w14:paraId`, `dcterms:modified`, `TotalTime`), diff theo cây.
3. Bộ suy luận đề xuất check: thấy `w:bookmarkStart` mới → đề xuất `bookmark_range`; thấy `w:pgMar` đổi → đề xuất `page_margins`; thấy `w:tblHeader` mới → đề xuất `table_repeat_header`.
4. Giáo viên bỏ đề xuất nhiễu, viết câu phản hồi tiếng Việt, gắn KC, đặt trọng số. Bản EN lấy từ Study Guide.

**Xây công cụ này trước khi nhập liệu hàng loạt, không phải sau.** Làm ngược thì 61 rubric bị soạn hai lần. Ước lượng 2 tuần cho công cụ, đổi lại mỗi rubric từ ~3 giờ xuống ~20 phút — hòa vốn ngay ở rubric thứ mười.

### 5.4 Thứ tự nhập liệu

1. **Excel + PowerPoint trước** — chúng sắp dùng thật mà đang yếu nhất.
2. **Word 1.2–1.4** — nối tiếp Objective 1 đã có bản mẫu tốt.
3. Phần còn lại theo trọng số kỳ thi: OG2 (20–25%) → OG3 (15–20%) → OG5 (15–20%) → OG4, OG6 (5–10%).

---

## 6. Thứ tự ưu tiên sửa lại

Thay cho bảng ở mục 4 của bản rà soát trước.

### Nhóm A — xong trước khi cho học sinh thi thật

| # | Việc | Ước lượng |
|---|---|---|
| A1 | Chặn `RecordRubric`/`demo_all` ở chế độ thi; loại sự kiện `synthetic` ở cả hai phía; test hồi quy | nửa ngày |
| A2 | Bỏ thoái lui `legacy-text` ở chế độ thi | nửa ngày |
| A3 | Kiểm tra hợp lý phía server (mục 2.2 lớp 4) | 2 ngày |
| A4 | Rà Excel + PowerPoint qua `rubric_tool.py check`, sửa hết lỗi | 1 tuần |

### Nhóm B — nền cho thi offline

| # | Việc | Ước lượng |
|---|---|---|
| B1 | Chuỗi băm HMAC + khóa phiên qua DPAPI | 3–4 ngày |
| B2 | Neo chuỗi vào băm tài liệu tại checkpoint | 1 ngày |
| B3 | Điểm thao tác hiện "đang xác minh"; chuỗi gãy thì gắn cờ, không cho 0 | 2 ngày |
| B4 | Chuyển 3 nhiệm vụ Go To sang dạng artifact (62 → 80 điểm chắc chắn) | 2 ngày |

### Nhóm C — chất lượng đo lường và mở rộng

| # | Việc | Ước lượng |
|---|---|---|
| C1 | Trình soạn rubric bằng diff | 2 tuần |
| C2 | Bổ sung facts `word_xml.py`: `pgMar`, `spacing`/`ind`, `documentProtection/@w:edit`, `sym`, cấp danh sách | 3–4 ngày |
| C3 | Chạy `migrate` cho 59 rubric, nhập bản EN theo thứ tự mục 5.4 | làm dần |
| C4 | Hai nút đầu Q-matrix trả `unverified` khi không có events | 1 ngày |
| C5 | UI Automation cho Find/Results tab (sửa triệt để P2-A) | 1 tuần |
| C6 | `drawing_kinds` chuyển sang `graphicData/@uri` | 1 ngày |
| C7 | `glossary.json` + tooltip thuật ngữ | 2 ngày |

A1 và A2 cộng lại đúng **một ngày công** và bịt hai đường cộng điểm sai. Nếu tuần này chỉ làm được một việc, làm hai việc đó.

---

## 7. File bàn giao

| File | Nội dung |
|---|---|
| `app/i18n.py` | Bộ truy xuất song ngữ. Nhận cả rubric cũ lẫn mới. Phần chấm của `grade.py` và `qmatrix.py` không phải sửa — nhưng vẫn cần 5 điểm nối `localize_rubric`, xem mục 4.3 |
| `app/rubrics/word-objective-1-1.json` | Bản song ngữ hoàn chỉnh, EN 100%, đã gắn `kc[]`. Dùng làm mẫu cho 59 file còn lại |
| `scripts/rubric_tool.py` | `migrate` dựng khung song ngữ; `check` chạy 6 luật chất lượng; `--strict` cho CI |
| `build_rubric.py` | Script đã dựng ra bản song ngữ trên. Giữ lại làm mẫu khi soạn rubric mới bằng tay |
| `fixtures/` | Hai rubric tái dựng đúng nguyên trạng repo, để kiểm chứng bộ luật bắt đúng lỗi thật |
