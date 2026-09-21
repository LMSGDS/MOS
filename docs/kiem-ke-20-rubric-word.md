# Kiểm kê 20 rubric Word — MOS-KulKul

**Ngày:** 21/09/2026
**Phạm vi:** đã đọc **trọn vẹn cả 20 file** `app/rubrics/word-objective-*.json`
**Tiếp nối:** `ra-soat-mos-kulkul.md`, `cap-nhat-mos-kulkul.md`

---

## 1. Đính chính: MO-100 có 6 nhóm objective, không phải 5

| Nhóm | Tên | Trọng số kỳ thi | Rubric trong repo |
|---|---|---|---|
| 1 | Manage documents | 20–25% | 1-1, 1-2, 1-3, 1-4 |
| 2 | Insert and format text, paragraphs, and sections | 20–25% | 2-1, 2-2, 2-3 |
| 3 | Manage tables and lists | 15–20% | 3-1, 3-2, 3-3 |
| 4 | Create and manage references | 5–10% | 4-1, 4-2a, 4-2b, 4-2c |
| 5 | Insert and format graphic elements | 15–20% | 5-1, 5-2, 5-3, 5-4 |
| 6 | Manage document collaboration | 5–10% | 6-1, 6-2 |

**20 file, 101 tiêu chí, phủ đủ cả 6 nhóm.** Việc tôi mới chuyển song ngữ 1.1 là do cố ý làm bản mẫu trước — không phải vì repo thiếu. Nhưng sau khi đọc hết cả 20 file thì lý do dừng lại hóa ra còn chính đáng hơn tôi nghĩ: **khoảng 6 rubric cần sửa predicate trước khi dịch**, nếu dịch bây giờ thì phần lớn công sẽ phải làm lại. Chi tiết ở mục 4.

---

## 2. Bảng kiểm kê

Cột **thao tác** = điểm thuộc loại `action_sequence`. Cột **điểm chết** = trong số đó, phần **không có bất kỳ nguồn bằng chứng nào** (giải thích ở mục 3). Cột **yếu** = điểm gánh bởi `contains_text` / `not_contains_text` vượt ngưỡng 20.

| rubric | TC | Σđ | thao tác | điểm chết | yếu |
|---|---:|---:|---:|---:|---:|
| 1-1 Navigate within documents | 16 | 100 | 38 | **18** | 0 |
| 1-2 Format documents | 7 | 100 | 0 | 0 | 0 |
| 1-3 Save and share documents | 6 | 100 | 45 | **45** | 0 |
| 1-4 Inspect documents for issues | 5 | 100 | 30 | **30** | 0 |
| 2-1 Insert text and paragraphs | 3 | 100 | 0 | 0 | **80** |
| 2-2 Format text and paragraphs | 9 | 100 | 0 | 0 | 0 |
| 2-3 Create and configure sections | 5 | 100 | 0 | 0 | 0 |
| 3-1 Create tables | 4 | 100 | 0 | 0 | 0 |
| 3-2 Modify tables | 7 | 100 | 0 | 0 | 0 |
| 3-3 Create and modify lists | 5 | 100 | 0 | 0 | 0 |
| 4-1 Reference elements | 3 | 100 | 0 | 0 | **30** |
| 4-2a Insert a table of contents | 3 | 100 | 0 | 0 | 0 |
| 4-2b Modify a table of contents | 3 | 100 | 0 | 0 | 0 |
| 4-2c Insert a bibliography | 3 | 100 | 0 | 0 | **60** |
| 5-1 Insert illustrations and text boxes | 5 | 100 | 0 | 0 | 0 |
| 5-2 Format illustrations | 3 | 100 | 0 | 0 | 0 |
| 5-3 Add text to graphic elements | 2 | 100 | 0 | 0 | 0 |
| 5-4 Modify graphic elements | 3 | 100 | 0 | 0 | 0 |
| 6-1 Add and manage comments | 4 | 100 | 0 | 0 | 0 |
| 6-2 Manage change tracking | 5 | 100 | 0 | 0 | 0 |
| **TỔNG** | **101** | **2000** | **113** | **93** | **170** |

Tin tốt trước: **tổng trọng số của cả 20 file đều đúng bằng 100** — không file nào lệch. Đó là dấu hiệu rubric được soạn có kỷ luật.

---

## 3. Phát hiện lớn nhất: 93/113 điểm thao tác không có nguồn bằng chứng

