/*
  Nâng cấp tối thiểu cho CSDL vienvienco_VVNET để chạy WebBanHang.Api.
  Chạy một lần, an toàn khi chạy lại (idempotent). Không xoá/đổi dữ liệu hiện có.

  1. Mở rộng cột Password (nvarchar(50) → nvarchar(255)) để lưu hash PBKDF2 thay cho MD5.
     Mật khẩu MD5 cũ vẫn đăng nhập được và được tự nâng cấp sau lần đăng nhập đầu tiên
     (tắt bằng Auth:UpgradeLegacyPasswords=false nếu site cũ còn dùng chung CSDL).
  2. Thêm index cho các truy vấn của storefront (lọc theo ngôn ngữ, danh mục, slug).
*/
SET NOCOUNT ON;

IF COL_LENGTH('dbo.Account', 'Password') < 510
    ALTER TABLE dbo.Account ALTER COLUMN [Password] nvarchar(255) NOT NULL;

IF COL_LENGTH('dbo.Logons', 'Password') < 510
    ALTER TABLE dbo.Logons ALTER COLUMN [Password] nvarchar(255) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Language_Category' AND object_id = OBJECT_ID('dbo.Product'))
    CREATE INDEX IX_Product_Language_Category ON dbo.Product (LanguageID, IsDeleted, CategoryID) INCLUDE (IDProduct, Levels);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_TitleURL' AND object_id = OBJECT_ID('dbo.Product'))
    CREATE INDEX IX_Product_TitleURL ON dbo.Product (TitleURL);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_IDProduct' AND object_id = OBJECT_ID('dbo.Product'))
    CREATE INDEX IX_Product_IDProduct ON dbo.Product (IDProduct);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_Email' AND object_id = OBJECT_ID('dbo.Orders'))
    CREATE INDEX IX_Orders_Email ON dbo.Orders (Email) INCLUDE (IDCategory, IsDeleted);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ordersdetailed_Order' AND object_id = OBJECT_ID('dbo.ordersdetailed'))
    CREATE INDEX IX_ordersdetailed_Order ON dbo.ordersdetailed (CategoryID);

PRINT 'upgrade.sql: done';
