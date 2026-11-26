# PACE · Cơ sở nghiên cứu và rà soát bằng chứng

## Câu hỏi nghiên cứu

- **RQ1.** B1–B4 làm thay đổi đích gọi, endpoint, vị trí gọi và luồng nội hàm như thế nào trên đầu vào có nhãn phù hợp?
- **RQ2.** B5 sinh được bao nhiêu câu hỏi–đáp án có dẫn chứng kiểm tra được, và phải để trống bao nhiêu lượt yêu cầu?
- **RQ3.** Kết quả có lặp lại trên dự án chưa được dùng để phát triển? Chi phí thời gian và bộ nhớ là bao nhiêu?

Bốn bộ cũ đều là **tập phát triển**. Fixture là kiểm thử hồi quy do PACE thiết kế. Sport Center là quan sát không nhãn và đã được xem. Đơn vị đo khác nhau giữa các bộ. [Báo cáo kiểm toán B0–B4](../pace-graph-research-handoff.md)

## Phương pháp và nguồn gốc

| Mốc | PACE thực hiện | Cơ sở và lý do |
|---|---|---|
| B0 | Giải lời gọi dựa vào tên, receiver và số đối số. Dùng matcher cùng parser ban đầu. | Commit sản phẩm 22351f5 là đối chứng thực nghiệm. |
| B1 | Tree-sitter đọc source. Javac phân giải khai báo theo kiểu/chữ ký. PACE suy tập đích ảo theo phân cấp kiểu trong source. | JLS tách lựa chọn lúc biên dịch khỏi dispatch lúc chạy. Unimocg là tham chiếu cho cách tách xử lý tính năng khỏi thuật toán đồ thị. B1 không chạy Unimocg hoặc points-to. [JLS §15.12](https://docs.oracle.com/javase/specs/jls/se21/html/jls-15.html#jls-15.12) · [Unimocg, ISSTA 2024](https://doi.org/10.1145/3650212.3652109) |
| B2 | Đọc alias, verb và path của annotation Spring trong phạm vi hỗ trợ. Ghép mapping kế thừa từ symbol override và giữ ca mơ hồ. | Tài liệu Spring định nghĩa mapping. AutoOAS cung cấp nhãn đối chiếu endpoint. [Spring](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-controller/ann-requestmapping.html) · [AutoOAS](https://www.sciencedirect.com/science/article/pii/S0164121226003559) |
| B3 | Giữ cạnh theo từng vị trí gọi và đích, giữ đệ quy, lưu lý do dừng theo ngân sách. | Judge dùng annotation ở điểm gọi để đánh giá đích cần có và đích bị cấm. Phép chiếu của PACE tương thích B0 ở mức owner + method nên điểm JCG hiện tại chưa chấm riêng từng tọa độ. [Judge, ISSTA 2019](https://conf.researchr.org/details/issta-2019/issta-2019-Technical-Papers/24/Judge-Identifying-Understanding-and-Evaluating-Sources-of-Unsoundness-in-Call-Grap) |
| B4 | Dựng CFG cấp câu lệnh cho Java trong phạm vi hỗ trợ. Tính các định nghĩa có thể tới chỗ đọc biến theo điểm cố định. Một lần gán thay thế lần gán trước trên nhánh hiện tại và các tập định nghĩa được hợp tại điểm nhập. | Kildall đặt nền tảng cho phân tích luồng dữ liệu trên CFG. Yamaguchi và cộng sự mô tả suy quan hệ dữ liệu từ def/use và reaching definitions. PACE áp dụng phần nội hàm của ý tưởng, chưa triển khai toàn bộ CPG. [Kildall, POPL 1973](https://doi.org/10.1145/512927.512945) · [Code Property Graphs, IEEE S&P 2014](https://www.ieee-security.org/TC/SP2014/papers/ModelingandDiscoveringVulnerabilitieswithCodePropertyGraphs.pdf) · [JLS chương 14](https://docs.oracle.com/javase/specs/jls/se21/html/jls-14.html) |
| B5 | Chọn câu trả lời từ CFG và vị trí code trước, rồi dựng câu hỏi theo mẫu. | PACE chuyển ý tưởng answer-first của Code-QA-Bench sang câu hỏi vấn đáp Java. Mẫu hỏi và rubric khác paper. LongEval là nguồn tham khảo cho kiểm tra từng mệnh đề với đoạn source và báo mức đồng thuận. [Code-QA-Bench, preprint 2026](https://arxiv.org/abs/2605.29277) · [LongEval, EACL 2023](https://aclanthology.org/2023.eacl-main.121/) |

RepoQA đo tìm hàm từ mô tả, có thể tham khảo khi đo truy xuất bằng chứng. CrossCodeEval đo code completion qua file. Cả hai không trực tiếp đo chất lượng câu hỏi do PACE sinh. Báo validity, độ tin cậy và khả năng tái lập theo chuẩn SIGSOFT và BESS. [RepoQA](https://arxiv.org/abs/2406.06025) · [CrossCodeEval](https://github.com/amazon-science/cceval) · [ACM SIGSOFT Empirical Standards](https://www2.sigsoft.org/EmpiricalStandards/) · [BESS, EASE 2021](https://doi.org/10.1145/3463274.3463361)

## Vì sao chọn hướng này

| Công trình | Bằng chứng và quyết định cho PACE |
|---|---|
| Architecture-Aware Static Analysis and Violation Detection of C# Student Submissions, 2026 | Dùng Roslyn để trích phụ thuộc và phát hiện lệch kiến trúc. Việc suy layer giảm chất lượng khi thiếu cấu trúc namespace, cách đặt tên quen thuộc hoặc phụ thuộc gần phi chu trình. PACE có thể mượn thiết kế đối chiếu với nhãn người chấm, nhưng lỗi kiến trúc khác đích gọi, CFG và chất lượng vấn đáp. [Computers](https://doi.org/10.3390/computers15070404) |
| The accuracy of dependency analysis in static architecture compliance checking, 2017 | Bộ testware Java bao phủ 34 loại phụ thuộc, thích hợp nếu có nhánh nghiên cứu kiểm tra kiến trúc. Tách phép đo phụ thuộc lớp ra khỏi endpoint và CFG. [Pruijt et al.](https://www.architecturemining.org/publications/PruijtKWB17.pdf) |
| RepoAgent, 2024 | Tài liệu repo có lợi từ việc gom ngữ cảnh tham chiếu. Đây là lựa chọn nếu sau này cần mở rộng câu hỏi theo nhiều hàm. Bản demo đánh giá sinh tài liệu, dùng nguồn Python, nên PACE cần chấm riêng cho Java. [EMNLP Demo](https://aclanthology.org/2024.emnlp-demo.46/) |
| Context-aware Code Summary Generation, bản tạp chí 2026 | Bài gốc nghiên cứu tóm tắt hàm Java có ngữ cảnh call graph và báo kết quả trên tác vụ tóm tắt. Phù hợp làm cơ sở cho thí nghiệm cấp ngữ cảnh. B5 hiện ưu tiên các câu hỏi có đáp án xác định theo CFG và dữ liệu. [JSS](https://doi.org/10.1016/j.jss.2025.112580) |
| Do Code LLMs Do Static Analysis?, 2026 | Các mô hình được thử làm yếu ở tác vụ dựng call graph, AST và data flow. Paper không đánh giá thiết kế PACE hoặc xác lập rằng mọi dạng cấp graph đều làm câu hỏi tốt hơn. PACE để analyzer cung cấp dữ kiện. [EMSE](https://link.springer.com/article/10.1007/s10664-026-10853-z) |
| Code-QA-Bench, preprint 2026 | Tạo đáp án trước bằng cách kiểm tra mã, sau đó mới lập câu hỏi và rubric. Dữ liệu gốc dùng repo Python. PACE áp dụng thứ tự tạo câu, dùng CFG Java và mẫu xác định. [Preprint](https://arxiv.org/abs/2605.29277) · [Replication artifact](https://github.com/Lens-Frontier/code-qa-bench) |

Các ứng viên dữ liệu có nhãn công bố gần hơn với RQ2 gồm **CodeQA** với 119.778 cặp trên đoạn Java, **StackRepoQA** với 1.318 câu hỏi thực và đáp án được chấp nhận trên 134 dự án Java, cùng **SWE-QA** với 720 câu hỏi cấp repo được tuyển chọn và kiểm tra. Các bộ này đưa câu hỏi có sẵn để đo **trả lời câu hỏi**. Với PACE, chúng thích hợp cho một thí nghiệm trả lời hoặc truy xuất bằng chứng riêng. Đáp án từ comment hoặc Stack Overflow không tự thành oracle CFG hay rubric sư phạm. Trước khi chọn, kiểm tra snapshot, giấy phép, trùng họ repo và nguy cơ học thuộc. Lượt B5 này chưa lấy dữ liệu hoặc xem đầu ra trên các bộ ứng viên. [CodeQA](https://aclanthology.org/2021.findings-emnlp.223/) · [StackRepoQA, arXiv v2](https://arxiv.org/abs/2603.26567v2) · [SWE-QA](https://aclanthology.org/2026.findings-acl.402/)

Gap nghiên cứu đề xuất là **liệu câu hỏi vấn đáp Java được sinh từ bằng chứng tĩnh có tăng tỷ lệ câu dùng được trên cùng ngân sách, khi tính cả câu thiếu và dùng giảng viên chấm mù trên repo giữ lại**. Có thể kiểm chứng giả thuyết này bằng thí nghiệm ở protocol. Tính mới ở giao điểm của vấn đáp, bằng chứng cấp mã và phép đo theo ngân sách còn cần khảo sát hệ thống trước khi tuyên bố ưu tiên.

## Số liệu B0–B4 đã ghi

| Chỉ số trên tập phát triển | B0 | B1 | B2 | B3 | B4 |
|---|---:|---:|---:|---:|---:|
| JCG · đích cần có | 56/81 | 68/81 | 68/81 | 68/81 | 68/81 |
| JCG · đích bị cấm xuất hiện | 3/11 | 1/11 | 1/11 | 1/11 | 1/11 |
| AutoOAS GT+ · verb/path khớp | 293/551 | 293/551 | 488/551 | 488/551 | 488/551 |
| AutoOAS GT+ · ngoài nhãn | 16 | 16 | 14 | 14 | 14 |
| AutoOAS spec mẫu · verb/path khớp | 30/61 | 30/61 | 35/61 | 35/61 | 35/61 |
| AutoOAS spec mẫu · ngoài nhãn | 279 | 279 | 467 | 467 | 467 |
| microSecEnD · dự án/path khớp | 97/119 | 97/119 | 97/119 | 97/119 | 97/119 |
| microSecEnD · ngoài nhãn | 4 | 4 | 4 | 4 | 4 |
| Jasmine · đích DI tìm thấy | 6/6 | 6/6 | 6/6 | 6/6 | 6/6 |
| Jasmine · advice AOP tìm thấy | 0/8 | 0/8 | 0/8 | 0/8 | 0/8 |
| Tổng thời gian 25 mục ngoài JCG, giây | 2,469 | 71,826 | 74,118 | 74,455 | 72,513 |
| Trung vị mỗi mục, giây | 0,022 | 2,274 | 2,397 | 2,391 | 2,362 |

- JCG có 117 annotation, 79 trong phạm vi chấm, thuộc 74 chương trình. B1 tăng 12 đích bắt buộc, +14,81 điểm phần trăm. Khoảng bootstrap ghép cặp 95% theo chương trình từ +7,41 đến +23,38 điểm phần trăm, áp dụng cho phép đo này. [Đầu ra B0](../../benchmarks/results/public-b0-summary.json) · [So sánh B1](../../benchmarks/results/public-b1-summary.json)
- AutoOAS GT+ lấy 551 cặp của 7 dự án Spring. Mẫu 61 nằm trong GT+. B2 mới khớp 203, mất 8, tăng ròng 195 cặp, tức +35,39 điểm phần trăm. Trong số mới khớp, 202 là POST. Hai profile CWA gộp ở cấp dự án. Cặp ngoài nhãn cần xác minh trước khi gọi là false positive. [Artifact v2](https://zenodo.org/records/20962871) · [Chấm lại từng repo và từng cặp](../../benchmarks/results/auto-oas-gt-plus-audit.json)
- microSecEnD chấm 119 project/path trên 17 repo sau khi gộp 151 service/path. Jasmine chấm 6 đích DI và 8 advice AOP, dùng route có sẵn làm đầu vào. 25 mục ngoài JCG gồm 24 dự án và 1 repo fixture. [microSecEnD](https://doi.org/10.1109/MSR59073.2023.00030) · [Mở rộng endpoint](https://doi.org/10.1007/s10664-025-10686-2) · [Jasmine](https://doi.org/10.1145/3551349.3556910)
- B3 ghi 20.713 cạnh CALL có tọa độ trên 700 graph cùng entry so với 17.879 cạnh ở B2. Graph bị cắt tăng 4→27. B4 xuất CFG cho 4.275/4.807 method Java có thân do parser báo cáo, 531 ngoài phạm vi, 1 chạm giới hạn. Đây là số đếm đầu ra và độ bao phủ kỹ thuật. [B3](../../benchmarks/results/public-b3-summary.json) · [B4 theo dự án](../../benchmarks/results/public-b4-summary.json)
- Fixture Java đạt 3/10→10/10 qua B0/B1 và B4 chạy lại 10/10. Spring đạt 5/15→15/15 qua B1/B2 và B4 chạy lại 15/15. Vị trí gọi đạt 2/7 ở B2, 7/7 ở B3 và B4. CFG đạt 0/8 ở B3 do chưa xuất, 8/8 ở B4 gồm 6 ca có dữ liệu, 2 ca báo đúng ngoài phạm vi. C# nội bộ có (TP, FP, FN) là (5, 2, 3) ở B0 và (7, 2, 1) ở B4, F1 66,7%→82,4%, ca chọn handler 0/1 ở cả hai. [Fixture chạy lại B4](../../benchmarks/results/b4-report-audit.json) · [Fixture CFG](../../benchmarks/results/java-method-flow-b3-b4.json)
- Sport Center cùng snapshot 79f3abbaf723e47adb2835534a33543cc157045e và 176 file Java, cả hai có cùng 91 cặp verb/path và handler. Lượt cạnh CALL qua graph 236→111, lần gọi có đích trong repo 234→128, graph không có CALL 1→18. B4 có 150 CFG và 514 quan hệ gán–đọc, javac báo 2.889 lỗi với classpath chỉ có JDK. Các số không phân biệt mất cạnh đúng với bỏ cạnh sai. [Lượt quan sát local](../../benchmarks/local/sport-center/20261125T123802Z/report.md)

## Gap và hướng mở rộng

| Gap | Hướng nghiên cứu |
|---|---|
| Nhãn lệch sang Java/Spring và đã xem trong phát triển | Chốt holdout theo họ dự án, kiểm tra fork và trùng repo, ưu tiên nhãn công bố. Ghi lịch sử đã xem dữ liệu. [Dwork et al.](https://pubmed.ncbi.nlm.nih.gov/26250683/) |
| Chưa chấm đúng handler, cạnh theo call site, nhánh và def-use trên dự án thật | Chốt nhãn ở đúng cấp file, dòng/cột và định danh. Ghi required, prohibited và chưa xác minh. Trace chỉ phản ánh đường chạy được quan sát. [Judge](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf) · [Jasmine](https://doi.org/10.1145/3551349.3556910) |
| Javac thiếu dependency, DI/AOP do framework tạo | B6 là ứng viên nếu thực nghiệm classpath cho thấy tác động đáng kể. So cùng mã PACE với JDK-only và dependency đã ghim, báo lỗi phân giải theo repo rồi chấm trên nhãn. Mô hình DI/AOP cần đo riêng. [Jasmine](https://doi.org/10.1145/3551349.3556910) |
| B4 là CFG nội hàm từng phần, chưa xử lý heap và luồng liên thủ tục | Chỉ mở rộng sau khi có nhãn và xác định ca thực sự ảnh hưởng câu hỏi. [Reps et al., POPL 1995](https://doi.org/10.1145/199448.199462) |
| Câu đúng dẫn chứng có thể kém tự nhiên hoặc quá dễ | B7 là ứng viên nếu chấm người xác nhận gap này. Có thể thử diễn đạt lại bằng cùng model và giữ nguyên đáp án kiểm tra được. [Learning to Ask, ACL 2017](https://aclanthology.org/P17-1123/) |
| Một lượt thời gian chính mỗi mốc | Đo lặp theo cặp cùng máy, ghi warm/cold, JVM, cache, thời gian từng pha và bộ nhớ đỉnh. Ít lượt thì báo từng lần và khoảng quan sát. [BESS](https://doi.org/10.1145/3463274.3463361) |
