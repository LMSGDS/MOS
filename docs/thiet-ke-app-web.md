# Đánh giá thiết kế app web mos.gds.edu.vn

**Ngày:** 21/09/2026
**Đã đọc:** `app/main.py`, `app/static/mos.css` (28,8 KB), `app/static/tokens.css` *(mới)*, `base.html`, `student_nav.html`, `progress.html`, `portal.html`, `word.html`, danh mục 24 template
**Chưa đọc:** `admin.py`, `bank.py`, `client_v1.py`, `progress.py`, `insights.py`, `pedagogy.py`, `lti.py`, `schema.sql`, `mos.js`, `kulkul.js`, 12 template `admin_*`

---

## 1. Hiện trạng: đây là cổng theo dõi, chưa phải môi trường học

24 template, trong đó **12 là `admin_*`**. Bề mặt học sinh chỉ có 3 màn — và cả ba nằm trong một file `progress.html` rẽ nhánh theo `student_nav`:

| Đường dẫn | Màn hình | Nội dung |
|---|---|---|
| `/tien-do` | Tiến độ của tôi | Radar 3 trục, thẻ Certiport, lộ trình thích ứng, ô nhập mã lớp |
| `/tien-do/nhiem-vu` | Nhiệm vụ cần làm | Danh sách bài giáo viên giao |
| `/tien-do/lich-su` | Lịch sử bài nộp | Bảng điểm + kỹ năng còn sai |

Luồng vào: `/` → nếu là học sinh thì **chuyển thẳng** sang `/tien-do`; nếu là giáo viên thì sang `/quan-tri`. Nghĩa là `portal.html` học sinh gần như không bao giờ thấy.

### Đối chiếu với kiến trúc LMS UDL 4 module

| Module trong thiết kế UDL | Trên web hiện nay |
|---|---|
| **Bản đồ Năng lực** | Có một phần — radar 3 trục Word/Excel/PowerPoint |
| **Không gian Khám phá** | **Không có gì cả** |
| **Trạm Thực hành** | Đẩy hết sang app desktop |
| **Trạm Chẩn đoán** | Không có trên web |

**Đây là khoảng trống thiết kế lớn nhất.** Học sinh vào web chỉ để *xem mình được bao nhiêu điểm* — không có chỗ nào để **học**. Toàn bộ nội dung hướng dẫn (`help_steps` trong rubric, thứ duy nhất mang tính dạy học trong hệ thống) chỉ sống trong dock của app desktop, và ở **chế độ Thi thì bị ẩn đi**.

Hệ quả thực tế cho lớp 11: em nào làm sai, đọc phản hồi "Bookmark SalesManager phải phủ đúng chữ Lola Jacobsen", rồi… không có đường nào để học lại cách làm. Vòng lặp UDL đứt ở đúng khúc quan trọng nhất.

### Radar 3 trục quá thô để dẫn đường

`progress.html` vẽ một tam giác Word/Excel/PowerPoint. Phép toán SVG đúng, nhưng ba trục cho ba **môn** không nói được em yếu ở **kỹ năng** nào. Thiết kế UDL gọi Bản đồ Năng lực là đồ thị ở mức objective/KC — tức mịn hơn cái này khoảng 30 lần. Radar hiện tại là biểu đồ để ngắm, không phải để điều hướng.

---

## 2. Màu đỏ đang dùng cho hướng dẫn học tập

Trong `progress.html` và `portal.html`, mục **"Lộ trình thích ứng"** — tức gợi ý em nên ôn phần nào — được render bằng:

```html
<p class="alert heat-red"><strong>{{ c.title }}</strong> — {{ c.reason }}</p>
```

`.alert` là `#fee2e2` / `#991b1b`, và `.heat-red` cũng `#fee2e2`. Đây chính là lớp dùng cho **thông báo lỗi** (`Mã lớp không đúng`).

Nguyên tắc UDL về rào cản cảm xúc nói ngược lại: gợi ý ôn tập **không phải là lỗi**. Chính hệ thống đã làm rất đúng ở chỗ khác — `unverified` không kết luận học sinh sai, màn hình tránh xếp hạng lớp, microcopy "So sánh với chính bạn". Rồi lại tô đỏ đúng cái phần đáng ra phải khích lệ.

