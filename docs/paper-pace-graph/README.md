# PACE · Hồ sơ nghiên cứu B0–B5

**Câu hỏi trung tâm:** khi PACE dùng phân tích tĩnh để dựng graph và dùng graph làm bằng chứng cho câu hỏi vấn đáp, phần nào đã cải thiện, và cần đo thêm gì để kết luận trên dự án mới?

| Đọc theo thứ tự | Nội dung |
|---|---|
| [01 · Cơ sở nghiên cứu](01-research.md) | B0–B5 làm gì, phương pháp lấy từ paper nào, số B0–B4 và giới hạn của từng bộ dữ liệu. |
| [02 · Protocol](02-protocol.md) | Chỉ số đã triển khai, cách đặt đối chứng và thiết kế chấm mù cho bài báo. |
| [03 · Kết quả và tái lập](03-results.md) | Kết quả B5 từng ca, kết quả kiểm tra hồi quy, lượt lặp, hash, lệnh chạy và gap. |
| [Runner benchmark](../../benchmarks/README.md) | Cách chuẩn bị nguồn, commit đã ghim và cách để mỗi lần chạy tự ghi log. |
| [Mode Evidence](../question-generation.md#evidence) | Cách gọi API và hành vi khi nguồn không đủ bằng chứng. |

## Đọc đúng các số

- **Chất lượng graph và endpoint.** JCG, AutoOAS, microSecEnD và Jasmine có nguồn công bố nhưng đo các đối tượng khác nhau. Bốn bộ đã được dùng trong phát triển. So B0–B5 trên chúng giúp kiểm tra hồi quy và mô tả thay đổi; đánh giá khả năng tổng quát cần tập độc lập.
- **Chất lượng câu hỏi.** Fixture B5 đo việc trích đúng dữ kiện theo oracle nhỏ đã khóa. Phép quan sát trên route công khai đếm câu sinh được. Độ khó, chất lượng giáo dục và mức tăng so với B4 cần đối chứng trực tiếp qua giảng viên chấm.
- **Đối chứng lịch sử.** 36 nhãn Spring PetClinic REST là các operation OpenAPI được nối với concrete handler ở một commit, dùng riêng để đối chiếu B0. Spring PetClinic Microservices là một dự án khác trong microSecEnD. Số Sport Center là quan sát trên source không nhãn. [Phạm vi benchmark](../../benchmarks/README.md)

Đóng góp có thể viết ngay ở phần phương pháp là pipeline tách xây graph và tạo câu hỏi, kèm bộ chấm theo từng lớp. Giả thuyết thực nghiệm chính là tăng tỷ lệ câu đúng và có ích trên cùng ngân sách hỏi. Hồ sơ ghi rõ thiết kế kiểm chứng còn cần thực hiện. [Kildall](https://doi.org/10.1145/512927.512945) · [Code-QA-Bench, preprint](https://arxiv.org/abs/2605.29277) · [ACM SIGSOFT Empirical Standards](https://www2.sigsoft.org/EmpiricalStandards/)