Đối chiếu `WordActionProbe.cs` với predicate mà rubric dùng cho thấy một khoảng trống rộng hơn nhiều so với con số "38 điểm" ghi trong README.

`WordActionProbe.Poll()` chỉ đọc trạng thái `word.Selection.Find`. Nó sinh ra được đúng **bốn** loại sự kiện:

```
find · advanced_find · results_tab · find_navigate
```

Trong khi rubric đang dùng **tám loại khác** mà không có dòng mã nào sinh ra:

| Loại thao tác | Dùng ở | Điểm |
|---|---|---|
| `goto_graphic`, `goto_page`, `goto_bookmark` | 1-1 (N01–N03) | 18 |
| `save_alternate_format` | 1-3 (S01) | 15 |
| `print_settings` | 1-3 (S02) | 15 |
| `share_electronic` | 1-3 (S03) | 15 |
| `inspect_document` | 1-4 (I01) | 15 |
| `compatibility_check` | 1-4 (X01) | 15 |
| | **Tổng** | **93** |

Nghĩa là:

- **Word 1.3 — 45/100 điểm** vĩnh viễn `unverified`. Học sinh làm đúng hết vẫn chỉ hiện tối đa 55.
- **Word 1.4 — 30/100 điểm** vĩnh viễn `unverified`.
- **Word 1.1 — 18/100 điểm** vĩnh viễn `unverified` (20 điểm Find thì chấm được).

Con số 38 trong README chỉ tính riêng 1.1. Nhìn cả bộ thì **chỉ 20 trong số 113 điểm thao tác là thật sự chấm được**.

Tôi đã đưa luật này vào `scripts/rubric_tool.py` để không phải dò tay nữa:

```
word-objective-1-3.json
  ✗ W13-S01 (15 điểm) dùng thao tác «save_alternate_format» — WordActionProbe
    không sinh bằng chứng cho loại này, nên tiêu chí vĩnh viễn unverified.
    Chuyển sang dạng artifact hoặc bổ sung bộ ghi nhận
  ✗ tổng 45/100 điểm không có nguồn bằng chứng nào
```

### Cách xử lý

Ba trong tám loại này **chuyển sang dạng artifact được ngay**, vì chúng thật sự để lại dấu vết trong file — chỉ là `word_xml.py` chưa trích:

| Thao tác | Dấu vết trong OOXML | Việc cần làm |
|---|---|---|
| `inspect_document` (gỡ thông tin cá nhân) | `settings.xml/w:removePersonalInformation`, `dc:creator` rỗng | thêm facts, đổi sang `artifact` |
| `compatibility_check` | `settings.xml/w:compatSetting[@w:name="compatibilityMode"]` | thêm facts, đổi sang `artifact` |
| `save_alternate_format` | chính phần mở rộng tệp nộp kèm (.pdf/.doc) | cho nộp kèm tệp thứ hai, kiểm content-type |

Hai loại còn lại thì không để dấu vết thật:

- `print_settings`, `share_electronic` — chỉ mở hộp thoại, không đổi tài liệu. Hoặc dựng UI Automation, hoặc **bỏ khỏi phần chấm điểm** và chuyển thành câu hỏi lý thuyết ngắn.
- `goto_*` — như đã bàn: đổi đề thành *"dùng Go To tới ảnh cuối, rồi chèn chú thích Hình cuối ngay dưới ảnh đó"*, kiểm bằng artifact.

---

## 4. Sáu rubric có tiêu chí không đo đúng thứ cần đo

Đây là các lỗi phải đọc từng file mới thấy — máy không tự bắt được, vì predicate hợp lệ, chỉ là **đo nhầm thứ**.

### 4.1 `W32-D01` — "Xóa cột ID" (15 điểm), kiểm bằng `table_has_text: "Customer"`

Tiêu chí này kiểm xem bảng có chứa chữ *Customer* hay không. Nhưng *Customer* vốn đã nằm trong hàng tiêu đề gộp của bảng **từ trước khi học sinh làm gì**. Không xóa cột ID vẫn đỗ; xóa nhầm cột khác cũng đỗ. **15 điểm cho không.**

### 4.2 `W33-R01` — "Restart numbering" (20 điểm), kiểm bằng `style_used: ListParagraph, min: 20`

Đếm xem có ≥ 20 đoạn mang style `ListParagraph`. Đây là đo *số lượng mục danh sách*, hoàn toàn không liên quan tới việc có bắt đầu lại đánh số hay không. Nguyên nhân gốc: `word_xml.py` chưa trích `w:num/w:lvlOverride/w:startOverride`, nên người soạn phải lấy một đại lượng thay thế.