Tôi đã thêm lớp `.coach` trong `tokens.css` — tông ấm, viền trái, không phải đỏ:

```html
<div class="coach">
  <p class="coach__what"><strong>{{ c.title }}</strong></p>
  <p class="coach__why">{{ c.reason }}</p>
</div>
```

---

## 3. Hệ thống thiết kế: ba bảng màu đang chạy song song

`mos.css` khai báo 7 biến ở `:root`, nhưng phần thân dùng **hơn 40 mã hex viết cứng**. Nghiêm trọng hơn, có **ba sắc xanh khác nhau cùng đóng vai "màu chính"**:

| Hệ | Màu chính | Dùng ở |
|---|---|---|
| gds | `#008EE2` | thẻ, nút primary, thanh tiến độ, nav quản trị |
| Canvas (`ic-brand`) | `#0374b5` | nav dọc, tab quản trị, nav học sinh, focus |
| login | `#0b2545` | nút đăng nhập |

Cộng thêm `--nav-bg: #394b58` cho nền nav và `#1e4f73` cho dock. Đó là lý do giao diện trông "lệch" khi đi từ trang đăng nhập sang trang tiến độ sang trang quản trị — không phải do thiếu chăm chút, mà do không có một nguồn màu duy nhất.

`tokens.css` gom lại một bộ, **giữ nguyên giá trị đang dùng** nên nạp vào không đổi diện mạo ngay, kèm bảng tra để thay dần hex cứng bằng `var()`. Trong đó có sẵn:

- **Nền tối** (`prefers-color-scheme` + `data-theme="dark"`) — hiện chưa có.
- **Tương phản cao** (`data-theme` độc lập với `data-contrast="high"`) — UDL yêu cầu học sinh tự bật được.
- Tông `--coach-*` riêng cho hướng dẫn học tập.

---

## 4. Ba lỗi tiếp cận cần sửa

UDL không phải là lời hứa suông trong tài liệu — nó phải đo được trên trang. Ba chỗ đang hỏng:

**4.1 — Nav xóa viền focus.** `mos.css`:

```css
.ic-app-header__menu-list-item:hover,
.ic-app-header__menu-list-item:focus-visible {
  background: rgba(255, 255, 255, 0.08);
  outline: 0;
}
```

Bỏ `outline` và thay bằng nền trắng 8% trên nền `#394b58` — gần như không nhìn thấy. Học sinh dùng bàn phím (và đây đúng là nhóm UDL nhắm tới) sẽ lạc. Vi phạm WCAG 2.4.7.

**4.2 — Chữ phụ trên nav dưới ngưỡng tương phản.** `.ic-app-header__menu-list-item-sub { opacity: 0.7 }` — chữ trắng 70% trên `#394b58` cho tỉ lệ khoảng **3,5:1**, ở cỡ chữ 10px. Ngưỡng AA là 4,5:1.

**4.3 — Không có `prefers-reduced-motion`.** Hiệu ứng `live-flash` nháy nền dòng vừa nộp bài chạy bất kể thiết lập hệ thống.

Cả ba đã được sửa trong `tokens.css` (nạp sau nên đè được, không phải sửa `mos.css`).

Ngoài ra, hai chỗ nên bổ sung khi có dịp: bảng `.grid` chưa có `<caption>`, và radar SVG có `aria-label` nhưng số liệu chỉ nằm trong `<text>` — nên thêm một bảng ẩn `.visually-hidden` (lớp này đã có sẵn trong `mos.css`) để trình đọc màn hình đọc được.

---

## 5. Sẵn sàng song ngữ: hiện bằng 0

Lớp song ngữ tôi dựng hôm nay mới nằm ở tầng rubric. Tầng web chưa có gì:

| Hạng mục | Trạng thái |
|---|---|
| `<html lang="vi">` | **ghi cứng** trong `base.html` |
| Nút đổi ngôn ngữ | không có ở bất kỳ nav nào |
| `lang` trong `_ctx()` | không có |
| Chuỗi giao diện | viết thẳng trong template, chưa tách |
| Đường dẫn | slug tiếng Việt (`/tien-do`, `/dang-nhap`) |

