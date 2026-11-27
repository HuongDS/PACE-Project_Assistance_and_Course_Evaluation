# PACE · Kết quả B5 và tái lập

## B5 làm gì

Khi gọi mode Evidence, PACE lấy những hàm đã liên kết với nghiệp vụ. Với mỗi CFG Java ở trạng thái `Complete`, hệ thống xem nhánh đúng hoặc sai dẫn tới câu lệnh nào và những chỗ gán nào có thể đưa giá trị đến một lần đọc biến. Hệ thống chọn đáp án cùng vị trí nguồn trước, rồi viết câu hỏi theo mẫu. Dữ liệu thiếu, ngoài phạm vi hoặc tọa độ không đối chiếu được sẽ không tạo câu từ dữ kiện đó. Lượt hỏi có thể trả ít hơn ngân sách và báo số chưa lấp. Mode chạy ngoại tuyến, dùng mẫu tiếng Việt, và ghi nhãn độ khó theo yêu cầu. [Code-QA-Bench, preprint](https://arxiv.org/abs/2605.29277) · [Hướng dẫn API](../question-generation.md#evidence)

Ví dụ ở ca hai nhánh nhập lại, dòng 5 gán `x` trên nhánh đúng và dòng 7 gán `x` trên nhánh sai. Cả hai đi tới chỗ đọc tại dòng 9. Câu hỏi yêu cầu liệt kê các định nghĩa có thể tới chỗ đọc này. Đáp án chỉ đến dòng 5, cột 7 và dòng 7, cột 7. Các vị trí có thể tới theo phân tích tĩnh. Một lượt chạy cụ thể chỉ đi qua các vị trí phù hợp với đầu vào. [Oracle Java](../../benchmarks/fixtures/grounded-questions.json)

## Câu hỏi trên fixture đã ghim

| Ca Java | Slot yêu cầu | Câu khớp oracle | Slot trống |
|---|---:|---:|---:|
| Hai nhánh và còn ngân sách | 5 | 3 | 2 |
| Hai định nghĩa gặp nhau | 4 | 4 | 0 |
| Thoát sớm làm một định nghĩa không đến được | 5 | 5 | 0 |
| Định nghĩa truyền qua vòng lặp | 4 | 4 | 0 |
| `try/finally` ngoài phạm vi | 2 | 0 | 2 |
| CFG hợp lệ nhưng thiếu dạng câu hỗ trợ | 2 | 0 | 2 |
| Method cũ chưa lưu CFG | 2 | 0 | 2 |
| CFG không còn đối chiếu được với source | 2 | 0 | 2 |
| **Tổng** | **26** | **16** | **10** |

Cả 8 ca cho đúng tập kết quả kỳ vọng. Có **16/16 câu sinh ra** khớp loại dữ kiện và tập dòng đích, được phân bổ trên **4/8 ca**. Tỷ lệ câu khớp trên toàn ngân sách là **16/26 (61,54%)**. Cả **10/26 slot thiếu (38,46%)** phù hợp kỳ vọng. Có 0 đáp án sai theo oracle, 0 câu dự kiến bị hụt, 0 câu trùng trong lượt và 0 lượt sinh thiếu chuỗi tọa độ cho dữ kiện đã trích. Oracle chấm ngữ nghĩa ở cấp dòng trên tám tình huống tự tạo. Phần kiểm tra chuỗi tọa độ xác nhận trích dẫn xuất hiện trong câu và đáp án. Nó chưa có nhãn độc lập ở cấp cột, chưa chấm độ tự nhiên hay giá trị vấn đáp.

## Hồi quy trên bốn bộ phát triển

| Phép đo | B4 | B5 |
|---|---:|---:|
| JCG · đích yêu cầu tìm thấy | 68/81 | 68/81 |
| JCG · đích bị cấm xuất hiện | 1/11 | 1/11 |
| AutoOAS GT+ · cặp verb/path khớp | 488/551 | 488/551 |
| AutoOAS GT+ · cặp ngoài nhãn cần xác minh | 14 | 14 |
| AutoOAS spec mẫu · cặp verb/path khớp | 35/61 | 35/61 |
| AutoOAS spec mẫu · cặp ngoài nhãn cần xác minh | 467 | 467 |
| microSecEnD · project/path khớp | 97/119 | 97/119 |
| microSecEnD · project/path ngoài nhãn cần xác minh | 4 | 4 |
| Jasmine · đích DI tìm thấy | 6/6 | 6/6 |
| Jasmine · advice AOP tìm thấy | 0/8 | 0/8 |
| Method Java có CFG trạng thái `Complete` | 4.275/4.807 | 4.275/4.807 |

JCG có 34 annotation ngoài phạm vi trực tiếp, 4 annotation không quy được về một call site theo định nghĩa phép chiếu, và 5 ca thiếu caller trong mẫu số đủ điều kiện. B5 giữ nguyên trạng thái các ca và đầu ra graph cũ. Trong 532 method Java chưa có CFG đầy đủ, 531 ngoài phạm vi và 1 chạm giới hạn; các lý do thường gặp nhất là `try_statement` (118), `lambda_expression` (114) và `enhanced_for_statement` (93). Đây là thống kê hỗ trợ kỹ thuật, không suy ra độ đúng CFG trên toàn repo. [Judge](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf) · [Chấm lại AutoOAS GT+ trước B5](../../benchmarks/results/auto-oas-gt-plus-audit.json)

## Độ bao phủ sinh câu trên route công khai

| Nguồn route | Phát hiện | Có entry | Có câu | Đủ 5 câu | Slot có câu |
|---|---:|---:|---:|---:|---:|
| AutoOAS Spring | 504 | 504 | 478 | 351 | 2.014/2.520 |
| microSecEnD | 152 | 151 | 94 | 27 | 252/755 |
| Jasmine | 36 | 36 | 19 | 2 | 46/180 |
| **Tổng** | **692** | **691** | **591** | **380** | **2.312/3.455** |

Trong số route có entry, **591/691 (85,53%)** có câu, **380/691 (54,99%)** đủ năm câu và **100/691** không sinh câu. Tổng 2.312 câu chiếm **66,92%** ngân sách, còn 1.143 slot trống. Có 1.680 câu về định nghĩa tới chỗ đọc, 326 về nhánh đúng và 306 về nhánh sai. Một route do PACE phát hiện nhưng chưa tìm được entry nằm ngoài ngân sách. Đơn vị là graph route do PACE phát hiện từ source đã ghim, gồm cả ánh xạ mà nhãn endpoint chưa xác minh. Cùng một hàm có thể đóng góp cho nhiều route. Số 2.312 là số câu được sinh, chưa có nhãn để xác nhận bao nhiêu câu đúng trên các repo này.

## Hai lượt chạy và tệp bằng chứng

| Lượt đo | ID UTC | Tổng scan, parse và graph của 25 mục | Trung vị mỗi mục |
|---|---|---:|---:|
| B4 lưu trước đó | `20261125T100605904945Z-B4` | 72,513 s | 2,362 s |
| B5 công khai | `20261125T175210929015Z-B5` | 89,599 s | 2,951 s |
| B5 công khai lặp lại | `20261125T175504219380Z-B5Repeat` | 77,519 s | 2,403 s |
| B5 fixture | `20261125T175159831633Z-B5` | — | — |
| B5 fixture lặp lại | `20261125T175459357064Z-B5Repeat` | — | — |

Hai lượt fixture có file kết quả giống nhau từng byte. Hai lượt công khai cho cùng câu hỏi theo từng route và cùng chỉ số. Cả hai khớp B4 từng hàng JCG, từng ca chấm route, CFG coverage và đầu ra graph của 25 mục. Không có mục công khai lỗi chạy. Đối chiếu AutoOAS GT+ cho 0 cặp từng khớp bị mất và 0 cặp mới khớp so với B4. Độ dao động giữa hai lần chạy B5 còn lớn và không có thí nghiệm xen kẽ có kiểm soát để quy chênh lệch thời gian cho B5. Bảng lịch sử B0–B4 dùng các lượt đã lưu. [B5 công khai, từng repo và pin](../../benchmarks/results/public-b5-summary.json)

Chạy lại 52 kiểm thử ngoại tuyến liên quan, 52 đạt. Fixture Java signature đạt 10/10 ca, Spring route/handler 15/15, call site 7/7 và CFG 8/8. Đối chứng nhỏ C# vẫn cho 7 TP, 2 FP, 1 FN, F1 82,4% và chọn handler 0/1. Các số này có mẫu số riêng và được giữ như kiểm tra hồi quy. [Kết quả B5 từng câu và fixture hồi quy](../../benchmarks/results/b5-grounded-questions.json)

Các lượt B5 chính dùng HEAD `dcf6f995982b3b04c8153011428ee23336e82bba` cùng thay đổi trong working tree. Fingerprint mã sản phẩm là `936c22c7a00d52c61f001d40d5fc1bc66c9e550ba50c51af9664e5a8bb19bc58` và của harness lúc chạy là `7f7d822b9dfde7e270b3a3e643f4887aabedc4d4566fdf6ad8c75895a2030f4d`. Môi trường .NET SDK 9.0.124, JDK 21.0.13.1 chỉ với thư viện JDK, Python 3.12.3 trên Linux x86-64. Hash fixture là `38983f8026f5cfa2cfbc851e6c6ba0245c2d5c6f1681a55d71c2f52a4d49f15c`. Hash nguồn và nhãn công khai, từng báo cáo, bản chạy nháp và bản cuối đều có trong JSON. Bản chạy nháp được giữ để kiểm toán, số tổng hợp dùng bản cuối.

Từ thư mục gốc PACE, dùng JDK và .NET đã restore. Nguồn công khai chuẩn bị theo manifest trong README benchmark.

```bash
python3 benchmarks/run.py --stage B5 --suite grounded-questions --no-restore
python3 benchmarks/run.py --stage B5 --observe-grounded --no-restore \
  --compare benchmarks/local/runs/20261125T100605904945Z-B4
```

Nếu chưa có lượt B4 tại đường dẫn trên, có thể chạy B5 mà bỏ tùy chọn `--compare`. Runner vẫn lưu điểm, từng câu hỏi, kết quả từng ca, TRX và log trong thư mục UTC in ra ở terminal. Dùng script audit để chấm AutoOAS GT+ trên các thư mục lượt chạy đã lưu. Mã thay đổi có trong `b5-source.patch` của gói bằng chứng cục bộ. Lưu gói này khi chuyển máy vì Git bỏ qua thư mục `benchmarks/local`. [Nguồn và lệnh benchmark](../../benchmarks/README.md) · [Gói log, TRX, đầu ra gốc và patch](../../benchmarks/local/pace-b5-evidence.tar.gz)

## Giới hạn và khả năng kết luận

- B5 dùng hai dạng dữ kiện nội hàm Java. Câu hỏi về hành vi liên dịch vụ, DI/AOP, heap, route gián tiếp hoặc code ngoài phạm vi CFG chưa được đo. `Complete` là trạng thái theo phạm vi B4, không phải tuyên bố analyzer bao phủ toàn Java. Kiểm tra tọa độ chỉ cho biết sự phù hợp cục bộ với snippet, chưa chứng nhận phiên bản file khi câu hỏi được sử dụng lâu sau lượt phân tích. [Java Language Specification](https://docs.oracle.com/javase/specs/jls/se21/html/jls-14.html) · [Judge](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf)
- B4 đã sinh được câu hỏi qua LLM. Chưa có lượt hỏi B4/B5 ghép cặp cùng nghiệp vụ, chưa có chấm của giảng viên và chưa có holdout. B5 hiện là mode API chủ động chọn. Kiểm thử service dùng repo giả lập và xác nhận không gọi LLM, chưa kiểm thử qua database thật hoặc đo end-to-end, bộ nhớ và chi phí B4/B5. Không báo 0 làm điểm chuẩn B4 cho fixture B5. [Protocol](02-protocol.md)
- B6 và B7 chưa triển khai. Hai ứng viên là chấm tác động của classpath đầy đủ lên việc phân giải Java, và thí nghiệm diễn đạt câu hỏi với ML/LLM nếu rubric xác nhận câu đúng nhưng quá máy móc. Việc bổ sung cú pháp CFG nên dựa trên lỗi thiếu câu được xác minh theo repo và oracle độc lập. [Jasmine](https://doi.org/10.1145/3551349.3556910) · [Learning to Ask](https://aclanthology.org/P17-1123/) · [Cơ sở và gap](01-research.md)