### 4.3 `W61-A01` — "Reply mang tên Joan Lambert" (15 điểm), kiểm bằng `comment_author: "Joan Lambert"`

Chính `help_steps` đã tự thú nhận vấn đề: *"Bài mẫu dùng Joan Lambert; máy học sinh dùng tên đăng nhập Word."* Khi học sinh bấm Reply, Word gắn **tên tài khoản của chính em đó**, không bao giờ là Joan Lambert. Nên tiêu chí này hoặc **đỗ miễn phí** (vì file gốc đã sẵn có comment của Joan Lambert), hoặc **không ai đỗ được**. Cả hai đều sai.

Sửa: đổi sang `comment_reply` (predicate này đã có sẵn trong `grade.py`, đọc `w15:paraIdParent`) — nó đo đúng "có trả lời comment hay không", không phụ thuộc tên người dùng.

### 4.4 `W62-L01` — "Lock Tracking" (15 điểm), kiểm bằng `document_protection`

`word_xml.py` chỉ trả về boolean *"có phần tử documentProtection hay không"*. Học sinh bật **bất kỳ** kiểu Restrict Editing nào — chỉ đọc, chỉ điền form, chỉ nhận xét — đều đỗ. Muốn đo đúng phải đọc `@w:edit="trackedChanges"` và `@w:enforcement="1"`.

### 4.5 `W42C-H01` — "Có tiêu đề References" (30 điểm), kiểm bằng `contains_text: "References"`

*References* là tên một tab trên ribbon và là một từ rất thường gặp. Nhiều khả năng tài liệu gốc đã chứa nó. Nên dùng `style_used: Bibliography` kết hợp `field_contains: BIBLIOGRAPHY`.

### 4.6 `W42A-T01/H01/E01` — ba tiêu chí, một thao tác

Ba predicate khác nhau (`field_contains: TOC`, `style_used: TOCHeading`, `style_used: TOC1`) nhưng **cả ba đều được thỏa bởi đúng một hành động**: Insert > Table of Contents > Automatic Table. Bộ luật của tôi không bắt được vì predicate không trùng nhau — nhưng về mặt sư phạm, đây vẫn là 100 điểm cho một cú bấm.

Tương tự nhẹ hơn ở **5-3**: `W53-T01` (min 1) và `W53-T02` (min 2) cùng predicate `drawing_text`, chỉ khác ngưỡng — đỗ cái chặt thì cái lỏng luôn đỗ. Luật mới đã bắt được ca này.

---

## 5. Rubric bị bó hẹp bởi facts, không phải bởi objective

Một mẫu hình lặp lại: rubric chỉ phủ những gạch đầu dòng mà `word_xml.py` nhìn thấy được, bỏ trống phần còn lại của objective.

| Objective | Skills Measured yêu cầu | Rubric hiện phủ | Bỏ trống |
|---|---|---|---|
| 2.2 Format text and paragraphs | text effects, Format Painter, **line/paragraph spacing**, **indentation**, built-in styles, **clear formatting** | 8 tiêu chí style + 1 text effect | spacing, indentation, clear formatting |
| 3.2 Modify tables | sort, **cell margins & spacing**, merge/split, **resize**, **split tables**, repeat header | sort, merge, repeat header, thêm/xóa cột | cell margins, resize, split table |
| 3.3 Create and modify lists | bullet/number, **đổi ký tự bullet**, **bullet tùy biến**, **tăng/giảm cấp**, **restart numbering**, **đặt số bắt đầu** | bullet + numFmt cấp 0 | 4/6 gạch đầu dòng |
| 4.1 Reference elements | footnote, **thuộc tính footnote/endnote**, **tạo nguồn trích dẫn**, chèn citation | footnote count, CITATION field | endnote, thuộc tính, nguồn |
| 5.1 Insert illustrations | shapes, pictures, 3D, SmartArt, **screenshots**, text boxes | 5 loại | screenshots |

**Nguyên nhân chung là mục C2 trong bản cập nhật trước**: thiếu `w:pgMar`, `w:spacing`, `w:ind`, `w:sym`, `w:numPr/w:ilvl`, `w:startOverride`, `w:tblCellMar`, `w:footnotePr`, `customXml` sources. Bổ sung các facts này không chỉ sửa được 4.2 và 4.4 ở trên, mà còn **mở ra khoảng 15 tiêu chí mới** trên toàn bộ ba objective 2.2, 3.2, 3.3.

