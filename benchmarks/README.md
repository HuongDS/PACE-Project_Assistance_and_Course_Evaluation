# Benchmark công khai cho PACE

Bộ dùng để so sánh B0 → B1 → B2 → Final gồm **JCG, AutoOAS Spring, microSecEnD và Spring Jasmine**. Các phép đo dùng annotation hoặc artifact do nhóm tác giả công bố. Adapter của PACE chọn phần có thể đối chiếu và báo kết quả theo từng bộ. Repo sinh viên và PetClinic nằm ngoài bộ đối chứng. PetClinic có nhãn dẫn xuất trong workspace từ OpenAPI và source công khai.

## Chạy và lưu kết quả

### Checkpoint trước B1

Tag Git `pace-graph-pre-b1` lưu mã PACE và bộ đo công khai ở thời điểm trước B1. Mã sản phẩm ở mốc này khớp B0 tại `22351f5b3cc0fdc6d17602dfbb35063d9b73b76f`.

- Kết quả tổng hợp đã lưu trong Git tại [public-b0-summary.json](results/public-b0-summary.json).
- Lượt B0 dùng để đối chiếu nằm ở `benchmarks/local/runs/20261125T044805621095Z-B0/`. Thư mục `local/` lưu log, dữ liệu đầu vào và báo cáo chi tiết trên máy. Khi chuyển máy, sao lưu thư mục lượt chạy hoặc tái tạo B0 từ tag và các nguồn đã ghim bên dưới.
- Để quay về mốc này trên một nhánh riêng và giữ lịch sử nhánh đang làm, lưu các thay đổi đang dang dở rồi chạy từ thư mục gốc PACE.

```bash
git switch -c recover/pace-graph-pre-b1 pace-graph-pre-b1
```

### Lệnh benchmark

Từ thư mục gốc PACE, đặt các artifact tại đường dẫn ghi ở mục nguồn. Dùng `--no-restore` nếu đã restore dự án test.

```bash
python3 benchmarks/run.py --stage B0
python3 benchmarks/run.py --stage B1 --compare benchmarks/local/runs/<thu-muc-B0>
python3 benchmarks/run.py --stage B2 --compare benchmarks/local/runs/<thu-muc-B0>
python3 benchmarks/run.py --stage Final --compare benchmarks/local/runs/<thu-muc-B0>
```

Thay `<thu-muc-B0>` bằng thư mục runner đã in. B0 kiểm tra mã sản phẩm trùng commit `22351f5b3cc0fdc6d17602dfbb35063d9b73b76f`. Mỗi lượt lưu commit PACE, fingerprint của mã và harness, SDK, môi trường và hash đầu vào. Lượt so sánh yêu cầu B0 thành công và hash đầu vào trùng khớp. Có thể chạy riêng `--suite jcg` hoặc `--suite research` (ba bộ Spring/microservice) để chẩn đoán. Mỗi lượt so sánh cần dùng cùng bộ dữ liệu.

| File trong `benchmarks/local/runs/<UTC>-<stage>/` | Nội dung |
|---|---|
| `run.json`, `run.log`, `tests.trx` | Đầu vào, môi trường, trạng thái, log và kết quả test |
| `jcg-raw.json` | Kết quả theo từng annotation JCG |
| `research-oracles.json`, `research-raw.json`, `research-cases.csv` | Nguồn nhãn, đầu ra PACE và kết quả từng ca của ba bộ nghiên cứu |
| `summary.json` | Tóm tắt theo từng bộ và từng loại phép đo |
| `comparison.json`, `comparison.csv`, `jcg-delta.json` | Chênh lệch so với lượt đã chỉ định bằng `--compare` |

### B1 · Phân giải lời gọi Java

PACE dùng Tree-sitter để đọc cấu trúc source và dùng `JavacTask.parse()/analyze()` để xác định khai báo mà Java chọn cho mỗi lời gọi. Với lời gọi có dispatch ảo, PACE xét các implementation trong source theo phân cấp kiểu. Symbol Java gồm tên binary đầy đủ của class và chữ ký tham số sau type erasure. Lời gọi thiếu bằng chứng được ghi nhận trong warning. Công cụ chỉ phân tích source đã quét, tắt annotation processing và dùng thư viện JDK theo mặc định. Ứng dụng cần JDK 17 trở lên. Có thể cấu hình `PACE_JAVA_CLASSPATH` bằng đường dẫn JAR hoặc thư mục class tin cậy đã có sẵn. Runner benchmark dùng JDK với classpath rỗng để giữ cấu hình B0/B1 có thể lặp lại.