Về đường dẫn: **giữ nguyên slug tiếng Việt.** Chúng là định danh, không phải văn bản hiển thị; đổi sẽ vỡ link đã phát cho học sinh và vỡ `MOS_ASSET_V` cache. Ngôn ngữ giao diện nên độc lập với URL.

Tôi đã viết `app/web_lang.py` — nối vào bằng **ba dòng**, không đụng route nào:

```python
from app.web_lang import install as install_lang, lang_ctx
install_lang(TEMPLATES)

def _ctx(request, extra=None):
    data = { ..., **lang_ctx(request) }
```

Rồi `base.html` đổi một chỗ: `<html lang="{{ lang }}">`.

Thứ tự ưu tiên chọn ngôn ngữ, đã kiểm thử:

```
?lang=en / ?ngon-ngu=EN-us  →  session đã lưu  →  cookie  →  Accept-Language  →  vi
```

Kèm ba bộ lọc Jinja để template đọc thẳng khối song ngữ của rubric:

```jinja
<h2>{{ criterion.prompt | t(lang) }}</h2>
<ol>{% for s in criterion.help_steps | tlist(lang) %}<li>{{ s }}</li>{% endfor %}</ol>
```

Chạy thử trên `word-objective-1-1.json` thật:

```
[vi] Trong phần Contact Us, chọn tên Lola Jacobsen và chèn một bookmark tên SalesManager.
     || Tab **Insert**, nhóm **Links**, bấm **Bookmark**.
[en] In the Contact Us section, select the name Lola Jacobsen and insert a bookmark named SalesManager.
     || On the **Insert** tab, in the **Links** group, click **Bookmark**.
```

Nút chuyển ngôn ngữ (lớp `.lang-switch` đã có trong `tokens.css`) — thêm vào `student_nav.html`:

```jinja
<div class="lang-switch">
  {% for code in langs %}
  <a href="{{ lang_urls[code] }}" aria-current="{{ 'true' if code == lang else 'false' }}"
     lang="{{ code }}">{{ 'VI' if code == 'vi' else 'EN' }}</a>
  {% endfor %}
</div>
```

---

## 6. Ba vấn đề kỹ thuật

### 6.1 `word.html` có vẻ tự lồng iframe vào chính nó — **cần kiểm chứng**

Route `/khung/word` trả về `word.html`:

```python
@app.get("/khung/word", response_class=HTMLResponse)
def office_frame(request):  return TEMPLATES.TemplateResponse(request, "word.html", _ctx(request))
```

Mà `word.html` lại chứa:

```html
<iframe id="word-frame" src="/khung/word"></iframe>
```

Tức `/khung/word` → `word.html` → iframe nạp `/khung/word` → `word.html` → … Và `X-Frame-Options: SAMEORIGIN` cho phép cùng gốc nên không có gì chặn lại; trình duyệt chỉ dừng khi chạm giới hạn lồng nhau.

Không template nào khác render `word.html`, nên tôi không tìm ra đường đi "đúng". Nhưng tôi **chưa chạy được** app nên để ở mức cần kiểm chứng: mở `/khung/word` trên máy chạy thật và xem DevTools có nhiều frame lồng nhau không. Nếu đúng thì `word.html` nên tách làm hai — một trang khung, một trang nội dung (`word_frame.html`).

### 6.2 Cookie phiên mặc định không có cờ `Secure`

```python
https_only=os.environ.get("MOS_HTTPS_ONLY", "0") == "1"
```

Mặc định **tắt**. Trên máy chủ chạy sau nginx + Cloudflare, nếu quên đặt `MOS_HTTPS_ONLY=1` thì cookie phiên đi không có cờ `Secure`. Nên đảo mặc định thành `"1"` và chỉ tắt khi chạy máy cá nhân.

### 6.3 CSP chỉ có `frame-ancestors`

```python
response.headers["Content-Security-Policy"] = "frame-ancestors 'self'"
```

