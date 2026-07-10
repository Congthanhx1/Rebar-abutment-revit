# AI CODING SKILLS & GUIDELINES (CHECKPOINT)

Tài liệu này đóng vai trò là "Kỹ năng cốt lõi" (Core Skills) cho AI khi tham gia phát triển, refactor hoặc sửa lỗi trong dự án này. Bất kỳ khi nào AI được yêu cầu sửa lỗi hoặc viết tính năng mới, AI **BẮT BUỘC** phải đọc và tuân thủ các quy tắc dưới đây.

## 1. Kiến trúc Giao diện và Revit API (WPF Modeless)
Dự án này sử dụng thư viện **Revit.Async** để giải quyết bài toán giao tiếp giữa cửa sổ WPF (Modeless) và Revit API. 
- **TUYỆT ĐỐI KHÔNG** sử dụng `IExternalEventHandler` thủ công hay `ExternalEvent.Create()`.
- **LUÔN LUÔN** bọc các lệnh gọi Revit API từ UI event (Button Click, ComboBox Selection...) bên trong `await Revit.Async.RevitTask.RunAsync()`.
- **File mẫu tham chiếu:** `CmdDrawMoCauRebar.cs`.

**Code chuẩn:**
```csharp
currentUI.OnDrawRebar = async (ui) =>
{
    await Revit.Async.RevitTask.RunAsync(app =>
    {
        // Code gọi Revit API ở đây
        Document doc = app.ActiveUIDocument.Document;
        // ...
    });
};
```

## 2. Thao tác với Element và Parameter (Fluent API)
Dự án này sử dụng bộ thư viện **Nice3point.Revit.Extensions**. Quá trình lấy dữ liệu, ép kiểu và đọc/ghi Parameter phải được viết theo phong cách C# hiện đại (Fluent).
- **TUYỆT ĐỐI KHÔNG** dùng `doc.GetElement(id) as T`.
- **TUYỆT ĐỐI KHÔNG** dùng `element.get_Parameter(BuiltInParameter)`.
- **LUÔN LUÔN** dùng `.ToElement<T>()` và `.FindParameter()`.
- **File mẫu tham chiếu:** `RebarNamingService.cs`, `CmdDrawMoCauRebar.cs`.

**Code chuẩn:**
```csharp
// Lấy Element từ ID:
Rebar rebar = rebarId.ToElement<Rebar>(doc);

// Đọc Parameter an toàn:
string comment = rebar.FindParameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();

// Ghi Parameter:
rebar.FindParameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK)?.Set("Tên Thép");
```

## 3. Xử lý Lỗi (Exception Handling) trong Giao dịch
- Khi thực hiện các thay đổi làm thay đổi model (`Transaction` / `TransactionGroup`), luôn phải dùng khối `try...catch`.
- Trong khối `catch`, luôn gọi `RollBack()` trước khi hiển thị cảnh báo để tránh treo Revit.

---
**LƯU Ý DÀNH CHO AI:** Nếu bạn đang chuẩn bị đề xuất một đoạn code sửa lỗi cho USER, hãy tự hỏi: *"Đoạn code của mình đã dùng `RevitTask` và `ToElement/FindParameter` chưa?"*. Nếu chưa, hãy viết lại trước khi gửi cho USER.