Các dự án thiếu dependency, generated source hoặc module đầy đủ có thể mất cạnh. Các cạnh DI/AOP do Spring tạo lúc chạy cần mô hình framework. Benchmark B1 dùng [đặc tả overload Java](https://docs.oracle.com/javase/specs/jls/se21/html/jls-15.html#jls-15.12), [API JavacTask](https://docs.oracle.com/en/java/javase/21/docs/api/jdk.compiler/com/sun/source/util/JavacTask.html) và nguyên tắc tách phân giải khai báo khỏi dispatch trong [Unimocg](https://doi.org/10.1145/3650212.3652109).

Bộ `signatures` đo chính xác tập đích gọi theo file và chữ ký. Đây là 10 ca kiểm tra có source và nhãn ghi trong [java-signatures.json](fixtures/java-signatures.json), chạy riêng ngoài bốn benchmark công khai. B0 của bộ này được đo trước khi đổi mã sản phẩm. Kết quả và hash fixture có trong [java-signatures-b0-b1.json](results/java-signatures-b0-b1.json).

[Kết quả B1 trên bốn bộ công khai](results/public-b1-summary.json) ghi lại chênh lệch JCG với cùng nhãn và cùng 79 điểm đo, cùng số liệu thời gian quan sát được trong hai lượt chạy. Trên 25 repo của ba bộ Spring/microservice, tổng thời gian phân tích quan sát được là 2,47 giây ở B0 và 71,83 giây ở B1. Đây là hai lượt chạy, mỗi repo ở B1 khởi động một tiến trình Java.

```bash
python3 benchmarks/run.py --stage B1 --suite signatures --compare benchmarks/local/runs/20261125T050456681020Z-B0
python3 benchmarks/run.py --stage B1 --compare benchmarks/local/runs/20261125T044805621095Z-B0
```

### B2 · Tìm route và handler Spring

B2 ghép các path ở cấp class và cấp method, giữ các alias và HTTP verb khai báo, rồi ưu tiên mapping khớp verb và path khi chọn handler. Nó kiểm tra import Spring và dùng quan hệ override do `javac` xác định để lấy mapping từ interface hoặc lớp cha trong source. Khi có nhiều handler khớp, một hint controller và method chính xác có thể phân biệt chúng. Ca vẫn mơ hồ được ghi cảnh báo. Quy tắc dựa trên [Spring Mapping Requests](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-controller/ann-requestmapping.html) và cách đánh giá endpoint từ source trong [AutoOAS](https://doi.org/10.1016/j.jss.2026.113122).

Phạm vi là mapping annotation chuẩn với path literal trong controller có stereotype Spring trực tiếp. PACE ghi cảnh báo và bỏ ngỏ handler vượt 4.096 tổ hợp mapping. Route đăng ký bằng code, path lấy từ config hoặc biểu thức, custom annotation và điều kiện phân giải lúc chạy cần phép phân tích bổ sung. AutoOAS và microSecEnD đánh giá route. [15 ca kiểm tra B1/B2](results/spring-routes-b1-b2.json) kiểm tra thêm handler đích và trường hợp cần để ngỏ.

B1 có tag `pace-graph-b1` và báo cáo từng ca trong `benchmarks/results/b1/`. Runner có thể dùng trực tiếp bản đối chứng trong Git.

```bash
python3 benchmarks/run.py --stage B2 --compare benchmarks/results/b1
dotnet test PACE.Tests/PACE.Tests.csproj --filter FullyQualifiedName~SpringRouteBenchmarkTests
```

## Nguồn công khai và đơn vị chấm

Đặt JCG tại `benchmarks/local/jcg` ở commit `4737aac8c2652acded1c4505961b6af52d06ceeb`.

```bash
git clone https://github.com/opalj/JCG.git benchmarks/local/jcg
git -C benchmarks/local/jcg checkout --detach 4737aac8c2652acded1c4505961b6af52d06ceeb
```

Đặt ba nguồn sau dưới `benchmarks/local/upstream/`.

| Đường dẫn | Nguồn đã ghim | Phép đo |
|---|---|---|
| `auto-oas.zip` | [AutoOAS v2 trên Zenodo](https://zenodo.org/records/20962871), MD5 `460e47f4108b2b8e663376de467bd08c` do tác giả công bố. | `verb + route` trên 7 dự án Spring và 8 spec. Hai profile CWA được hợp ở cấp dự án, không chấm riêng việc nhận biết profile. |
| `microsecend` và `microsecend-sources` | [microSecEnD](https://github.com/tuhh-softsec/microSecEnD) tại `63653e09177b6d757eada4521f8bace8a8c20766` và [source submodule của paper EMSE](https://github.com/M3SOulu/EMSE2025SAR-Benchmarks) tại `bc21234b64f8cf55a181fe8d86a3632fe1234eec`. Chạy `git submodule update --init` ở repo source. | Endpoint ở cấp `project + path` vì annotation phát hành chứa path. Loại trùng path giữa các service trong từng project cho phép đo này. |
| `jasmine` | [Spring Micro-Benchmarks](https://github.com/SpringJasmine/Spring-micro-benchmarks) tại `79ad187a6f7ca07cfc6b874b05f5972ba9d64132`. | Tập con có bằng chứng source từ ca gắn tag và khai báo bean. Chấm riêng 6 cặp method DI và 8 kiểm tra advice AOP có thể xuất hiện trong graph. |

Runner xuất `research-oracles.json` với pin và nhãn, `research-raw.json`, `research-cases.csv` và `summary.json`. Các route PACE báo ngoài gold được giữ dưới `unmatched_observation` để kiểm tra phạm vi trước khi tính precision toàn bộ. Báo riêng từng bộ và từng cơ chế, không trộn mẫu số. Nguồn phương pháp: [AutoOAS](https://doi.org/10.1016/j.jss.2026.113122), [EMSE 2025](https://doi.org/10.1007/s10664-025-10686-2), [Jasmine](https://doi.org/10.1145/3551349.3556910).

JCG dựa trên [Judge](https://michael-reif.name/publications/RKE%2B2019_preprint.pdf). Adapter này đo **direct target ở cấp owner + method name**, qua projection của đầu ra PACE B0. Cạnh ngoài các nhãn required/prohibited giữ trạng thái chưa chấm. Vì thế báo **required-target recall** và **prohibited-target violation rate**. Không dùng phần cạnh chưa chấm để tính precision/F1. Các nhãn indirect, constructor, nested JVM owner và ca không thể quy về một lần gọi cùng tên được báo riêng. Caller không tìm thấy và lỗi parse trong tập đủ điều kiện được tính là thiếu target. Kết quả của adapter không tương đương phép chấm bytecode chính thức của JCG.

## Sử dụng kết quả trong paper

Giữ nguyên commit, nhãn, đơn vị phân tích và quy tắc chấm qua các phiên bản. Báo số đo riêng cho lời gọi Java, endpoint Spring, endpoint microservice và DI/AOP. Mỗi phép đo dùng mẫu số riêng. Giải thích các ca bị loại theo nguyên nhân. Dùng JCG để kiểm tra thay đổi phân tích Java dựa trên ngữ nghĩa ngôn ngữ, và áp dụng cùng quy tắc cho source Java ngoài benchmark. Các mốc triển khai được ghi trong [kế hoạch benchmark graph](../docs/graph-benchmark.md).

## Kết quả lịch sử PetClinic

B0 từng phát hiện 0/36 endpoint PetClinic. Báo cáo theo từng ca vẫn có trong `results/`. Bộ regression C# có thể chạy độc lập bằng bộ lọc `FullyQualifiedName~GraphBenchmarkTests`.

PetClinic đánh giá **bao phủ các endpoint có trong gold**, không chấm precision của mọi endpoint hệ thống có thể có. Input matcher chỉ chứa verb và path ở cấp ứng dụng. OpenAPI khai báo base URL có context path `/petclinic`; source controller dùng prefix `/api`. Gold trong repo dùng route tương đối với application context. 36 dòng đã nối operation ID với concrete override. Một operation `failingRequest` bị loại vì không có override duy nhất trong nhóm controller đã chọn. Đây là đối chứng theo hợp đồng và source, chưa xác minh runtime bằng cách chạy app.

### Tái hiện kết quả lịch sử

Tải đúng hai snapshot công khai, sau đó chạy từ thư mục gốc PACE. Không chạy build hoặc code của repo đầu vào trong hai bài đo này.

```bash
git clone https://github.com/opalj/JCG.git /tmp/pace-benchmark-jcg
git -C /tmp/pace-benchmark-jcg checkout --detach 4737aac8c2652acded1c4505961b6af52d06ceeb
git clone https://github.com/spring-petclinic/spring-petclinic-rest.git /tmp/pace-benchmark-petclinic
git -C /tmp/pace-benchmark-petclinic checkout --detach 220f4ba8e92cec9ad1faa03324723a3e9da63d95

python3 benchmarks/import-jcg.py /tmp/pace-benchmark-jcg /tmp/pace-jcg-java.json
python3 benchmarks/import-petclinic.py /tmp/pace-benchmark-petclinic /tmp/pace-petclinic-gold.json
cmp benchmarks/petclinic-endpoints.json /tmp/pace-petclinic-gold.json

PACE_JCG_DATASET=/tmp/pace-jcg-java.json \
PACE_JCG_REPORT=/tmp/pace-jcg-candidate.json \
PACE_PETCLINIC_ROOT=/tmp/pace-benchmark-petclinic \
PACE_PETCLINIC_GOLD="$PWD/benchmarks/petclinic-endpoints.json" \
PACE_PETCLINIC_REPORT=/tmp/pace-petclinic-candidate.json \
dotnet test PACE.Tests/PACE.Tests.csproj -m:1 -p:UseSharedCompilation=false \
  --filter 'Category=ExternalBenchmark|FullyQualifiedName~GraphBenchmarkTests' \
  --logger 'console;verbosity=detailed'

python3 benchmarks/compare-jcg.py benchmarks/results/jcg-java-b0.json /tmp/pace-jcg-candidate.json
```

JCG input SHA-256 `43690cb599334c2e02aaffea4b884ec0a1da150138b393c9925e133e3a8a62e3`. Script so sánh dùng cùng ID và gold ở B0 và candidate. Nó dừng nếu mất điểm đo hoặc nhãn thay đổi. Bootstrap ghép cặp lấy mẫu theo chương trình JCG, phản ánh biến động trong tập ca này. Khi analyzer sau này có call-site ID, giữ phép đo cũ để so cùng thước và mở thêm chỉ số theo signature và source span.