Đây là lý do `word_xml.py` nên sửa **trước** khi nhập liệu hàng loạt: nếu không, người soạn rubric sẽ tiếp tục phải lấy đại lượng thay thế, và ta lại có thêm những ca như `W33-R01`.

---

## 6. Vì sao chưa nên dịch cả 20 file ngay

Gộp các phát hiện lại:

| Nhóm | Rubric | Trạng thái |
|---|---|---|
| **Cần sửa thiết kế trước khi dịch** | 1-3, 1-4 | 45 và 30 điểm không chấm được — phải đổi dạng nhiệm vụ |
| **Cần sửa predicate trước khi dịch** | 3-2, 3-3, 4-2c, 6-1, 6-2 | có tiêu chí đo nhầm, câu chữ sẽ đổi theo |
| **Nên xem lại trọng số** | 2-1, 4-1, 4-2a, 5-3 | predicate yếu gánh điểm lớn, hoặc chồng lấn |
| **Dịch được ngay** | 1-2, 2-2, 2-3, 3-1, 4-2b, 5-1, 5-2, 5-4 | 8 file, nội dung ổn định |

Dịch một tiêu chí sắp bị viết lại là làm hai lần. Nên thứ tự đúng là **sửa predicate → rồi dịch**, và bắt đầu từ 8 file đã ổn định.

**Đã làm xong 2/20:**
- `word-objective-1-1.json` — 16 tiêu chí, EN 100%, gắn `kc[]`. Bản EN lấy từ practice tasks của Study Guide.
- `word-objective-1-2.json` — 7 tiêu chí, EN 100%, gắn `kc[]`. Mẫu cho rubric thuần `artifact`.

> Lưu ý về 1-2: tôi không có trang practice tasks của Study Guide cho mục này, nên phần EN là do tôi soạn bám sát nhiệm vụ, dùng đúng tên lệnh Word 2019 English. Nên đối chiếu nhanh với Study Guide trước khi phát cho học sinh. Bản 1-1 thì lấy trực tiếp từ đề gốc.

---

## 7. Ba luật mới trong `rubric_tool.py`

Bổ sung sau khi đọc hết 20 file:

| # | Luật | Mức | Bắt được |
|---|---|---|---|
| 7 | `action_sequence` dùng thao tác mà `WordActionProbe` không sinh bằng chứng | lỗi | 93 điểm chết ở 1-1, 1-3, 1-4 |
| 8 | Hai tiêu chí cùng predicate, chỉ khác ngưỡng (`min`/`max`/`rows`/`cols`) | cảnh báo | 5-3 |
| 3′ | Predicate yếu gánh điểm lớn — nay có thể miễn trừ bằng `"weak_ok": "<lý do>"` | lỗi hoặc cảnh báo | 2-1 (chèn ký hiệu ® — kết quả *chính là* chuỗi, nên miễn trừ hợp lý) |

Luật 3′ sinh ra từ chính dữ liệu thật: với **2-1 Insert symbols**, `contains_text: "Microsoft®"` thực ra **đo đúng** — mục tiêu của objective là ký tự ® xuất hiện đúng chỗ. Khác hẳn ca Excel, nơi `contains_text: "Item 2"` được dùng thay cho việc đo thao tác Fill Series. Luật cứng sẽ bắt nhầm ca hợp lệ, nên tôi thêm đường miễn trừ có ghi lý do — buộc người soạn ra quyết định một lần và để lại dấu vết cho người duyệt.

---

## 8. Ưu tiên cập nhật

Chèn vào bản kế hoạch trước, không thay thế:

| # | Việc | Vì sao | Ước lượng |
|---|---|---|---|
| **A5** | Sửa 4 tiêu chí đo nhầm: `W32-D01`, `W33-R01`, `W61-A01`, `W62-L01` | 65 điểm đang chấm sai ở 4 rubric | 1 ngày |
| **A6** | Hiện rõ trần điểm thật của 1-3 và 1-4 cho học sinh, hoặc tạm rút hai bài khỏi chế độ thi | Học sinh làm đúng hết vẫn chỉ được 55 và 70 — sẽ khiếu nại | nửa ngày |
| **B5** | Chuyển `inspect_document`, `compatibility_check`, `save_alternate_format` sang `artifact` | Thu hồi 45 trong 93 điểm chết | 2 ngày |
| **C8** | Dịch 8 rubric đã ổn định (1-2 xong, còn 7) | Nội dung không đổi nữa | làm dần |
| **C9** | Sửa predicate 5 rubric còn lại, rồi mới dịch | Tránh làm hai lần | sau C2 |