Thiếu `default-src`, `script-src`, `style-src`. Trang quản trị hiển thị dữ liệu người dùng nhập (tên học sinh, tên lớp, nội dung comment từ file .docx). Jinja tự escape nên rủi ro thấp, nhưng một CSP đầy đủ là lớp phòng thủ thứ hai gần như miễn phí:

```
default-src 'self'; img-src 'self' data:; style-src 'self' https://fonts.googleapis.com;
font-src 'self' https://fonts.gstatic.com; script-src 'self'; frame-ancestors 'self'
```

**Thêm một chi tiết nhỏ:** `base.html` tải font từ Google với `family=Calibri`. Calibri **không có trên Google Fonts** — yêu cầu đó luôn hỏng. Calibri vẫn hiện đúng vì máy có Office, nhưng nên bỏ khỏi URL cho sạch.

---

## 7. Đề xuất thiết kế: màn hình còn thiếu

Nếu chỉ làm thêm **một** màn hình cho học sinh, làm cái này:

### 7.1 `/hoc/<objective>` — Không gian Khám phá

Nguồn nội dung đã có sẵn, chưa dùng: **`help_steps` của 101 tiêu chí**, nay đã song ngữ. Chỉ cần đưa chúng lên web ngoài chế độ thi.

Bố cục ba phần, giữ đúng tinh thần Warm Minimalist:

- **Trái (30%)** — cây objective 1.1 → 6.2, node tô theo mức làm chủ, cùng thang màu với Bản đồ Năng lực.
- **Phải (70%)** — với mỗi tiêu chí: đề bài, ba bước `help_steps`, và **ảnh GIF thao tác 20 giây** nếu có. Nút `VI / EN` ở góc.
- **Không có điểm số trên màn này.** Đây là chỗ để học, không phải để đo.

Chi phí thấp một cách bất ngờ: dữ liệu có rồi, lớp song ngữ có rồi, chỉ thiếu một route và một template.

### 7.2 Nâng radar thành Bản đồ Năng lực thật

Thay tam giác 3 trục bằng lưới 6 nhóm objective × các objective con, mỗi ô một node. Ba trạng thái theo đúng thiết kế UDL: xám (chưa khám phá) / vàng đất (đang học) / xanh rêu (đã làm chủ). Bấm vào node thì mở `/hoc/<objective>`.

Giữ radar 3 môn ở trang quản trị — ở đó nó hợp lý, vì giáo viên cần cái nhìn tổng.

### 7.3 Hiện trần điểm thật của mỗi bài

Nối thẳng với phát hiện ở bản kiểm kê: Word 1.3 chỉ chấm được tối đa 55/100, Word 1.4 được 70/100. Thẻ nhiệm vụ nên nói rõ:

> **Word 1.3 — Lưu và chia sẻ tài liệu**
> Chấm tự động: 55/100 điểm. 45 điểm còn lại cô/thầy chấm tay.

Ba dòng template, nhưng nó chặn đứng câu hỏi "em làm đúng hết sao chỉ được 55?" — câu hỏi sẽ xuất hiện ngay buổi dạy đầu tiên.

---

## 8. Việc theo thứ tự

| # | Việc | Ước lượng | Ghi chú |
|---|---|---|---|
| **W1** | Nạp `tokens.css` trước `mos.css` | 15 phút | Sửa luôn 3 lỗi tiếp cận, không đổi diện mạo |
| **W2** | Đổi `.alert.heat-red` → `.coach` ở "Lộ trình thích ứng" | 15 phút | 2 template |
| **W3** | `MOS_HTTPS_ONLY` mặc định `"1"` | 5 phút | |
| **W4** | Nối `web_lang.py` + `<html lang="{{ lang }}">` + nút VI/EN | nửa ngày | Ba dòng vào `main.py` |
| **W5** | Hiện trần điểm thật trên thẻ nhiệm vụ | nửa ngày | Chặn khiếu nại buổi đầu |
| **W6** | Kiểm chứng và sửa iframe `/khung/word` | nửa ngày | Xem 6.1 |
| **W7** | CSP đầy đủ + bỏ `Calibri` khỏi URL Google Fonts | 1 giờ | |
| **W8** | Màn `/hoc/<objective>` — Không gian Khám phá | 1 tuần | **Việc đáng giá nhất** |
| **W9** | Bản đồ Năng lực mức objective thay radar 3 trục | 1 tuần | Sau W8 |
| **W10** | Thay dần hex cứng trong `mos.css` bằng `var()` | làm dần | Theo bảng tra trong `tokens.css` |

