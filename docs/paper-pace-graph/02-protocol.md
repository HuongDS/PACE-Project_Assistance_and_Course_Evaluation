# PACE · Protocol B5 và kiểm chứng cuối

## Phạm vi chốt trước khi sửa B5

B4 checkpoint dcf6f995982b3b04c8153011428ee23336e82bba là mốc hồi quy. B5 bổ sung mode Evidence tại API sinh câu hỏi. Pipeline lấy CFG đã lưu, xác định đáp án có cấu trúc gồm loại kết luận, tọa độ và tập đích, rồi mới dựng câu hỏi theo mẫu. Hai dạng câu là nhánh đúng/sai dẫn tới vị trí nào, và những định nghĩa nào có thể tới một chỗ đọc biến. Thiếu CFG, graph ngoài scope, hoặc vị trí không đối chiếu được với source thì bỏ ngỏ. Pipeline ngoại tuyến và xác định. [Code-QA-Bench, preprint 2026](https://arxiv.org/abs/2605.29277)

1. **Fixture khóa trước khi sửa.** Dùng Java ngắn với rẽ nhánh, thoát sớm, điểm nhập, vòng lặp, cấu trúc chưa hỗ trợ, thiếu CFG và dẫn chứng lỗi. Chấm tập vị trí đích chính xác, dẫn chứng và slot còn trống. B4 chưa có mode Evidence. Các ca này kiểm thử B5 với oracle, không chấm chất lượng câu hỏi B4. Chạy lại bốn bộ cũ trên nhãn và nguồn đã ghim để dò hồi quy.
2. **Holdout cho paper.** Lấy mẫu theo repo và theo nghiệp vụ. Chốt tiêu chí, snapshot, nhãn, ngân sách và cách chấm trước khi xem đầu ra. Loại trùng repo, fork và version đã dùng. Hai người chấm độc lập theo source, ẩn phiên bản, đảo thứ tự câu hỏi, ghi mức đồng thuận thô, Cohen’s κ nếu phù hợp và hòa giải. Đừng dùng model sinh câu làm người chấm duy nhất. [LongEval](https://aclanthology.org/2023.eacl-main.121/) · [Empirical Standards](https://www2.sigsoft.org/EmpiricalStandards/)

## Phép quan sát bổ sung trên tập phát triển

Mỗi graph của route do PACE phát hiện có entry xác định được sẽ được hỏi tối đa năm câu. Dùng các method trong graph của route làm đại diện cho đầu vào. Đếm riêng route phát hiện, route có entry, route có ít nhất một câu, route đủ năm câu, slot đã lấp và loại câu. Route chưa tìm được entry nằm ngoài ngân sách hỏi và vẫn có mặt trong mẫu số route phát hiện. Nhãn endpoint không tham gia sinh hoặc tự chấm câu hỏi. Phép quan sát này không chạy qua database, LLM hay thao tác gom nhiều route thành một nghiệp vụ. Một câu có thể lặp giữa hai route dùng chung method. Thời gian scan, parse và dựng graph được chốt trước bước sinh câu.

## Bộ chỉ số

| Tiêu chí | Đơn vị và cách chấm |
|---|---|
| Câu hợp lệ trên ngân sách | Số câu trả lời được, có đáp án đúng và đủ chứng cứ chia tổng slot được yêu cầu. Giữ cả slot bỏ trống, lỗi và trùng trong mẫu số. |
| Sai tiền đề hoặc sai đáp án | Báo số trên câu đã sinh và số trên tổng yêu cầu. Tách sai tiền đề, sai đáp án, mơ hồ. |
| Dẫn chứng | Đo tọa độ tồn tại và dẫn chứng có hỗ trợ mệnh đề, ghi riêng. Tồn tại vị trí không đủ để chứng minh mệnh đề. |
| Đáp án có cấu trúc | So tập tọa độ đích chính xác theo từng ca, tính TP/FP/FN nếu oracle đủ. Cạnh ngoài phạm vi nhãn giữ trạng thái chưa xác minh. |
| Bỏ ngỏ và độ bao phủ | Số slot chưa sinh, số nghiệp vụ có đầu ra và nguyên nhân. Báo cạnh số câu hợp lệ. |
| Trùng và đa dạng | Số câu trùng loại/vị trí/đích, chủ đề và mức khó theo repo. |
| Tính sư phạm | Giảng viên chấm độ rõ, mức suy luận, độ khó và ích lợi cho vấn đáp. |
| Chi phí | Thời gian end-to-end và từng pha, bộ nhớ, token nếu có LLM. Báo số lượt và môi trường. |

## Đối chứng cho bài báo

- So B4 prompt hiện tại với B5 mẫu từ CFG trên cùng nghiệp vụ và số câu yêu cầu. Đây là so sánh toàn pipeline. Khi muốn quy riêng hiệu ứng cho answer-first hoặc graph trong một pipeline LLM, giữ cùng model/version, nhiệt độ, ngân sách và số lượt. So hỏi trước, đáp án trước dùng code, và đáp án trước dùng code cùng graph.
- Final chốt hash code, nguồn, nhãn và cách loại trừ. Chạy B0 với bản chốt trên holdout cho phần graph. Chạy B4 với B5 trên cùng holdout cho phần vấn đáp. Báo theo repo và paired delta, bootstrap theo repo nếu đủ nhóm. Nếu dùng kết quả holdout để chỉnh phương pháp, đánh dấu bộ đã xem và dành tập khác cho lần kiểm chứng tiếp. [Dwork et al.](https://pubmed.ncbi.nlm.nih.gov/26250683/) · [BESS](https://doi.org/10.1145/3463274.3463361)

## Phiếu chấm đề xuất cho đối chứng

Mỗi hàng gồm ID ẩn, repo@SHA, nghiệp vụ, slot, câu và đáp án, vị trí code, người chấm và ghi chú. Người chấm đánh dấu **đúng, sai, thiếu bằng chứng, mơ hồ hoặc không có câu**, kèm nguồn và lý do. Độ rõ, độ khó và ích lợi cho vấn đáp dùng thang 1–5 với ví dụ neo do giảng viên thống nhất trên tập pilot riêng. Chấm mù phiên bản và ghi cả nhận xét, đồng thuận trước hòa giải, kết quả sau hòa giải, số hàng loại và lý do. Chốt cỡ mẫu bằng pilot ngoài holdout và mức chênh lệch có ý nghĩa thực tiễn. Khi ước lượng khoảng tin cậy, coi repo là cụm để các câu từ cùng code không được xem như các mẫu độc lập. [LongEval](https://aclanthology.org/2023.eacl-main.121/) · [Empirical Standards](https://www2.sigsoft.org/EmpiricalStandards/)
