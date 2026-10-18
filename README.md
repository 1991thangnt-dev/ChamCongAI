# Chấm công AI - hệ thống chấm công và nghỉ phép có chatbot hỗ trợ

Đồ án 3. ASP.NET Core MVC + SQL Server LocalDB. Chatbot dùng mô hình ngôn ngữ qua API (chuẩn OpenAI-compatible).

## Chức năng nghiệp vụ
- Chấm công vào/ra hằng ngày, tự tính đi trễ (sau 08:05) và về sớm (trước 17:00).
- Đơn xin nghỉ (phép năm, ốm, thai sản, kết hôn, hiếu hỉ, không lương) kèm upload giấy tờ, luồng duyệt
  nhân viên → quản lý (hoặc nhân sự), tự kiểm tra trùng ngày và không cho vượt số phép còn lại.
- Số dư phép năm tự trừ khi đơn được duyệt. Tổng hợp chấm công theo tháng cho quản lý / nhân sự.
- Kho tài liệu chính sách (.txt, .md, .pdf) do nhân sự quản lý.

## Chatbot: pipeline xử lý mỗi câu hỏi
1. **Hiểu câu hỏi** (QuestionAnalyzer): phân loại intent, trích xuất thực thể (khoảng thời gian, tên nhân viên, loại nghỉ),
   **viết lại câu hỏi theo ngữ cảnh hội thoại**. Có LLM thì dùng LLM, lỗi hoặc không có thì **fallback sang bộ quy tắc từ khóa**.
2. **Định tuyến**: tra tài liệu (RAG), gọi hàm database, cả hai, trả lời mẫu hoặc từ chối nếu ngoài phạm vi.
3. **Kiểm soát quyền bằng code**: nhân viên chỉ xem dữ liệu của mình, quản lý xem nhân viên của mình, nhân sự xem tất cả.
4. **Truy xuất lai** (RetrievalService): BM25 + embedding, hợp nhất bằng Reciprocal Rank Fusion. Không có embedding thì dùng BM25.
5. **Truy vấn DB bằng hàm có sẵn** (ChatTools: PhepConLai, ThongKeChamCong, DanhSachDon). AI không đọc DB và không tự tính số liệu.
6. **Sinh câu trả lời có căn cứ**: chỉ dựa trên ngữ cảnh đã truy xuất, kèm trích nguồn, nói rõ khi thiếu thông tin.
   Không có AI thì dùng câu trả lời mẫu ghép từ dữ liệu.
7. **Ghi log** mọi bước (trang Nhật ký chat) và hiển thị "Các bước xử lý" ngay dưới mỗi câu trả lời trong khung chat.

Trang **Đánh giá AI** (tài khoản `ns`) chạy 26 câu hỏi mẫu và đo: độ chính xác intent, định tuyến (quy tắc so với LLM),
và tỷ lệ truy xuất đúng đoạn tài liệu (BM25 so với lai). Đây là số liệu để đưa vào báo cáo.

## Cách chạy
1. Cài **Visual Studio 2022** (workload *ASP.NET and web development*, đã gồm SQL Server LocalDB) hoặc .NET SDK.
2. Mở `ChamCongAI.csproj`. Nếu máy dùng .NET 9/10, sửa `TargetFramework` trong `.csproj`
   (và Version gói `Microsoft.EntityFrameworkCore.SqlServer` cho cùng số chính).
3. Chạy (F5 hoặc `dotnet run`). Lần đầu app tự tạo database `ChamCongAiDb`, tài khoản mẫu, dữ liệu chấm công 60 ngày
   và 4 tài liệu chính sách mẫu. **Chưa cần API key vẫn chạy được** (chế độ quy tắc + BM25 + câu trả lời mẫu).
4. Đăng nhập (mật khẩu chung `123456`): `nv1`, `nv2`, `nv3` (nhân viên), `ql` (quản lý), `ns` (nhân sự).

## Bật AI (miễn phí)
Mở `appsettings.json`, điền API key. **Không đưa file có key lên GitHub.**

- **Chat** (`Ai:Chat`): mặc định trỏ tới Groq (`https://api.groq.com/openai/v1`). Đăng ký key miễn phí tại console.groq.com,
  không cần thẻ. Nếu báo lỗi "model not found", mở danh sách model trong console của nhà cung cấp và sửa `Model`.
- **Embedding** (`Ai:Embedding`): mặc định trỏ tới Jina (`https://api.jina.ai/v1`, model `jina-embeddings-v3`), có key miễn phí.
  Sau khi điền key, vào trang **Tài liệu** → bấm *Tạo embedding*.
- Đổi nhà cung cấp chỉ cần sửa `BaseUrl`, `ApiKey`, `Model` vì mọi nhà cung cấp dưới đây đều theo chuẩn OpenAI-compatible:
  - Google Gemini: `https://generativelanguage.googleapis.com/v1beta/openai/` (một key dùng được cả chat và embedding)
  - OpenRouter: `https://openrouter.ai/api/v1` (chọn model có hậu tố `:free`)
  - Ollama chạy trên máy: `http://localhost:11434/v1` (key điền chữ bất kỳ)
- Hạn mức miễn phí của các nhà cung cấp thay đổi thường xuyên, hãy xem trang giới hạn của họ. Hệ thống tự thử lại khi bị giới hạn
  tốc độ (429) và tự chuyển sang chế độ không AI nếu gọi API lỗi.

## Lưu ý
- Tài liệu chính sách trong `SampleDocs/` là **tài liệu mẫu để demo**, có tham khảo Bộ luật Lao động 2019 nhưng chưa phải văn bản pháp lý.
  Khi viết báo cáo, hãy đối chiếu lại với quy định hiện hành (đặc biệt phần bảo hiểm xã hội) và với nội quy công ty giả định của bạn.
- Giấy tờ đính kèm đơn nghỉ lưu trong `App_Data/don`, chỉ chủ đơn, quản lý trực tiếp và nhân sự tải được.
- Muốn chạy lại từ đầu: xóa database `ChamCongAiDb` rồi chạy lại.
