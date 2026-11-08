using System.Text;

namespace PACE.Application.QuestionGeneration;

/// <summary>Câu hỏi mẫu của giảng viên (few-shot). PACE hiện chưa có bảng lưu, để sẵn cho sau này.</summary>
public record FewShotExample(string Question, string? SuggestedAnswer, string? Difficulty, string? Tag);

public record QuestionPrompt(string SystemInstruction, string UserPrompt);

/// <summary>
/// Prompt sinh câu hỏi — CHÉP NGUYÊN VĂN từ Repo_Into_Graph
/// (AIService.GenerateUnifiedQuestionsAsync), là prompt dùng chung cho cả ba arm
/// Traditional / Pure CFG / Hybrid trong bài báo 1. KHÔNG sửa nội dung prompt ở đây,
/// nếu không kết quả sẽ không còn so sánh được với bài báo.
/// </summary>
public static class QuestionPromptBuilder
{
    public static QuestionPrompt Build(
        string businessName,
        string codeBuilder,
        string contextBuilder,
        int numberOfQuestions,
        string difficulty,
        string? additionalContext = null,
        IEnumerable<FewShotExample>? fewShotExamples = null,
        string? groundTruthSpecification = null)
    {
        string difficultyInstruction = difficulty.ToLower() switch
        {
            "dễ" => "- Mức độ DỄ (Happy Path): Câu hỏi CHỈ tập trung vào luồng xử lý thành công, đi thẳng từ đầu đến cuối mà KHÔNG kích hoạt bất kỳ nhánh rẽ ngoại lệ (if/else) nào, KHÔNG hỏi về lỗi.",
            "trung bình" => "- Mức độ TRUNG BÌNH (Single Exception): Câu hỏi BẮT BUỘC phải hỏi về tình huống kích hoạt ĐÚNG MỘT (1) nhánh rẽ ngoại lệ (kiểm tra điều kiện, validation lỗi...).",
            "khó" => "- Mức độ KHÓ (Double Exception): Câu hỏi BẮT BUỘC phải tạo ra tình huống phức tạp kích hoạt ĐỒNG THỜI TỪ HAI (2) nhánh rẽ ngoại lệ trở lên (xung đột dữ liệu, thứ tự ưu tiên, giao dịch chéo).",
            _ => $"- Mức độ: {difficulty}."
        };

        var groundTruthInstruction = string.IsNullOrWhiteSpace(groundTruthSpecification)
            ? string.Empty
            : @"
ĐIỂM NEO TRI THỨC NGHIỆP VỤ CHUẨN (GROUND TRUTH SPECIFICATION):
Bạn được cung cấp một Sơ đồ quy trình chuẩn đã được Giảng viên và Sinh viên thống nhất (từ Draw.io). Đây là CHÂN LÝ NGHIỆP VỤ để bạn đối chiếu:
1. ĐỐI CHIẾU THỨ TỰ THỰC THI (SEQUENCE CONFORMANCE): So sánh thứ tự các bước trong Quy trình chuẩn với luồng thực tế trong Mã nguồn (Source Code) và Mermaid Graph.
2. PHÁT HIỆN LỖ HỔNG / NGHỊCH ĐẢO LUỒNG:
   - Nếu sinh viên thực hiện SAI THỨ TỰ (Ví dụ: Thanh toán trước khi Gọi món, hoặc Cập nhật trạng thái trước khi Kiểm tra điều kiện): Bạn BẮT BUỘC phải ưu tiên đặt câu hỏi tình huống chất vấn trực tiếp vào lỗi ngược luồng này, yêu cầu sinh viên giải thích hậu quả khi chạy ngược quy trình.
   - Nếu sinh viên THIẾU BƯỚC BẮT BUỘC (Ví dụ: Quy trình chuẩn có bước kiểm tra số dư/tồn kho nhưng code nhảy cóc): Hãy đặt câu hỏi vào rủi ro khi thiếu bước kiểm tra đó.
3. QUY ĐỊNH TARGETED ENTRY POINTS KHI CODE SAI/THIẾU:
   - Trỏ mảng targetedEntryPoints vào các hàm/action trong code đang thực thi sai thứ tự đó (hoặc hàm gần nhất chịu trách nhiệm về luồng bị thiếu).
";

        var antiHallucinationInstruction = string.IsNullOrWhiteSpace(groundTruthSpecification)
            ? @"Bạn TUYỆT ĐỐI KHÔNG ĐƯỢC tự phát minh hoặc suy diễn ra các tính năng, quy trình, rủi ro (như thanh toán, giao dịch chéo...) nếu nó không ĐƯỢC THỂ HIỆN RÕ RÀNG trong Mermaid Graph hoặc Source Code.
Nếu Mermaid Graph hoặc Source Code chỉ là một luồng đơn giản (không có if/else phức tạp), bạn BẮT BUỘC phải đặt câu hỏi đơn giản tương ứng, KHÔNG ĐƯỢC cố tình tạo tình huống phức tạp nằm ngoài phạm vi tài liệu được cung cấp. Câu hỏi phải chỉ đích danh được dựa vào bước nào trong sơ đồ hoặc dòng code nào."
            : @"Bạn căn cứ vào Quy trình chuẩn (Ground Truth) để xác định các quy tắc nghiệp vụ hợp lệ. TUYỆT ĐỐI KHÔNG tự phát minh ra các nghiệp vụ nằm ngoài cả Quy trình chuẩn lẫn Source Code.
Nếu phát hiện sự sai lệch giữa Quy trình chuẩn và Source Code, hãy chất vấn dựa trên điểm sai lệch đó.";

        var systemInstruction = $@"Bạn là một Giảng viên đại học chấm thi vấn đáp đồ án phần mềm. Nhiệm vụ tối cao của bạn là phân tích Mã nguồn (Source Code) và Sơ đồ luồng (Mermaid Graph) được cung cấp để bóc tách ra các Quy tắc nghiệp vụ (Business Rules) cốt lõi của dự án, từ đó đặt câu hỏi tình huống để kiểm tra xem sinh viên có thực sự hiểu luồng đi của nghiệp vụ trên thực tế hay không.
{groundTruthInstruction}
YÊU CẦU BẮT BUỘC VỀ SỐ LƯỢNG VÀ ĐỘ KHÓ:
- Bạn PHẢI tạo ra chính xác ĐÚNG {numberOfQuestions} câu hỏi. Không được tạo nhiều hơn hoặc ít hơn.
- Tất cả các câu hỏi phải được thiết kế tuân thủ nghiêm ngặt quy định độ khó sau:
{difficultyInstruction}

THIẾT QUÂN LUẬT VỀ NGÔN NGỮ (100% BUSINESS LANGUAGE):
1. Cả câu hỏi (question) và câu trả lời (suggestedAnswer) TUYỆT ĐỐI KHÔNG CHỨA bất kỳ từ khóa kỹ thuật hay cấu trúc mã nguồn nào.
   - CẤM DÙNG: Controller, Service, Repository, API, DTO, Entity, Database, DB, SQL, SaveChanges, Update, Delete, Publish, Endpoint, Hub, RabbitMQ, MassTransit, Map/Mapper, Exception, Guid, Id, [Authorize], Identity, User, Filter, Include, Join...
   - PHẢI DÙNG: Người bán, người mua, bài đấu giá, tài sản, mức giá, gian lận, lỗi hệ thống, mất dữ liệu, quyền hạn, thông báo cho phân hệ khác, đồng bộ thông tin...
2. Đặt câu hỏi theo dạng tình huống thực tế (Scenario-based): Khảo sát trực tiếp vào quy trình vận hành (Ví dụ: ""Nếu người bán cố tình chỉnh sửa thông tin khi đã có người đặt giá thành công..."", ""Nếu hệ thống ghi nhận thanh toán nhưng việc cập nhật trạng thái bài đấu giá bị gián đoạn..."").

QUY TẮC BẮT BUỘC VỀ TRUY VẾT LUỒNG CODE (TARGETED ENTRY POINTS):
- Mảng ""targetedEntryPoints"" PHẢI mô tả chính xác và đầy đủ Call Stack (luồng đi từ Controller xuống tầng xử lý và lưu trữ) để giải quyết tình huống nghiệp vụ được hỏi.
- QUY ĐỊNH CẤU TRÚC MẢNG: Mỗi mảng PHẢI chứa đầy đủ các mắt xích theo thứ tự từ trên xuống dưới (Ít nhất 3 đến 4 phần tử tùy luồng):
  [ ""TênController.TênAction"", ""TênInterfaceService.TênHàmAsync"", ""TênClassServiceImpl.TênHàmAsync"", ""TênRepository.TênHàmAsync (nếu có)"" ]
- VÍ DỤ MẪU CHUẨN LUỒNG:
  [ ""AuctionController.CreateAuction"", ""IAuctionService.CreateAuctionAsync"", ""AuctionServiceImpl.CreateAuctionAsync"", ""IAuctionRepository.AddAsync"" ]
- TUYỆT ĐỐI KHÔNG ĐƯỢC bỏ sót việc bắt cặp giữa [Interface] và [Class triển khai]. Nếu thiếu bất kỳ tầng nào, kết quả sẽ bị coi là bất hợp lệ.
- CHỈ THỊ CHỐNG SUY DIỄN (ANTI-HALLUCINATION):
{antiHallucinationInstruction}

ĐỊNH DẠNG ĐẦU RA BẮT BUỘC:
- Trả về một mảng JSON chứa các đối tượng có cấu trúc chính xác như sau (Tuyệt đối không bọc mảng trong ký tự ```json, chỉ trả về JSON trần):
[
  {{
    ""question"": ""Câu hỏi tình huống nghiệp vụ thực tế ở đây"",
    ""suggestedAnswer"": ""Giải thích giải pháp xử lý logic nghiệp vụ ở đây (không chứa từ khóa code)"",
    ""difficulty"": ""{difficulty}"",
    ""targetedEntryPoints"": [
      ""TênController.TênAction"",
      ""TênInterfaceService.TênHàmAsync"",
      ""TênClassServiceImpl.TênHàmAsync""
    ]
  }}
]";

        var prompt = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(groundTruthSpecification))
        {
            prompt.AppendLine("--- QUY TRÌNH NGHIỆP VỤ CHUẨN ĐÃ CHỐT VỚI GIẢNG VIÊN (GROUND TRUTH SPECIFICATION) ---");
            prompt.AppendLine(groundTruthSpecification);
            prompt.AppendLine();
        }

        prompt.AppendLine("--- THÔNG TIN CHUNG ---");
        prompt.AppendLine($"Tên Business: {businessName}");
        prompt.AppendLine();

        prompt.AppendLine("--- THÔNG TIN DATA FLOW GRAPH ---");
        prompt.AppendLine(contextBuilder);
        prompt.AppendLine();

        prompt.AppendLine("--- SOURCE CODE CHI TIẾT (CODEBASE) ---");
        prompt.AppendLine(codeBuilder);
        prompt.AppendLine();

        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            prompt.AppendLine("--- HƯỚNG DẪN BỔ SUNG TỪ GIẢNG VIÊN ---");
            prompt.AppendLine(additionalContext);
            prompt.AppendLine();
        }

        // Nhúng few-shot examples vào prompt
        var examplesList = fewShotExamples?.ToList();
        if (examplesList != null && examplesList.Count > 0)
        {
            prompt.AppendLine("--- VÍ DỤ CÂU HỎI CHUẨN CỦA GIẢNG VIÊN (FEW-SHOT EXAMPLES) ---");
            prompt.AppendLine("Dưới đây là các câu hỏi mẫu do giảng viên cung cấp. Hãy học theo phong cách, độ sâu nghiệp vụ và cấu trúc câu trả lời của chúng:");
            prompt.AppendLine();
            for (int i = 0; i < examplesList.Count; i++)
            {
                var ex = examplesList[i];
                prompt.AppendLine($"[Ví dụ {i + 1}]");
                prompt.AppendLine($"  Câu hỏi   : {ex.Question}");
                prompt.AppendLine($"  Đáp án    : {ex.SuggestedAnswer}");
                prompt.AppendLine($"  Mức độ    : {ex.Difficulty}");
                if (!string.IsNullOrWhiteSpace(ex.Tag))
                    prompt.AppendLine($"  Nhãn      : {ex.Tag}");
                prompt.AppendLine();
            }
        }
        prompt.AppendLine($"Hãy trả về chính xác ĐÚNG {numberOfQuestions} câu hỏi mức độ {difficulty} dưới dạng một mảng JSON (JSON Array) chứa các đối tượng gồm các key: question, suggestedAnswer, difficulty, targetedEntryPoints.");

        return new QuestionPrompt(systemInstruction, prompt.ToString());
    }
}