**W1–W3 cộng lại chưa tới một giờ** và sửa được ba lỗi tiếp cận cộng một lỗ hổng cookie. Làm trước.

**W8 là việc đáng giá nhất trong danh sách.** Không có nó thì hệ thống đo học sinh mà không dạy học sinh — và đó là điều ngược hẳn với chữ "học tập" trong tên dự án.

---

## 9. File kèm theo

| File | Nội dung |
|---|---|
| `app/static/tokens.css` | Bộ token gom 3 hệ màu, nền tối, tương phản cao, sửa 3 lỗi tiếp cận, lớp `.coach` và `.lang-switch` |
| `app/web_lang.py` | Chọn ngôn ngữ cho web + 3 bộ lọc Jinja (`t`, `tlist`, `tmap`); nối vào bằng 3 dòng |

---

## 10. Trạng thái triển khai (21/09/2026)

| # | Việc | Trạng thái | Ghi chú |
|---|---|---|---|
| W1 | `tokens.css` | **Xong** | Nạp sau `mos.css` (để đè luật cũ). Sửa focus nav, tương phản chữ phụ, `prefers-reduced-motion`; có nền tối và `data-contrast="high"` |
| W2 | `.coach` cho Lộ trình thích ứng | **Xong** | `progress.html`, `portal.html` |
| W3 | `MOS_HTTPS_ONLY` mặc định `1` | **Xong** | Tắt bằng `MOS_HTTPS_ONLY=0`; `scripts/run-mos-web.sh` và `tests/conftest.py` tự tắt cho máy cá nhân |
| W4 | `web_lang.py` + `<html lang>` + nút VI/EN | **Xong** | Thứ tự `?lang` → session → cookie `mos_lang` → Accept-Language → vi. Bộ lọc `t`, `tlist`, `tmap`, `bold` |
| W5 | Trần điểm thật trên thẻ nhiệm vụ | **Xong** | `app/ceiling.py` — cùng tập `PRODUCIBLE_ACTIONS` với `rubric_tool` (luật 7) |
| W6 | iframe `/khung/word` | **Không có lỗi** | `word.html` không chứa iframe; `kulkul.html` mới nhúng `/khung/office`. Không tự lồng |
| W7 | CSP đầy đủ + bỏ Calibri | **Xong một phần** | `script-src` còn `'unsafe-inline'` vì `login.html`, `install.html` và 6 template `admin_*` có `<script>` nội tuyến / `onclick=`. Gỡ dần rồi siết về nonce |
| W8 | `/hoc/<project_id>` — Không gian Khám phá | **Xong** | Cây objective trái, đề bài + `help_steps` phải, VI/EN, không hiện điểm. Ảnh/GIF thao tác đặt ở `app/static/hoc/<project_id>/<criterion_id>.gif` là tự hiện |
| W9 | Bản đồ Năng lực mức objective | **Xong (Word)** | Lưới 6 nhóm × objective trên `/tien-do` và `/hoc`; ba trạng thái xám / vàng đất / xanh rêu (`mastered` hoặc ≥ 70 điểm). Radar 3 môn giữ ở trang quản trị |
| W10 | Thay hex cứng trong `mos.css` | Làm dần | Bảng tra ở đầu `tokens.css` |

Về **B5** trong bản kiểm kê rubric: đã thêm facts `personal_info_removed`, `compatibility_mode` (`word_xml.py`) và predicate cùng tên (`grade.py`), **nhưng không đổi** `W14-I01`/`W14-X01` sang `artifact` — tệp đề `Word_1-4.docx` của Study Guide đã sẵn `removePersonalInformation` và `compatibilityMode=15`, nên đổi predicate là cho điểm miễn phí. Thay vào đó trần 70/100 được nói rõ trên thẻ nhiệm vụ và trang `/hoc` (A6). Dùng hai predicate mới khi soạn đề với tệp gốc chưa qua Inspect.
