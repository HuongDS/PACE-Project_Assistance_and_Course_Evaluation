# PACE · Báo cáo kỹ thuật và bàn giao tích hợp B0–B2

## Tóm tắt

PACE dựng sơ đồ nghiệp vụ từ mã nguồn theo hai bước chính. Bước đầu tìm hàm xử lý yêu cầu HTTP. Bước sau lần theo các hàm được gọi từ đó. B0 là mã sản phẩm gốc. B1 thay cách phân giải lời gọi Java bằng thông tin kiểu và chữ ký của trình biên dịch. B2 mở rộng việc đọc ánh xạ Spring và chọn handler theo HTTP verb cùng path. Điểm JCG tăng từ **56/81 lên 68/81** ở B1 và giữ nguyên ở B2. AutoOAS Spring tăng từ **30/61 lên 35/61** ở B2, với **7 cặp khớp mới và 2 cặp từng khớp bị mất**. Thời gian phân tích quan sát trên 25 repo tăng từ **2,469 s lên 71,826 s ở B1 và 74,118 s ở B2**. Đây là các kết quả về graph và endpoint. Hiệu quả đối với câu hỏi vấn đáp chưa được đo.

## 1. Repo và trạng thái Git

Repo [HuongDS/PACE-Project_Assistance_and_Course_Evaluation](https://github.com/HuongDS/PACE-Project_Assistance_and_Course_Evaluation), nhánh local `chore/refactor-graph`. Trạng thái dưới đây được kiểm tra ngày 25/11/2026.

| Mốc | Mã nguồn và đối chứng |
|---|---|
| B0 sản phẩm | `22351f5b3cc0fdc6d17602dfbb35063d9b73b76f` |
| Checkpoint trước B1 | Tag `pace-graph-pre-b1` tại `116444cc09fb3779593f9065d3ac79c25fdfa2f2`. Có runner và báo cáo B0. |
| B1 | Tag `pace-graph-b1` tại `1d98fd714c593268e3bd9456d107416b8d244fd3`. Lượt B1 chạy trước commit trên mã sản phẩm đang sửa, được nhận diện bằng fingerprint trong báo cáo. |
| Đối chứng B1 lưu trong Git | `2609c31a39563e1f6b9e9934dd580912b8e5730f` hoàn thiện [thư mục đối chứng B1](../benchmarks/results/b1/), cũng là HEAD local khi ghi tài liệu. |
| B2 | Mã và báo cáo B2 đang trong working tree, gồm cả file chưa được Git theo dõi. Lượt đo cuối `20261125T053903128722Z-B2` có fingerprint mã sản phẩm `b0fb67501767d6d6ab8ba874a0cb2fd84cece68f9077f53bc4b45e654da164cd`, đã đối chiếu với mã đang có. |

Trường `pace_commit` trong metadata lượt B1/B2 ghi HEAD lúc chạy. Hai lượt đều có `production_changes: true`, nên khi truy xuất mã đã đo cần đọc thêm `production_source_sha256`. Chỉ chuyển nhánh hoặc gửi commit HEAD hiện tại sẽ bỏ sót B2. Nhánh nghiên cứu song song của người cộng tác chưa được cung cấp để đối chiếu.

## 2. Phương pháp và phạm vi triển khai

Đường chạy chính của sản phẩm là `RepositoryScanner → JavaTreeSitterParser → EntryPointMatcher → FlowBuilder/CallResolver → Mermaid và dữ liệu lưu`. `BusinessTemplateGenerator` dùng kết quả phân tích để tạo danh sách endpoint đầu vào. Các benchmark gọi lại parser, matcher hoặc resolver của sản phẩm tùy phép đo.

| Mốc | Thay đổi trong mã sản phẩm |
|---|---|
| B0 | Resolver dùng tên class đơn giản, so tên method không phân biệt hoa thường, lọc theo số đối số khi có nhiều ứng viên và có suy đoán implementation theo tên. Java parser đọc được một path và một verb đại diện. Matcher ưu tiên tên class/method, có fallback hậu tố path và có thể chọn ứng viên đầu tiên khi hòa. |
| B1 | Giữ Tree-sitter để đọc source. `JavacTask.parse()/analyze()` lấy khai báo được chọn và các đích ảo có thể xuất hiện trong source theo phân cấp kiểu. Khóa Java gồm tên binary của class, tên method và kiểu tham số sau type erasure, tức chữ ký dùng để nhận diện method sau khi lược thông tin generic. Gắn kết quả vào vị trí lời gọi và dùng khóa này trong `CallResolver`. Khi thiếu bằng chứng, ghi cảnh báo và để ngỏ cạnh. |
| B2 | Đọc annotation Spring chuẩn và các tổ hợp path/verb cấp class và method, gồm alias và một số mapping kế thừa qua override trong source. Java matcher ưu tiên path và verb, ưu tiên verb cụ thể hơn `ANY` và giữ phân biệt hoa thường của path. Khi nhiều ứng viên, có thể dùng hint class/method chính xác. Ca còn mơ hồ được để ngỏ. Khi có thông tin symbol, controller trong template sinh tự động dùng tên đầy đủ theo package. |

B1 áp dụng quy tắc phân giải của Java và nguyên tắc tách khai báo lúc biên dịch khỏi tập đích lúc chạy. B2 áp dụng semantics của mapping Spring ở phạm vi được hỗ trợ. Dữ liệu tham chiếu nằm ở runner và bộ chấm. Sản phẩm không đọc nhãn JCG/AutoOAS để chọn method hoặc route. Các ca kiểm thử nội bộ dùng để xác nhận lỗi đã nhắm tới, vì vậy cần báo riêng với bốn bộ công khai. [Java Language Specification §15.12](https://docs.oracle.com/javase/specs/jls/se21/html/jls-15.html#jls-15.12) · [Unimocg, ISSTA 2024](https://doi.org/10.1145/3650212.3652109) · [Spring Mapping Requests](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-controller/ann-requestmapping.html)

## 3. Benchmark, repo và đơn vị chấm

Bộ chính gồm bốn nguồn công khai dưới đây. Dữ liệu bài nộp sinh viên và PetClinic REST thử nghiệm đã ra khỏi bộ chính. Repo `spring-petclinic-microservices` thuộc tập microSecEnD công khai.

| Nguồn | Snapshot/repo | Đơn vị chấm của PACE |
|---|---|---|
| JCG / Judge | [opalj/JCG](https://github.com/opalj/JCG) tại `4737aac8c2652acded1c4505961b6af52d06ceeb`. 109 chương trình Java, 117 annotation nhập từ upstream. | 79 annotation đủ điều kiện trong 74 chương trình. Chấm đích gọi trực tiếp theo class sở hữu và tên method, với 81 đích bắt buộc và 11 đích bị cấm. Chấm trên source và projection của PACE. Phép đo bytecode của tác giả là một protocol khác. [Paper Judge, ISSTA 2019](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf) |
| AutoOAS Spring | [Artifact v2](https://zenodo.org/records/20962871), MD5 `460e47f4108b2b8e663376de467bd08c`. 7 repo gồm proxyprint-kitchen, ocvn, ur-codebin-api, ohsome-api, catwatch, quartz-manager và cwa-verification-server. | 61 khóa HTTP verb + path trong 8 spec sau khi hợp hai profile CWA ở cấp project. Không có cột đích handler trong phép đo này. [Paper AutoOAS](https://doi.org/10.1016/j.jss.2026.113122) |
| microSecEnD | [Nhãn](https://github.com/tuhh-softsec/microSecEnD) tại `63653e09177b6d757eada4521f8bace8a8c20766` và [manifest repo nguồn](https://github.com/M3SOulu/EMSE2025SAR-Benchmarks) tại `bc21234b64f8cf55a181fe8d86a3632fe1234eec`. | 119 khóa project + path trên 17 repo. Path trùng giữa các service trong cùng một project được tính một lần. [Paper EMSE 2025](https://doi.org/10.1007/s10664-025-10686-2) |
| Spring Jasmine | [Spring Micro-Benchmarks](https://github.com/SpringJasmine/Spring-micro-benchmarks) tại `79ad187a6f7ca07cfc6b874b05f5972ba9d64132`. | Tập con của PACE gồm 6 đích gọi qua DI và 8 ca kiểm tra advice AOP có xuất hiện trong graph. [Paper Jasmine, ASE 2022](https://doi.org/10.1145/3551349.3556910) |

Danh sách đủ 25 mục ngoài JCG, gồm từng SHA của 17 repo microSecEnD và nhãn theo project, có trong [manifest oracle đã lưu](../benchmarks/results/b1/research-oracles.json). Các snapshot AutoOAS nằm trong ZIP phát hành. Trường `pin` của chúng là SHA-256 chung của ZIP, không phải commit Git riêng cho mỗi repo.

**Giữ nguyên đầu vào.** B0, B1 và B2 có SHA-256 JCG `43690cb599334c2e02aaffea4b884ec0a1da150138b393c9925e133e3a8a62e3` và SHA-256 oracle nghiên cứu `ac3402c03585ae7c654c3ea008e3dabbd52ee8b560119dcb9271e69ed1e9c6d6`. Runner từ chối đối chiếu nếu hash đầu vào khác hoặc lượt tham chiếu thất bại. Với JCG, lỗi đọc source hoặc thiếu caller trong phạm vi chấm vẫn được tính là thiếu đích. Cảnh báo phân giải của javac được tách khỏi lỗi đọc source ở B1. Các nhãn và 79 điểm đo so sánh giữ nguyên.

## 4. Kết quả

| Phép đo | B0 | B1 | B2 |
|---|---:|---:|---:|
| JCG · đích bắt buộc tìm được | 56/81 (69,14%) | 68/81 (83,95%) | 68/81 (83,95%) |
| JCG · đích bị cấm xuất hiện | 3/11 | 1/11 | 1/11 |
| AutoOAS · verb + path khớp nhãn | 30/61 (49,18%) | 30/61 (49,18%) | 35/61 (57,38%) |
| AutoOAS · verb + path báo ngoài nhãn | 279 | 279 | 467 |
| microSecEnD · project + path khớp nhãn | 97/119 (81,51%) | 97/119 (81,51%) | 97/119 (81,51%) |
| Jasmine · đích DI / advice AOP | 6/6 · 0/8 | 6/6 · 0/8 | 6/6 · 0/8 |
| Ca nội bộ · đúng đủ tập đích theo chữ ký Java | 3/10 | 10/10 | 10/10¹ |
| Ca nội bộ · route và handler đúng kỳ vọng | Chưa đo | 5/15 | 15/15 |
| Tổng thời gian phân tích quan sát trên cùng 25 repo, chưa gồm JCG | 2,469 s | 71,826 s | 74,118 s |
| Trung vị thời gian mỗi repo trong nhóm 25 repo | 0,022 s | 2,274 s | 2,397 s |

¹ B2 chạy lại kiểm thử hồi quy chữ ký cùng suite test mục tiêu. Báo cáo so sánh độc lập cho fixture này lưu B0/B1. Ở B0 → B1, đích dư giảm **9 → 0**, đích thiếu giảm **1 → 0**. B2 qua **8 bài kiểm thử mục tiêu**, gồm Java, Spring, graph C# và Hono/TypeScript. [B0](../benchmarks/results/public-b0-summary.json) · [B1](../benchmarks/results/public-b1-summary.json) · [B2](../benchmarks/results/public-b2-summary.json) · [Fixture chữ ký](../benchmarks/results/java-signatures-b0-b1.json) · [Fixture route/handler](../benchmarks/results/spring-routes-b1-b2.json)

**JCG.** B1 thêm 12/81 đích bắt buộc tìm được theo số ròng, tương đương **+14,81 điểm phần trăm**. Khoảng tin cậy bootstrap ghép cặp 95% là **+7,41 đến +23,38 điểm phần trăm**, lấy mẫu lại theo chương trình 10.000 lần. Khoảng này áp dụng cho tập đo JCG đã chọn. Mẫu âm gồm 11 đích được upstream đánh dấu cấm. Các đích ngoài hai tập nhãn chưa được chấm; do đó không suy ra precision/F1 toàn bộ graph.

**AutoOAS.** B2 tăng **+8,20 điểm phần trăm** về tỷ lệ tìm thấy trong nhãn. Số ngoài nhãn tăng ròng **188 cặp**. Mỗi cặp ngoài nhãn cần được kiểm tra trước khi xác định đúng hay sai. Phép đo hiện tại chưa đủ để kết luận precision hoặc độ chính xác handler trên repo công khai.

| Repo AutoOAS | Mẫu số | Khớp B1 → B2 | Ngoài nhãn B1 → B2 |
|---|---:|---:|---:|
| proxyprint-kitchen | 10 | 8 → 8 | 58 → 59 |
| ocvn | 10 | 2 → 5 | 133 → 255 |
| ur-codebin-api | 6 | 6 → 6 | 0 → 0 |
| ohsome-api | 10 | 6 → 10 | 74 → 149 |
| catwatch | 10 | 7 → 5 | 4 → 2 |
| quartz-manager | 10 | 1 → 1 | 9 → 2 |
| cwa-verification-server | 5 | 0 → 0 | 1 → 0 |

Hai cặp từng khớp bị mất đều ở Catwatch, `GET /contributors` và `GET /languages`. Source gắn prefix cấp class bằng `Constants.API_RESOURCE_STATISTICS`, được khai báo là `"/statistics"`. B0/B1 đã bỏ phần prefix biểu thức và báo path con. B2 bỏ ngỏ mapping có prefix chưa phân giải. Đây là hồi quy theo khóa nhãn đang chấm, đồng thời là ca cần đối chiếu cách artifact biểu diễn prefix. Việc route nào tồn tại khi chạy app chưa được xác minh. Nếu mở rộng hỗ trợ hằng, hãy dùng quy tắc phân giải hằng Java chung và giữ nguyên cặp nhãn này khi chấm lại. [Báo cáo từng ca B1](../benchmarks/results/b1/research-cases.csv) · [Artifact AutoOAS v2](https://zenodo.org/records/20962871)

**Chi phí và phạm vi.** Các số thời gian lấy từ một lượt mỗi phiên bản, không suy rộng thành độ trễ production. B1/B2 khởi động Java theo repo và dùng phân tích javac. Lượt đo gộp khởi động JVM, phân tích source và dựng graph; chưa có profiling từng phần. Cấu hình B1/B2 đã đo dùng .NET SDK `9.0.124`, Python `3.12.3`, OpenJDK `21.0.13.1`, classpath chỉ có JDK. Ứng dụng yêu cầu JDK 17+ cho phân giải Java. B2 ưu tiên mapping annotation chuẩn có path literal, giới hạn 4.096 tổ hợp mỗi handler. Những phần còn thiếu gồm path từ hằng/cấu hình, custom annotation, route đăng ký bằng code và dispatch do proxy Spring. B1/B2 cũng có thể thiếu cạnh khi thiếu dependency hoặc generated source. Jasmine AOP vẫn 0/8. [Paper Jasmine](https://doi.org/10.1145/3551349.3556910)

## 5. Ranh giới tích hợp với nhánh song song

| Vùng dễ chồng thay đổi | Hợp đồng cần kiểm tra khi ghép |
|---|---|
| [ParsedModels](../PACE.Application/Analysis/Models/ParsedModels.cs), [JavaTreeSitterParser](../PACE.Infrastructure/Analysis/Parsing/JavaTreeSitterParser.cs), [JavaCompilerBindings](../PACE.Infrastructure/Analysis/Parsing/JavaCompilerBindings.cs), [helper javac](../PACE.Infrastructure/Analysis/Parsing/Java/PaceJavaSymbols.java) | C# và Java trao đổi declaration, call binding và override bằng vị trí trong cùng source. Thay đổi offset, encoding, khóa method hoặc định dạng output của helper cần sửa đều hai đầu. Các trường cần giữ tương thích gồm `JavaSymbol`, `JavaBinding`, `JavaOverriddenSymbols` và `SpringEndpoints`. |
| [CallResolver](../PACE.Application/Analysis/Flow/CallResolver.cs), [FlowBuilder](../PACE.Application/Analysis/Flow/FlowBuilder.cs), [MermaidBuilder](../PACE.Application/Analysis/Flow/MermaidBuilder.cs) | Tách danh tính method, vị trí gọi và nhãn hiển thị. `ParsedMethod.Id` vẫn là GUID theo lần phân tích. FlowBuilder hiện gộp theo caller, callee và điều kiện; cùng callee ở hai dòng có thể thành một cạnh. Cạnh return hiện là mô tả dựa trên kiểu, chưa phải chứng minh luồng dữ liệu. |
| [JavaSpringMappings](../PACE.Infrastructure/Analysis/Parsing/JavaSpringMappings.cs), [EntryPointMatcher](../PACE.Application/Analysis/Flow/EntryPointMatcher.cs), [BusinessTemplateGenerator](../PACE.Application/Analysis/Template/BusinessTemplateGenerator.cs) | Kiểm tra template cũ dùng tên controller ngắn và template B2 có tên đầy đủ. Giữ test nhiều verb/path, ánh xạ kế thừa, route trùng và handler chưa xác định. Các alias chung một handler có thể được gom thành một flow trong cùng nghiệp vụ ở service lưu graph. |
| [RepoAnalysisService](../PACE.Application/Analysis/RepoAnalysisService.cs), [DataFlowEdge](../PACE.Domain/Entities/DataFlowEdge.cs), [BusinessFlow](../PACE.Domain/Entities/BusinessFlow.cs) | B1/B2 chưa đổi schema DB. Quan hệ graph đã lưu dùng method ID và thứ tự cạnh. Nếu nhánh kia thêm vị trí gọi, metadata phần bị cắt, node ID hoặc dữ liệu chứng minh cho câu hỏi, kiểm tra đường ghi, đọc, render và dữ liệu cũ cùng lúc. |
| [Dockerfile](../Dockerfile), [runner](../benchmarks/run.py) | Dockerfile của B1 thêm JDK 17. Runner benchmark bỏ `PACE_JAVA_CLASSPATH` từ môi trường để giữ cấu hình JDK-only. Nếu nhánh kia đổi JDK, tải dependency, cache, hoặc sửa phép chấm, ghi phiên bản và đo lại hai phía trên cùng cấu hình trước khi so kết quả. |

Quy trình tích hợp đề xuất:

1. Ghi SHA của nhánh song song, khóa snapshot B2 gồm file mới, rồi tìm commit chung bằng `git merge-base`. Kiểm tra diff của mỗi nhánh từ commit chung, tập trung vào các hợp đồng trên.
2. Tích hợp trên nhánh riêng. Kiểm tra luồng đầy đủ **tìm endpoint → chọn handler → dựng graph → lưu/đọc → render**, gồm template đã tồn tại và template mới. Chạy regression Java/Spring/C#/TypeScript.
3. Chạy bốn bộ công khai trên từng nhánh và bản ghép với cùng đầu vào. Gắn kết quả bản ghép với SHA mới. Giữ báo cáo từng ca để phát hiện việc mất cạnh hoặc endpoint dù tổng điểm giữ nguyên. Nếu cả hai nhánh thay cùng một khâu, đo riêng từng thay đổi trước khi gán đóng góp cho nó.

## 6. Chạy lại và hướng sau B2

Đặt các artifact theo [hướng dẫn benchmark](../benchmarks/README.md). Chạy từ gốc repo. Đo B0 trên một checkout của `pace-graph-pre-b1`. Đo B2 trên source B2. Có thể thêm `--no-restore` khi .NET dependencies đã được restore.

```bash
python3 benchmarks/run.py --stage B0
python3 benchmarks/run.py --stage B2 --compare benchmarks/results/b1
dotnet test PACE.Tests/PACE.Tests.csproj --filter FullyQualifiedName~SpringRouteBenchmarkTests
```

Runner ghi metadata, log, TRX, JSON thô, CSV từng ca và delta trong `benchmarks/local/runs/<UTC>-<stage>/`. B0 công khai là `20261125T044805621095Z-B0`, B1 là `20261125T051319684835Z-B1`, B2 cuối là `20261125T053903128722Z-B2`. Toàn bộ log thô nằm ở workspace local. B1 có bộ file tối thiểu để đối chiếu đã lưu trong Git. B2 hiện có tóm tắt và kết quả fixture trong working tree.

**Sau B2, ở mức kế hoạch.** B3 lưu từng vị trí gọi và trạng thái graph bị cắt. Cần đo B2 theo vị trí source trước vì điểm JCG đang chấm đích gọi ở mức method. B4 bổ sung nhánh thực thi và quan hệ nơi tạo/nơi dùng giá trị cho Java. B5 dùng bằng chứng từ graph và dòng code để tạo đáp án trước câu hỏi, với cùng model và ngân sách câu hỏi qua các lần đo. Các đánh giá cho B3–B5 cần nhãn tương ứng. Việc cải thiện graph ở B1–B2 chưa cho phép kết luận chất lượng câu hỏi tốt hơn. [Kế hoạch PACE](graph-benchmark.md) · [Code Property Graphs](https://fabianyamaguchi.com/files/2014-ieeesp.pdf) · [Code-QA-Bench, preprint](https://arxiv.org/abs/2605.29277)

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
