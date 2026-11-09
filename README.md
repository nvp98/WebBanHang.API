# WebBanHang.Api

ASP.NET Core 9 Web API cho storefront Next.js (`../frontend`) và trang quản trị, chạy **trực tiếp trên CSDL có sẵn** `vienvienco_VVNET` (script `../SQL/bk phuoc_4462.sql`). Không dùng EF migrations — schema thuộc về script SQL.

## Chạy local

```bash
# 1. Import CSDL vào LocalDB (file SQL là UTF-16, sqlcmd đọc được trực tiếp)
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "CREATE DATABASE vienvienco_VVNET"
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d vienvienco_VVNET -i "..\SQL\bk phuoc_4462.sql"

# 2. Nâng cấp tối thiểu (mở rộng cột Password, thêm index) — idempotent
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d vienvienco_VVNET -i Database\upgrade.sql

# 3. Tạo tài khoản quản trị (mật khẩu được hỏi nếu bỏ trống)
cd WebBanHang.Api
dotnet run -- create-admin <tên-đăng-nhập>

# 4. Chạy API → http://localhost:5002/swagger
dotnet run
```

### Sửa ảnh sản phẩm không hiển thị

Bản backup cũ hơn site đang chạy: ảnh đã được tải lại với đuôi ngẫu nhiên khác (`…_0c5.jpg` trong CSDL, `…_cb9.jpg` trên web) nên link 404. Lệnh sau dò lại đúng tên file trên site cũ (og:image của trang sản phẩm, rồi các trang danh mục kể cả phân trang, ghép theo tên hoặc mã sản phẩm):

```bash
dotnet run -- sync-images                     # chỉ xem trước, không ghi
dotnet run -- sync-images --apply --download  # cập nhật CSDL + tải ảnh về wwwroot/Photos
```

`--apply` lưu tên cũ vào `Database/image-sync-backup-*.csv` để hoàn tác. Ảnh đã tải về được API ưu tiên phục vụ tại `{PublicBaseUrl}/Photos/...`, không còn phụ thuộc site cũ. Sản phẩm không còn trên site cũ thì tải ảnh lên lại trong trang quản trị.

Cấu hình trong `appsettings.json`:

| Khoá | Ý nghĩa |
|---|---|
| `ConnectionStrings:Default` | Chuỗi kết nối SQL Server. Không commit mật khẩu thật — dùng `dotnet user-secrets` hoặc biến môi trường `ConnectionStrings__Default`. |
| `Jwt:Key` | Khoá ký JWT ≥ 32 ký tự. Bắt buộc ngoài Development (biến môi trường `Jwt__Key`). Ở Development nếu bỏ trống sẽ dùng khoá tạm, token mất hiệu lực khi restart. |
| `Media:LegacyBaseUrl` | Nơi chứa ảnh cũ (`https://vienvienco.com/Photos`). Tên file trong cột `Images/BigImages` được ghép thành `{LegacyBaseUrl}/{Folders[kind]}/{file}`. |
| `Auth:UpgradeLegacyPasswords` | Tự đổi hash MD5 cũ sang PBKDF2 sau khi đăng nhập thành công. **Đặt `false` nếu site ASP.NET cũ vẫn dùng chung CSDL**, nếu không tài khoản đã nâng cấp sẽ không đăng nhập được ở site cũ. |
| `Cors:Origins` | Origin của frontend. |

## Quy ước của schema cũ

- Mỗi bảng có `PKxxx` (identity, 1 dòng / 1 ngôn ngữ) và `IDxxx` (id logic, dùng chung cho bản `vi` và `en`). `LanguageID`: 0 = vi, 1 = en.
- `IsDeleted = 1` là xoá mềm. `Levels` là thứ tự sắp xếp (lớn hiển thị trước).
- `TitleURL` lưu slug dạng `/slug.html` (vi) hoặc `/en/slug.html` (en) — API giữ nguyên định dạng này.
- `Orders` + `ordersdetailed` được dùng làm **yêu cầu báo giá** (B2B). `ordersdetailed.CategoryID` = id của đơn. Mã hiển thị `BG000123`.
- Cột `Product.Brand` thực chất là khối HTML "cam kết" — thương hiệu nằm ở `ManufacturerID` → `ProductManufacturer`.

## Endpoint

**Storefront** (khớp `frontend/lib/api/*`): `GET /api/products` (lọc `categorySlug`, `brandSlug` nhiều giá trị cách bởi dấu phẩy, `minPrice`, `maxPrice`, `isNew`, `isFeatured`, `search`, `sortBy`, `page`, `pageSize`), `/products/{slug}`, `/products/{id}/related`, `/products/featured|new|best-sellers|search`, `/categories`, `/categories/{slug}`, `/brands`, `POST /contact`, `POST /quotes`, `/auth/login|register|refresh-token|logout|me`, `/orders` (đơn của khách đăng nhập, khớp theo email), `/news`, `/banners`, `/partners`, `/services`, `/site-config`. Thêm `?lang=en` để lấy bản tiếng Anh.

**Quản trị** (`/api/admin/*`, JWT của tài khoản trong `Logons`; nhóm Admin = `Groups.IDGroups 1`, còn lại là Nhân viên):

| Tài nguyên | Đường dẫn | Ghi chú |
|---|---|---|
| Sản phẩm | `products` | + `/{pk}/images` (bảng ImagesProduct), `/{pk}/standards` (ProductStandardISO) |
| Danh mục, thương hiệu, tiêu chuẩn | `product-categories`, `manufacturers`, `standards` | Không xoá được khi còn sản phẩm |
| Tin tức, dịch vụ, banner, đối tác, dự án, tài liệu | `news`, `news-categories`, `services`, `banners`, `partners`, `projects`, `project-categories`, `documents`, `document-categories` | |
| Cấu hình | `configs` | **Chỉ Admin** (bảng chứa mật khẩu SMTP) |
| Báo giá | `quotes` | Lọc theo trạng thái/ngày, nhập đơn giá (`PUT /{id}/items`), đổi trạng thái, xoá hàng loạt |
| Liên hệ | `contacts` | Đánh dấu đã xử lý, xoá hàng loạt (dọn spam) |
| Tài khoản quản trị / khách hàng | `users` (chỉ Admin), `customers` | |
| Tải tệp | `uploads?kind=product` | Lưu vào `wwwroot/uploads/{kind}/`, trả về `path` để gán vào trường ảnh |
| Tổng quan | `dashboard` | |

Các bảng CMS dùng chung một bộ CRUD: `GET ?q=&lang=vi|en|all&status=active|deleted|all&categoryId=&page=`, `GET /{pk}`, `GET /{pk}/translations`, `POST ?lang=en&translationOf={id}` (tạo bản dịch), `PUT /{pk}`, `DELETE /{pk}` (xoá mềm mọi ngôn ngữ), `POST /{pk}/restore`, `DELETE /{pk}/permanent` (chỉ Admin, chỉ với bản ghi trong thùng rác). Nội dung HTML được lọc XSS trước khi lưu.
