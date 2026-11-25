# Đo tác động của từng bản sửa graph trong PACE

Bộ benchmark công khai và dữ liệu B0 đã lưu nằm trong [benchmarks/README.md](../benchmarks/README.md). Bộ C# trong tài liệu này dùng làm regression bổ sung.

Giữ một bộ đầu vào và đáp án chuẩn cố định cho toàn bộ thí nghiệm. Mỗi lần chạy lưu commit của PACE, commit của từng repo đầu vào, phiên bản SDK/analyzer, cấu hình phân tích và kết quả theo từng ca. Lưu kết quả đầu ra gốc để có thể chấm lại theo cùng rubric.

## Baseline C# ngoại tuyến

`PACE.Tests/GraphBenchmarkTests.cs` gọi parser Roslyn, resolver, matcher và builder hiện tại, không cần DB hay LLM. Test in sai lệch thay vì fail khi graph sai, để bản baseline vẫn xuất kết quả. Lỗi dựng fixture hoặc lỗi parser sẽ làm test fail.

```bash
dotnet test PACE.Tests/PACE.Tests.csproj -m:1 -p:UseSharedCompilation=false \
  --filter FullyQualifiedName~GraphBenchmarkTests --logger 'console;verbosity=detailed'
```

Oracle do người viết fixture xác định từ code. Bản hiện tại chấm cạnh CALL dưới dạng đa tập caller → callee, nên hai lời gọi giống nhau vẫn được đếm hai lần. Đây là mức đo chung mà baseline cung cấp được. Khi bản sửa xuất vị trí call site, bổ sung một chỉ số riêng theo file, span và target. Giữ phép đo đa tập để so cùng thước với baseline. Ca route dùng attribute giả lập để kiểm tra matcher, chưa kiểm tra ASP.NET Core runtime.

Kết quả B0 trên mã phân tích commit `22351f5b3cc0fdc6d17602dfbb35063d9b73b76f`, SDK .NET `9.0.124`, 8 fixture C#:

| Ca | TP | FP | FN | Kết quả đáng chú ý |
|---|---:|---:|---:|---|
| Lời gọi thường | 1 | 0 | 0 | Đúng |
| Lời gọi sau đổi tên hợp lệ | 1 | 0 | 0 | Đúng |
| Overload | 1 | 1 | 0 | Nối thêm overload kiểu `string` |
| Trùng tên khác namespace | 1 | 1 | 0 | Nối thêm method của `B.Store` |
| Hai lời gọi giống nhau | 1 | 0 | 1 | Mất một lần gọi |
| Đệ quy trực tiếp | 0 | 0 | 1 | Mất cạnh tự gọi |
| Hàm tính toán ngắn có tên giống accessor | 0 | 0 | 1 | Mất lời gọi `getDiscount()` |
| Route mâu thuẫn với tên | — | — | — | Chọn sai endpoint, 0/1 |
| **Tổng cạnh CALL** | **5** | **2** | **3** | **Precision 71,4%, recall 62,5%, F1 66,7%** |

Đây là baseline cho các lỗi mục tiêu trên fixture nhỏ. Tỷ lệ trên repo thực tế và chất lượng câu hỏi sẽ được đo bằng các nhóm dữ liệu bên dưới.

## Các phiên bản chạy trên cùng dữ liệu

| Phiên bản | Thay đổi so với bản trước | Chỉ số cần nhìn đầu tiên |
|---|---|---|
| B0 | PACE hiện tại | Ghi đủ tất cả chỉ số có thể đo |
| B1 | Định danh symbol và resolve overload | Cạnh dư, F1 cạnh CALL |
| B2 | Khớp endpoint theo metadata, xử lý mơ hồ | Tỷ lệ đúng endpoint và tỷ lệ bỏ ngỏ |
| B3 | Giữ từng call site, đệ quy và vị trí bằng chứng, lưu trạng thái bị cắt | Cạnh bị mất, tỷ lệ gộp nhầm, nhận diện đầu ra thiếu |
| B4 | Thêm CFG và def-use cho một stack | Độ đúng điều kiện, nhánh và quan hệ truyền giá trị |
| B5 | Dùng bằng chứng để sinh đáp án trước câu hỏi | Tỷ lệ câu hỏi sai tiền đề và số câu hợp lệ |
| Final | Cấu hình dự kiến sử dụng | So với B0 trên tập giữ lại và chạy lại regression |

Chạy toàn bộ bộ đo sau mỗi bước. Bn trừ B(n−1) cho biết thay đổi khi thêm bước đó vào chuỗi hiện tại. Nếu muốn tách đóng góp của hai thay đổi có thể bật độc lập, chạy thêm Final bỏ từng thay đổi, ví dụ Final bỏ CFG hoặc bỏ kiểm tra bằng chứng.

## Dữ liệu đánh giá và cách kết luận

- **Microbenchmark:** bổ sung ca route mơ hồ, nhánh lồng, early return, DI và cắt graph trước khi sửa các lớp liên quan. Mỗi ca ghi endpoint đúng, cạnh cần có, cạnh sai đã biết và điều kiện kỳ vọng. Giữ ca đổi tên cùng nhóm với bản gốc khi chia tập.
- **Repo thực tế:** dùng mock project có sẵn để thử luồng, rồi chốt thêm các repo đại diện cho stack muốn công bố. Khoanh vùng endpoint và call site để hai người có thể chấm tay. Để riêng một số repo chỉ chạy khi chốt phương pháp. Cạnh chưa xác minh ghi là chưa xác minh. Trace chỉ chứng minh cạnh đã chạy dưới cấu hình và input quan sát được.
- **Graph:** precision = TP/(TP+FP), recall = TP/(TP+FN), F1 = 2TP/(2TP+FP+FN). Báo thêm số TP/FP/FN, endpoint đúng/tổng endpoint, số call chưa resolve, số repo phân tích thành công và thời gian. Báo N/A khi thiếu dữ liệu hoặc mẫu số bằng 0. Tách kết quả theo stack và theo nhóm ca.
- **Câu hỏi:** cố định model, cấu hình, số câu yêu cầu và bộ nghiệp vụ. Chạy lặp cùng số lần cho mỗi bản và lưu prompt/response. Cho người chấm xem code và yêu cầu nghiệp vụ, ẩn phiên bản sinh câu. Ghi ba kết quả theo từng lượt để tránh bỏ câu khó rồi báo chất lượng cao: hợp lệ, sai tiền đề, không sinh/đưa duyệt. Báo số câu hợp lệ trên cùng ngân sách số câu yêu cầu, và tỷ lệ sai trong các câu đã sinh.
- **Cách trình bày:** báo chênh lệch Bn so với B(n−1) và Final so với B0, theo điểm phần trăm và số lượng tuyệt đối. Với đủ repo độc lập, có thể báo khoảng tin cậy bằng bootstrap ghép cặp, lấy mẫu theo repo. Với một số ít repo, báo bảng theo từng repo và giới hạn kết luận trong tập đã đo.

Tham khảo cách thiết kế ca call site trong [Judge](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf) và cách xây câu hỏi có đáp án bám mã nguồn trong [Code-QA-Bench](https://arxiv.org/abs/2605.29277).
