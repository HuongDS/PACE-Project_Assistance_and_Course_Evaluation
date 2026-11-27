# PACE · Báo cáo kỹ thuật và bàn giao tích hợp B0–B4

## Tóm tắt

**Phạm vi bằng chứng.** Cả bốn bộ công khai (JCG, AutoOAS Spring, microSecEnD và Spring Jasmine) đã được xem trong quá trình phát triển và dùng để đối chiếu các mốc. Kết quả trong báo cáo là kết quả trên tập phát triển. PACE chưa có kết quả trên tập kiểm chứng độc lập để kết luận mức cải thiện trên repo chưa từng xem. Việc ghim artifact và chạy lại cùng cấu hình giúp tái lập kết quả trong phạm vi đã đo. [Dwork et al., *The Reusable Holdout*, Science 2015](https://pubmed.ncbi.nlm.nih.gov/26250683/)

PACE dựng sơ đồ nghiệp vụ từ mã nguồn bằng cách tìm hàm xử lý yêu cầu HTTP rồi lần theo các lời gọi từ đó. B0 là mã sản phẩm gốc. B1 phân giải lời gọi Java bằng thông tin kiểu và chữ ký của trình biên dịch. B2 mở rộng việc đọc ánh xạ Spring. B3 giữ từng vị trí gọi và ghi lý do graph bị cắt. B4 bổ sung CFG và luồng gán–đọc trong một hàm Java. JCG tăng từ **56/81 lên 68/81** ở B1 và giữ nguyên qua B4. Chấm lại đầu ra đã lưu theo bảng nhãn endpoint rộng hơn của AutoOAS cho kết quả **293/551 ở B0 và B1, 488/551 ở B2 đến B4**. B2 có **203 cặp mới khớp và 8 cặp từng khớp bị mất**. Tác động đến chất lượng câu hỏi vấn đáp chưa được đo.

## 1. Repo và trạng thái Git

Repo [HuongDS/PACE-Project_Assistance_and_Course_Evaluation](https://github.com/HuongDS/PACE-Project_Assistance_and_Course_Evaluation), nhánh local `chore/refactor-graph`. Trạng thái dưới đây được kiểm tra ngày 25/11/2026.

| Mốc | Mã nguồn và đối chứng |
|---|---|
| B0 sản phẩm | `22351f5b3cc0fdc6d17602dfbb35063d9b73b76f` |
| Checkpoint trước B1 | Tag `pace-graph-pre-b1` tại `116444cc09fb3779593f9065d3ac79c25fdfa2f2`. Có runner và báo cáo B0. |
| B1 | Tag `pace-graph-b1` tại `1d98fd714c593268e3bd9456d107416b8d244fd3`. Lượt B1 chạy trước commit trên mã sản phẩm đang sửa, được nhận diện bằng fingerprint trong báo cáo. |
| Đối chứng B1 lưu trong Git | `2609c31a39563e1f6b9e9934dd580912b8e5730f` hoàn thiện [thư mục đối chứng B1](../benchmarks/results/b1/). |
| B2 | Tag `pace-graph-b2` tại `cab54ca0d12a48d3149cd28b982840821ddea64a`. Fingerprint mã sản phẩm trong tag khớp lượt `20261125T053903128722Z-B2`. |
| B3 | B3 được đo trên working tree trước khi sửa B4. Fingerprint `dbe66a9858956d6bbffa96f6f6f5f698440e331a7121d7a8481b6a1597833872` khớp lượt `20261125T061825691798Z-B3`. |
| B4 | Đo trên working tree dựa trên commit B2, có cả thay đổi B3 và B4. Fingerprint và ID lượt đo nằm trong [tóm tắt B4](../benchmarks/results/public-b4-summary.json). |

Trường `pace_commit` ghi HEAD lúc chạy. Lượt B1–B4 đều có `production_changes: true`. Dùng `production_source_sha256` để nhận diện mã đã đo trong bốn thư mục `PACE.Application`, `PACE.Infrastructure`, `PACE.Domain` và `PACE.API`. Rà soát đã tính lại fingerprint từ tag cho B0–B2 và từ working tree cho B3. Nhánh nghiên cứu song song của người cộng tác chưa có SHA để đối chiếu.

## 2. Phương pháp và phạm vi triển khai

Đường chạy chính của sản phẩm là `RepositoryScanner → JavaTreeSitterParser → EntryPointMatcher → FlowBuilder/CallResolver → Mermaid và dữ liệu lưu`. `BusinessTemplateGenerator` dùng kết quả phân tích để tạo danh sách endpoint đầu vào. Các benchmark gọi lại parser, matcher hoặc resolver của sản phẩm tùy phép đo.

| Mốc | Thay đổi trong mã sản phẩm |
|---|---|
| B0 | Resolver dùng tên class đơn giản, so tên method không phân biệt hoa thường, lọc theo số đối số khi có nhiều ứng viên và có suy đoán implementation theo tên. Java parser đọc được một path và một verb đại diện. Matcher ưu tiên tên class/method, có fallback hậu tố path và có thể chọn ứng viên đầu tiên khi hòa. |
| B1 | Giữ Tree-sitter để đọc source. `JavacTask.parse()/analyze()` lấy khai báo được chọn và các đích ảo có thể xuất hiện trong source theo phân cấp kiểu. Khóa Java gồm tên binary của class, tên method và kiểu tham số sau type erasure, tức chữ ký dùng để nhận diện method sau khi lược thông tin generic. Gắn kết quả vào vị trí lời gọi và dùng khóa này trong `CallResolver`. Khi thiếu bằng chứng, ghi cảnh báo và để ngỏ cạnh. |
| B2 | Đọc annotation Spring chuẩn và các tổ hợp path/verb cấp class và method, gồm alias và một số mapping kế thừa qua override trong source. Java matcher ưu tiên path và verb, ưu tiên verb cụ thể hơn `ANY` và giữ phân biệt hoa thường của path. Khi nhiều ứng viên, có thể dùng hint class/method chính xác. Ca còn mơ hồ được để ngỏ. Khi có thông tin symbol, controller trong template sinh tự động dùng tên đầy đủ theo package. |
| B3 | Giữ từng cặp vị trí gọi và method đích, lưu dòng/cột và lý do graph chạm giới hạn. Thứ tự bước là thứ tự duyệt graph tĩnh. |
| B4 | Dựng CFG cấp câu lệnh và quan hệ gán–đọc của tham số, biến cục bộ trong hàm Java. Lưu `javaFlow` cùng method và trả qua API chi tiết. |

B1 áp dụng quy tắc phân giải của Java và nguyên tắc tách khai báo lúc biên dịch khỏi tập đích lúc chạy. B2 áp dụng semantics của mapping Spring ở phạm vi được hỗ trợ. Dữ liệu tham chiếu nằm ở runner và bộ chấm. Sản phẩm không đọc nhãn JCG/AutoOAS để chọn method hoặc route. Các ca kiểm thử nội bộ dùng để xác nhận lỗi đã nhắm tới, vì vậy cần báo riêng với bốn bộ công khai. [Java Language Specification §15.12](https://docs.oracle.com/javase/specs/jls/se21/html/jls-15.html#jls-15.12) · [Unimocg, ISSTA 2024](https://doi.org/10.1145/3650212.3652109) · [Spring Mapping Requests](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-controller/ann-requestmapping.html)

## 3. Benchmark, repo và đơn vị chấm

Bộ chính gồm bốn nguồn công khai dưới đây. Dữ liệu bài nộp sinh viên và PetClinic REST thử nghiệm đã ra khỏi bộ chính. Repo `spring-petclinic-microservices` thuộc tập microSecEnD công khai.

| Nguồn | Snapshot/repo | Đơn vị chấm của PACE |
|---|---|---|
| JCG / Judge | [opalj/JCG](https://github.com/opalj/JCG) tại `4737aac8c2652acded1c4505961b6af52d06ceeb`. 109 chương trình Java, 117 annotation nhập từ upstream. | 79 annotation đủ điều kiện trong 74 chương trình. Chấm đích gọi trực tiếp theo class sở hữu và tên method, với 81 đích bắt buộc và 11 đích bị cấm. Chấm trên source và projection của PACE. Phép đo bytecode của tác giả là một protocol khác. [Paper Judge, ISSTA 2019](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf) |
| AutoOAS Spring | [Artifact v2](https://zenodo.org/records/20962871), MD5 `460e47f4108b2b8e663376de467bd08c`. 7 dự án Spring gồm proxyprint-kitchen, ocvn, ur-codebin-api, ohsome-api, catwatch, quartz-manager và cwa-verification-server. | Phép đo cũ dùng 61 khóa verb + path từ 8 spec từng phần. Phép chấm lại dùng 551 khóa từ 8 sheet trong `GT-plus.xlsx`. Cả hai gộp profile CWA ở cấp dự án, chấm verb + path và chưa chấm handler. [Paper AutoOAS](https://www.sciencedirect.com/science/article/pii/S0164121226003559) |
| microSecEnD | [Nhãn](https://github.com/tuhh-softsec/microSecEnD) tại `63653e09177b6d757eada4521f8bace8a8c20766` và [manifest repo nguồn](https://github.com/M3SOulu/EMSE2025SAR-Benchmarks) tại `bc21234b64f8cf55a181fe8d86a3632fe1234eec`. | 151 cặp service + path nếu tính riêng từng service trong từng project, còn 119 khóa project + path sau khi bỏ phân biệt service. Chấm trên 17 repo. [Paper bộ dữ liệu, MSR 2023](https://doi.org/10.1109/MSR59073.2023.00030) · [Phần mở rộng nhãn endpoint, EMSE 2025](https://doi.org/10.1007/s10664-025-10686-2) |
| Spring Jasmine | [Spring Micro-Benchmarks](https://github.com/SpringJasmine/Spring-micro-benchmarks) tại `79ad187a6f7ca07cfc6b874b05f5972ba9d64132`. | Nhãn dẫn xuất từ source upstream cho tập con do PACE chọn. 6 kiểm tra cạnh DI ở 6 route và 8 kiểm tra sự xuất hiện của advice AOP ở 3 route. Runner cung cấp verb/path để tìm graph cho các ca này. [Paper Jasmine, ASE ’22](https://doi.org/10.1145/3551349.3556910) |

Danh sách 25 mục ngoài JCG gồm **24 dự án và 1 repo fixture Jasmine**. Từng SHA của 17 repo microSecEnD và nhãn theo project được lưu trong [manifest oracle](../benchmarks/results/b1/research-oracles.json). Mọi snapshot AutoOAS nằm trong ZIP phát hành và dùng chung SHA-256 của ZIP làm `pin`.

**Giữ nguyên đầu vào.** B0–B3 có SHA-256 JSON đầu vào JCG `43690cb599334c2e02aaffea4b884ec0a1da150138b393c9925e133e3a8a62e3` và SHA-256 manifest nhãn và pin nguồn `ac3402c03585ae7c654c3ea008e3dabbd52ee8b560119dcb9271e69ed1e9c6d6`. Cả hai hash đã được tạo lại từ artifact đã ghim và khớp bốn lượt. Harness có fingerprint khác nhau qua các mốc. Rà soát kiểm tra lại danh tính ca và nhãn. JCG có cùng 79 annotation trong phạm vi chấm trên cả bốn mốc, gồm 74 dòng chấm được và 5 dòng không tìm thấy caller vẫn tính vào mẫu số. 34 dòng nằm ngoài phạm vi phép chiếu và 4 dòng mơ hồ bị loại.

## 4. Kết quả

| Phép đo trên tập phát triển | B0 | B1 | B2 | B3 | B4 |
|---|---:|---:|---:|---:|---:|
| JCG · đích bắt buộc tìm được | 56/81 (69,14%) | 68/81 (83,95%) | 68/81 (83,95%) | 68/81 (83,95%) | 68/81 (83,95%) |
| JCG · đích bị cấm xuất hiện | 3/11 | 1/11 | 1/11 | 1/11 | 1/11 |
| AutoOAS · khớp 61 khóa trong spec từng phần | 30/61 | 30/61 | 35/61 | 35/61 | 35/61 |
| AutoOAS · ngoài nhãn của spec từng phần | 279 | 279 | 467 | 467 | 467 |
| AutoOAS · chấm lại với GT+ rộng hơn | 293/551 (53,18%) | 293/551 (53,18%) | 488/551 (88,57%) | 488/551 (88,57%) | 488/551 (88,57%) |
| AutoOAS · ngoài nhãn GT+ | 16 | 16 | 14 | 14 | 14 |
| microSecEnD · project + path khớp nhãn | 97/119 (81,51%) | 97/119 (81,51%) | 97/119 (81,51%) | 97/119 (81,51%) | 97/119 (81,51%) |
| microSecEnD · báo ngoài nhãn | 4 | 4 | 4 | 4 | 4 |
| Jasmine · đích DI / advice AOP | 6/6 · 0/8 | 6/6 · 0/8 | 6/6 · 0/8 | 6/6 · 0/8 | 6/6 · 0/8 |
| Tổng thời gian phân tích, 25 mục ngoài JCG | 2,469 s | 71,826 s | 74,118 s | 74,455 s | 72,513 s |
| Trung vị thời gian mỗi mục | 0,022 s | 2,274 s | 2,397 s | 2,391 s | 2,362 s |
| Mục phân tích thành công ngoài JCG | 25/25 | 25/25 | 25/25 | 25/25 | 25/25 |
| Ca đã dựng graph ngoài JCG | 518 | 518 | 700 | 700 | 700 |
| Route phát hiện được nhưng chưa ghép handler | 0/509 | 0/509 | 1/692 | 1/692 | 1/692 |

### Kiểm thử nội bộ

| Phép đo | B0 | B1 | B2 | B3 | B4 |
|---|---:|---:|---:|---:|---:|
| Java · ca có đúng tập chữ ký đích | 3/10 | 10/10 | — | — | 10/10 |
| Java · đích đúng / dư / thiếu theo chữ ký | 7 / 9 / 1 | 8 / 0 / 0 | — | — | 8 / 0 / 0 |
| Spring · ca route và handler khớp kỳ vọng | — | 5/15 | 15/15 | — | 15/15 |
| Graph · ca vị trí gọi, đệ quy và giới hạn đạt | — | — | 2/7 | 7/7 | 7/7 |
| Java · ca CFG và gán–đọc khớp kỳ vọng | — | — | — | 0/8 | 8/8 |
| Java · ca có graph và gán–đọc đúng kỳ vọng | — | — | — | 0/6 | 6/6 |
| Java · ca báo đúng ngoài phạm vi | — | — | — | 0/2 | 2/2 |
| C# · cạnh CALL, TP / FP / FN | 5 / 2 / 3 | — | — | — | 7 / 2 / 1 |
| C# · precision / recall / F1 trên fixture | 71,4% / 62,5% / 66,7% | — | — | — | 77,8% / 87,5% / 82,4% |
| C# · ca chọn handler khi route mâu thuẫn tên | 0/1 | — | — | — | 0/1 |

`—` là chưa có kết quả từng ca được lưu cho phép đo ở mốc đó. Giá trị 0/8 của B3 nghĩa là chưa có đầu ra CFG. Trên fixture C#, B4 ghi đủ hai lần gọi trong ca lặp và thêm cạnh đệ quy trực tiếp. Hai đích dư vẫn xuất hiện trong ca overload và trùng tên khác namespace. Lời gọi accessor ngắn vẫn thiếu. [Fixture chữ ký](../benchmarks/results/java-signatures-b0-b1.json) · [Fixture route/handler](../benchmarks/results/spring-routes-b1-b2.json) · [Fixture B3](../benchmarks/results/graph-call-sites-b2-b3.json) · [Fixture CFG](../benchmarks/results/java-method-flow-b3-b4.json) · [Lần chạy bổ sung B4](../benchmarks/results/b4-report-audit.json) · [Đối chứng C# B0](graph-benchmark.md#baseline-c-ngoại-tuyến)

### Chỉ số cần kiểm chứng thêm cho paper

| Tiêu chí | PACE hiện đo được | Dữ liệu hoặc phép đo còn thiếu |
|---|---|---|
| Endpoint và handler Spring | Khớp verb/path với AutoOAS và khớp handler trên fixture | Repo có nhãn handler đích, xác minh các route ngoài nhãn và giữ riêng profile khi chấm runtime |
| Độ đúng cạnh gọi theo vị trí, CFG và gán–đọc | Fixture nội bộ, số đếm đầu ra và độ bao phủ trên tập phát triển | Bộ nhãn độc lập theo dòng/cột, có ca nhánh, đệ quy và các cạnh sai để tính precision/recall/F1 nếu nhãn đủ |
| Khả năng tổng quát và đóng góp từng bước | So sánh tuần tự B0–B4 trên bốn bộ đã xem | Tập repo giữ lại, kết quả theo repo và phép loại bỏ từng thành phần nếu muốn quy mức tăng cho riêng nó |
| Hiệu năng | Tổng và trung vị thời gian từ một lượt mỗi bản | Đo lặp cùng điều kiện, độ biến thiên, p95 nếu đủ lượt, bộ nhớ đỉnh và thời gian riêng của javac/CFG |
| Chất lượng câu hỏi vấn đáp | Chưa đo | Tỷ lệ câu hỏi hợp lệ và sai tiền đề, đáp án đối chiếu được với code và vị trí dẫn chứng, cùng model và số câu yêu cầu |

Các phép đo và yêu cầu đối với tập giữ lại được mô tả trong [kế hoạch đánh giá](graph-benchmark.md#dữ-liệu-đánh-giá-và-cách-kết-luận). [Judge](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf) · [The Reusable Holdout](https://pubmed.ncbi.nlm.nih.gov/26250683/) · [Code-QA-Bench, preprint](https://arxiv.org/abs/2605.29277)

**JCG.** B1 tìm thêm 12 đích bắt buộc, giữ đủ 56 đích đã tìm được ở B0 và loại 2 đích bị cấm từng xuất hiện. Mức tăng là **+14,81 điểm phần trăm**. Bootstrap ghép cặp 95% trên tập JCG đã chọn cho khoảng **+7,41 đến +23,38 điểm phần trăm**, lấy mẫu lại theo chương trình 10.000 lần. Đầu ra từng annotation giống nhau ở B1, B2 và B3. Các đích ngoài nhãn bắt buộc và bị cấm chưa được xác minh để tính precision/F1 toàn bộ graph.

**AutoOAS.** 61 khóa lịch sử là mẫu OpenAPI từng phần được tác giả biên soạn, gồm khoảng 10 operation mỗi dự án hoặc ít hơn. Cả 61 khóa này đều có trong bảng GT+ sau cùng quy tắc chuẩn hóa. Áp dụng cùng quy tắc cho bảng GT+ và đầu ra B0–B3 đã lưu cho B2 tăng ròng **195/551**, tương đương **+35,39 điểm phần trăm**. Các phép chấm gộp hai profile CWA, giữ phân biệt hoa thường, chuẩn hóa tên path parameter và chỉ lấy các verb HTTP cụ thể. PACE so với path được xuất trực tiếp, chưa đối chiếu cấu hình runtime hoặc áp dụng phép ghép OpenAPI server URL trong script của tác giả. Các khóa ngoài nhãn cần xác minh trước khi gọi là lần phát hiện sai. Cả hai phép đo đều dùng cùng bảy dự án đã xem và chưa đánh giá đích handler. [Artifact AutoOAS v2](https://zenodo.org/records/20962871) · [Kết quả chấm lại từng dự án và các ca đổi trạng thái](../benchmarks/results/auto-oas-gt-plus-audit.json)

Phần tăng tập trung theo dự án và HTTP verb. Trong 203 khóa GT+ mới khớp, 123 thuộc OCVN, 79 thuộc ohsome-api và 1 thuộc ProxyPrint. Có 202 khóa POST và 1 khóa OPTIONS. Cả 8 khóa mất khớp đều là GET. Do vậy mức tăng gộp theo số khóa chưa cho biết mức tăng điển hình trên mỗi dự án.

| Repo AutoOAS, spec từng phần | Mẫu số | Khớp B1 → B2 | Ngoài nhãn B1 → B2 |
|---|---:|---:|---:|
| proxyprint-kitchen | 10 | 8 → 8 | 58 → 59 |
| ocvn | 10 | 2 → 5 | 133 → 255 |
| ur-codebin-api | 6 | 6 → 6 | 0 → 0 |
| ohsome-api | 10 | 6 → 10 | 74 → 149 |
| catwatch | 10 | 7 → 5 | 4 → 2 |
| quartz-manager | 10 | 1 → 1 | 9 → 2 |
| cwa-verification-server | 5 | 0 → 0 | 1 → 0 |

Hai khóa từng khớp bị mất trong spec từng phần đều ở Catwatch, `GET /contributors` và `GET /languages`. Bảng GT+ thêm một khóa mất khớp ở Catwatch (`GET /projects`) và năm khóa ở OCVN. Source Catwatch gắn prefix cấp class bằng `Constants.API_RESOURCE_STATISTICS`, được khai báo là `"/statistics"`. B0/B1 bỏ phần prefix biểu thức và báo path con. B2 bỏ ngỏ mapping có prefix chưa phân giải. Đây là mất khớp với nhãn. Route thực tế của các khóa này chưa được kiểm chứng bằng chạy ứng dụng. [Artifact AutoOAS v2](https://zenodo.org/records/20962871) · [Danh sách tám khóa mất khớp](../benchmarks/results/auto-oas-gt-plus-audit.json)

**Graph B3.** Hai lượt B2/B3 có 691 ca discovery đã resolve và 9 ca Jasmine được cung cấp verb/path đã resolve, với cùng entry theo file/dòng. Còn 1 ca discovery không resolve ở cả hai và nằm ngoài phép đếm này. Trong 700 đầu ra đã resolve, cạnh CALL ghi nhận tăng **17.879 → 20.713**, trong đó **20.713/20.713** cạnh B3 có tọa độ. Graph báo bị cắt tăng **4 → 27**. Cả 27 đầu ra B3 bị cắt đều chạm giới hạn số bước theo cấu hình tối đa 250 bước và độ sâu 12. Đây là số đếm đầu ra theo ca. Tập này chưa có nhãn độc lập để tính độ chính xác của từng cạnh. [Báo cáo B3](../benchmarks/results/public-b3-summary.json)

**Chi phí và phạm vi.** Hàng thời gian dùng một lượt lịch sử mỗi phiên bản. Tỉ số B1/B0 là **29,09 lần** cho tổng thời gian trên 25 mục ngoài JCG. Đồng hồ đo từ quét source đến dựng và render Mermaid, gồm thời gian Java/javac ở B1–B3. B2/B3 còn dựng nhiều graph hơn B0/B1, với 700 so với 518 ca đã resolve. Các số này chưa cô lập chi phí javac, chưa đo độ biến động bằng nhiều lượt và chưa đo độ trễ production. Cấu hình B1–B3 dùng .NET SDK `9.0.124`, Python `3.12.3` và OpenJDK `21.0.13.1` với classpath chỉ có JDK. Cả 25 mục có lỗi javac được ghi ở mỗi lượt B1–B3, dù runner trả `ok`. Một số binding có thể thiếu vì dependency hoặc generated source. B2 giới hạn 4.096 tổ hợp annotation mỗi handler và còn thiếu hỗ trợ đầy đủ cho path từ hằng/cấu hình, custom annotation, route đăng ký bằng code và dispatch do proxy Spring. [Paper Jasmine](https://doi.org/10.1145/3551349.3556910)

## 5. Ranh giới tích hợp với nhánh song song

| Vùng dễ chồng thay đổi | Hợp đồng cần kiểm tra khi ghép |
|---|---|
| [ParsedModels](../PACE.Application/Analysis/Models/ParsedModels.cs), [JavaTreeSitterParser](../PACE.Infrastructure/Analysis/Parsing/JavaTreeSitterParser.cs), [JavaCompilerBindings](../PACE.Infrastructure/Analysis/Parsing/JavaCompilerBindings.cs), [helper javac](../PACE.Infrastructure/Analysis/Parsing/Java/PaceJavaSymbols.java) | C# và Java trao đổi declaration, call binding và override bằng vị trí trong cùng source. Thay đổi offset, encoding, khóa method hoặc định dạng output của helper cần sửa đều hai đầu. Các trường cần giữ tương thích gồm `JavaSymbol`, `JavaBinding`, `JavaOverriddenSymbols` và `SpringEndpoints`. |
| [CallResolver](../PACE.Application/Analysis/Flow/CallResolver.cs), [FlowBuilder](../PACE.Application/Analysis/Flow/FlowBuilder.cs), [MermaidBuilder](../PACE.Application/Analysis/Flow/MermaidBuilder.cs) | Tách danh tính method, vị trí gọi và nhãn hiển thị. `ParsedMethod.Id` vẫn là GUID theo lần phân tích. B3 giữ từng vị trí gọi. Cạnh RETURN được dựng từ kiểu trả về. Nếu đổi mô hình luồng dữ liệu, kiểm tra cùng lúc cả graph và phần render. |
| [JavaSpringMappings](../PACE.Infrastructure/Analysis/Parsing/JavaSpringMappings.cs), [EntryPointMatcher](../PACE.Application/Analysis/Flow/EntryPointMatcher.cs), [BusinessTemplateGenerator](../PACE.Application/Analysis/Template/BusinessTemplateGenerator.cs) | Kiểm tra template cũ dùng tên controller ngắn và template B2 có tên đầy đủ. Giữ test nhiều verb/path, ánh xạ kế thừa, route trùng và handler chưa xác định. Các alias chung một handler có thể được gom thành một flow trong cùng nghiệp vụ ở service lưu graph. |
| [RepoAnalysisService](../PACE.Application/Analysis/RepoAnalysisService.cs), [DataFlowEdge](../PACE.Domain/Entities/DataFlowEdge.cs), [BusinessFlow](../PACE.Domain/Entities/BusinessFlow.cs) | B3 đã thêm migration cho vị trí gọi và lý do graph bị cắt. Tọa độ và lý do ở bản ghi cũ có thể null. Kiểm tra đường ghi, đọc, render và dữ liệu cũ cùng lúc khi tích hợp. |
| [Dockerfile](../Dockerfile), [runner](../benchmarks/run.py) | Dockerfile của B1 thêm JDK 17. Runner benchmark bỏ `PACE_JAVA_CLASSPATH` từ môi trường để giữ cấu hình JDK-only. Nếu nhánh kia đổi JDK, tải dependency, cache, hoặc sửa phép chấm, ghi phiên bản và đo lại hai phía trên cùng cấu hình trước khi so kết quả. |

Quy trình tích hợp đề xuất:

1. Ghi SHA của nhánh song song, chốt snapshot PACE cần ghép rồi tìm commit chung bằng `git merge-base`. Kiểm tra diff của mỗi nhánh từ commit chung, tập trung vào các hợp đồng trên.
2. Tích hợp trên nhánh riêng. Kiểm tra luồng đầy đủ **tìm endpoint → chọn handler → dựng graph → lưu/đọc → render**, gồm template đã tồn tại và template mới. Chạy regression Java/Spring/C#/TypeScript.
3. Chạy bốn bộ công khai trên từng nhánh và bản ghép với cùng đầu vào. Gắn kết quả bản ghép với SHA mới. Giữ báo cáo từng ca để phát hiện việc mất cạnh hoặc endpoint dù tổng điểm giữ nguyên. Nếu cả hai nhánh thay cùng một khâu, đo riêng từng thay đổi trước khi gán đóng góp cho nó.

## 6. Chạy lại và hướng sau B4

Đặt các artifact theo [hướng dẫn benchmark](../benchmarks/README.md). Chạy từ gốc repo. Đo B0 trên một checkout của `pace-graph-pre-b1`. Đo B2 trên source B2. Có thể thêm `--no-restore` khi .NET dependencies đã được restore.

```bash
python3 benchmarks/run.py --stage B0
python3 benchmarks/run.py --stage B2 --compare benchmarks/results/b1
dotnet test PACE.Tests/PACE.Tests.csproj --filter FullyQualifiedName~SpringRouteBenchmarkTests
```

Runner ghi metadata, log, TRX, JSON thô, CSV từng ca và delta trong `benchmarks/local/runs/<UTC>-<stage>/`. Mốc nguồn là `20261125T044805621095Z-B0`, `20261125T051319684835Z-B1`, `20261125T053903128722Z-B2` và `20261125T061825691798Z-B3`. Toàn bộ log thô nằm ở workspace local. B1 có bộ file tối thiểu để đối chiếu đã lưu trong Git.

**Rà soát ngày 25/11/2026.** Đã tính lại số từ raw B0–B3, tái tạo đầu vào đã ghim, kiểm tra fingerprint mã sản phẩm và chạy lại B3 trên cùng đầu vào tại `20261125T091516524988Z-B3Audit`. Lượt chạy lại cho cùng tóm tắt, cùng điểm từng ca và cùng nội dung graph. Tổng thời gian 25 mục là **75,736 s**, trung vị **2,396 s**. Fingerprint sản phẩm trùng lượt B3 gốc. Fingerprint harness toàn cây khác và bao gồm cả file tài liệu, kết quả. [Tóm tắt B0](../benchmarks/results/public-b0-summary.json) · [B1](../benchmarks/results/public-b1-summary.json) · [B2](../benchmarks/results/public-b2-summary.json) · [B3](../benchmarks/results/public-b3-summary.json)

**B4.** Đo lại trên cùng đầu vào cho cùng điểm từng ca và graph lời gọi như B3, kể cả 488/551 cặp AutoOAS GT+. Thời gian phân tích 25 mục trong một lượt là **72,513 s**, trung vị **2,362 s**. Lượt B3 gốc là 74,455 s, lượt B3 chạy lại là 75,736 s. Chưa đủ số lượt để kết luận chênh lệch hiệu năng ổn định. Fixture CFG/def-use chốt trước khi sửa cho kết quả B3 **0/8 có đầu ra khớp kỳ vọng** và B4 **8/8**, gồm 6 ca có graph và 2 ca báo đúng `Unsupported`. Trên bộ công khai, B4 xuất CFG cho **4.275/4.807 method Java có thân hàm**, 531 method ngoài phạm vi và 1 method chạm giới hạn. Độ đúng CFG trên repo thực tế chưa có bộ nhãn tương ứng. [Tóm tắt B4](../benchmarks/results/public-b4-summary.json) · [Fixture B3/B4](../benchmarks/results/java-method-flow-b3-b4.json) · [Cách chạy và cơ sở phương pháp](../benchmarks/README.md)

**B5.** Mode Evidence tạo đáp án có cấu trúc từ CFG và tọa độ mã trước khi diễn đạt câu hỏi theo mẫu. Xem kết quả fixture, lượt chạy lại bốn bộ và quy trình đối chứng chất lượng câu hỏi trong hồ sơ nghiên cứu. [Hồ sơ nghiên cứu B0–B5](paper-pace-graph/README.md) · [Code-QA-Bench, preprint](https://arxiv.org/abs/2605.29277)

## Phụ lục · Snapshot 17 repo microSecEnD

| Repo nguồn | Commit nguồn |
|---|---|
| `anilallewar/microservices-basics-spring-boot` | `c1b9dc9d6f0346befa04db1b73d9099f5c397d70` |
| `apssouza22/java-microservice` | `69bee7a254c7de565ee3c02d4e5103b6b1b52416` |
| `callistaenterprise/blog-microservices` | `960d822456b1b4475091c0719f1a1c4e63014117` |
| `ewolff/microservice` | `84f12d9223e468306e2c0b1243fa803583cba3f0` |
| `ewolff/microservice-kafka` | `feb567053febae3dc73556f2fc87c424d1e98c35` |
| `fernandoabcampos/spring-netflix-oss-microservices` | `cd4c9376069a0ef802859679af76db64656aa6fa` |
| `georgwittberger/apache-spring-boot-microservice-example` | `7b32618ade84dd75190c3cad6063b6f82fd5dc99` |
| `jferrater/tap-and-eat-microservices` | `458728ab1ff2055da348a478ff150c6132934b0f` |
| `koushikkothagal/spring-boot-microservices-workshop` | `672d73a3b71aed4584d516ac8a102a5a2b12c245` |
| `mdeket/spring-cloud-movie-recommendation` | `7d09c7c5c141fb6a028580868a160ff464f06d9c` |
| `mudigal-technologies/microservices-sample` | `e8b734c324b55854610ee2bbabbd2abcb6c669ad` |
| `piomin/sample-spring-oauth2-microservices` | `f15b2f3bca718f9919a2e994eb6b5df310616de2` |
| `rohitghatol/spring-boot-microservices` | `46db8e19564ae20cbc2c8fab0325909c37fa5b74` |
| `shabbirdwd53/springboot-microservice` | `c4aaaeda349f904417ee2cf2f43aff980052229c` |
| `spring-petclinic/spring-petclinic-microservices` | `090b56aa3926e7d99d475257d1e8e156cc4b14a0` |
| `sqshq/piggymetrics` | `7633844d48c575237a2f6039e63d3088288ad929` |
| `yidongnan/spring-cloud-netflix-example` | `44724b94158c2fba22d20c16331506aa20f35713` |
