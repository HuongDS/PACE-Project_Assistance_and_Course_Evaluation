# Rubric đánh giá feedback của AI

Tài liệu mô tả các kiểu feedback AI sinh ra và bộ tiêu chí để sinh viên đánh giá feedback đó.
Dữ liệu tiêu chí được seed sẵn vào bảng `rubric_criteria` qua migration `InitialCreate`.

## 1. Phân biệt hai khái niệm "rubric"

| Khái niệm | Ai dùng | Lưu ở đâu |
|---|---|---|
| **Kiểu feedback Rubric** (một trong 3 kiểu AI chấm bài) | AI chấm câu trả lời của sinh viên | `student_answers.FeedbackFormat = 'rubric'` |
| **Rubric đánh giá feedback** (9 tiêu chí bên dưới) | Sinh viên chấm lại feedback mà AI đưa ra | `rubric_criteria` + `rubric_scores` |

## 2. Ba kiểu feedback của AI

Kiểu feedback gắn với **câu trả lời**, không gắn với câu hỏi: cùng một câu hỏi, mỗi lượt trả lời có thể được chấm theo một kiểu khác nhau.

| Giá trị `FeedbackFormat` | Tên hiển thị | Nội dung AI trả về (JSON lưu ở `student_answers.AiFeedback`) |
|---|---|---|
| `sandwich` | Sandwich (A) | `score`, `positives` (khen điểm đúng), `constructive_criticism` (chỉ lỗi), `next_steps` (gợi ý cải thiện) |
| `rubric` | Rubric (B) | `total_score`, `criteria[]` gồm `name`, `score`, `comment` cho từng tiêu chí |
| `explainable` | Explainable (C) | `score`, `code_evidence` (trích dẫn code), `logic_explanation` (giải thích vì sao) |

Điểm AI chấm được lưu riêng ở `student_answers.AiScore` (lấy từ `score` hoặc `total_score`).

## 3. Chín tiêu chí sinh viên đánh giá feedback

Thang Likert 1–5: 1 = Hoàn toàn không đồng ý, 2 = Không đồng ý, 3 = Trung lập, 4 = Đồng ý, 5 = Hoàn toàn đồng ý.
Sinh viên chấm đủ 9 tiêu chí cho mỗi câu trả lời (`rubric_scores`, mỗi tiêu chí một dòng).

| # | Code | Tên | Câu phát biểu hiển thị cho sinh viên | Nguồn |
|---|---|---|---|---|
| 1 | `ACCURACY` | Độ chính xác kỹ thuật | Nhận xét đúng về mặt kỹ thuật so với code thực tế trong repo của nhóm tôi. | Steiss et al. (2024) – *Accuracy* |
| 2 | `FAIRNESS` | Công bằng | Điểm số và nhận xét phản ánh công bằng chất lượng câu trả lời của tôi. | Strijbos et al. (2021) – FPQ *Fairness* |
| 3 | `SPECIFICITY` | Cụ thể | Nhận xét chỉ rõ phần nào trong câu trả lời của tôi đúng, phần nào sai hoặc còn thiếu. | Hattie & Timperley (2007) – *feed back*; Shute (2008) |
| 4 | `ACTIONABILITY` | Có thể hành động | Sau khi đọc, tôi biết cụ thể cần làm gì để cải thiện câu trả lời. | Hattie & Timperley (2007) – *feed forward*; Steiss et al. (2024) – *clear directions for improvement* |
| 5 | `USEFULNESS` | Hữu ích | Nhận xét này giúp tôi hiểu rõ hơn luồng xử lý trong project của mình. | Strijbos et al. (2021) – FPQ *Usefulness* |
| 6 | `CLARITY` | Rõ ràng | Nhận xét được trình bày rõ ràng, dễ hiểu. | Shute (2008) |
| 7 | `ACCEPTANCE` | Chấp nhận | Tôi đồng ý và chấp nhận nhận xét này. | Strijbos et al. (2021) – FPQ *Acceptance* |
| 8 | `WILLINGNESS` | Sẵn sàng cải thiện | Tôi sẵn sàng sửa lại câu trả lời dựa trên nhận xét này. | Strijbos et al. (2021) – FPQ *Willingness to improve* |
| 9 | `AFFECT` | Cảm xúc | Đọc nhận xét này khiến tôi thấy được động viên, không bị nản. | Strijbos et al. (2021) – FPQ *Affect* |

Nhóm tiêu chí:
- **Cảm nhận của sinh viên** (FPQ): FAIRNESS, USEFULNESS, ACCEPTANCE, WILLINGNESS, AFFECT
- **Chất lượng nội dung feedback**: ACCURACY, SPECIFICITY, ACTIONABILITY, CLARITY

## 4. Tài liệu tham khảo

1. Strijbos, J.-W., Pat-El, R. J., & Narciss, S. (2021). Structural validity and invariance of the Feedback Perceptions Questionnaire. *Studies in Educational Evaluation, 68*, 100980. https://doi.org/10.1016/j.stueduc.2021.100980
2. Hattie, J., & Timperley, H. (2007). The power of feedback. *Review of Educational Research, 77*(1), 81–112. https://doi.org/10.3102/003465430298487
3. Shute, V. J. (2008). Focus on formative feedback. *Review of Educational Research, 78*(1), 153–189. https://doi.org/10.3102/0034654307313795
4. Steiss, J., Tate, T., Graham, S., et al. (2024). Comparing the quality of human and ChatGPT feedback of students' writing. *Learning and Instruction*. https://doi.org/10.1016/j.learninstruc.2024.101894

## 5. Lưu ý khi dùng cho bài báo

- Các câu phát biểu tiếng Việt được chuyển ngữ và điều chỉnh cho ngữ cảnh code/repo, không phải nguyên văn bảng hỏi gốc → ghi *"adapted from"* khi trích dẫn.
- Mỗi khía cạnh hiện có 1 câu; nên chạy thử (pilot) và báo cáo Cronbach's alpha cho nhóm 5 tiêu chí FPQ.
- Muốn thêm hoặc sửa tiêu chí: thêm dòng trong `RubricCriterionConfiguration` (HasData) rồi tạo migration mới.