**A5 và A6 nên làm trong tuần này.** A5 vì 65 điểm đang chấm sai. A6 vì nếu dạy Objective 1 mà không nói trước, học sinh làm đúng hết bài 1.3 vẫn chỉ thấy 55/100 — đó là cách nhanh nhất làm các em mất niềm tin vào hệ thống.

---

## 9. Đã kiểm chứng trên repo thật

Toàn bộ số liệu ở mục 2 và 3 đã được dựng lại **từ chính các file trong `app/rubrics/`**, không phải từ bảng chép tay:

- `scripts/audit_word.py` nay **đọc thẳng file rubric** thay vì giữ bảng cứng, nên chạy lại sau mỗi lần sửa là có số mới. Kết quả khớp đúng bảng mục 2: **101 tiêu chí, 2000 điểm, 113 điểm thao tác, 93 điểm chết, 170 điểm yếu**, và cả 20 file đều đúng tổng 100.
- Khẳng định "WordActionProbe chỉ sinh 4 loại sự kiện" đã đối chiếu với mã nguồn: `WordActionProbe.cs` gọi `ActionEvidence.Add` đúng 4 lần — `find`, `advanced_find`, `results_tab`, `find_navigate`. Tám loại còn lại **chỉ xuất hiện trong `WordActionDemo.cs`**, tức nguồn duy nhất sinh ra chúng là bộ demo, không phải học sinh làm bài.
- `tests/test_rubric_tool.py` chốt lại quan hệ này bằng hai test đọc thẳng mã C#: nếu ai thêm bộ ghi nhận mới bên desktop mà quên cập nhật `PRODUCIBLE_ACTIONS`, test gãy ngay.

**Hệ quả cho A1 (chặn `demo_all` ở chế độ thi):** hai việc này gắn chặt hơn tưởng. Vì `WordActionDemo.cs` là nguồn *duy nhất* sinh bằng chứng cho 8 loại thao tác, chặn demo ở chế độ thi sẽ biến 93 điểm từ "đỗ giả" thành **`unverified` thật**. Đó là kết quả đúng, nhưng nó làm **A6 thành việc gấp ngang A1** — không thể chặn demo mà không nói trước với học sinh về trần điểm.

### Cổng CI: bánh cóc thay vì chặn cứng

Chạy `check --strict` trên repo hiện tại cho **52 lỗi ở 30 rubric** — gắn thẳng vào CI thì chặn mọi PR ngay từ hôm nay. Nên `rubric_tool.py` có thêm lệnh `baseline`:

```
python3 scripts/rubric_tool.py baseline app/rubrics          # chốt 52 lỗi tồn đọng
python3 scripts/rubric_tool.py check app/rubrics --strict --baseline rubric-baseline.json
```

CI chỉ gãy khi có **lỗi MỚI**. Lỗi tồn đọng hiện dấu `·` thay vì `✗`, vẫn nhìn thấy để trả dần. Sửa xong thì chạy lại `baseline` để hạ mức — đã trả rồi thì không mắc lại được. Đây là cách gắn cổng chất lượng vào CI **ngay hôm nay** mà không phải chờ sửa hết 52 lỗi.

---

## 10. File kèm theo

| File | Nội dung |
|---|---|
| `app/rubrics/word-objective-1-1.json` | Bản song ngữ, 16 tiêu chí, EN 100% |
| `app/rubrics/word-objective-1-2.json` | Bản song ngữ, 7 tiêu chí, EN 100% — mẫu rubric thuần artifact |
| `scripts/rubric_tool.py` | Luật 7, 8, đường miễn trừ `weak_ok`, và lệnh `baseline` cho CI |
| `scripts/audit_word.py` | Dựng bảng mục 2 bằng cách đọc thẳng `app/rubrics/`; chạy trong CI |
| `scripts/build_word_rubric.py` | Script đã dựng bản song ngữ 1-1, dùng làm khuôn cho file tiếp theo |
| `rubric-baseline.json` | 52 lỗi tồn đọng đã chốt, để CI chỉ chặn lỗi mới |
| `tests/test_rubric_tool.py` | 24 test cho bộ luật, gồm 2 test đối chiếu thẳng mã C# |
| `tests/test_i18n.py` | 15 test cho lớp song ngữ, gồm test chấm `vi`/`en` ra cùng điểm |
