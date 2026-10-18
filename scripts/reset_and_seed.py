#!/usr/bin/env python3
"""
Reset & Seed script cho PACE.
Usage:
  python3 scripts/reset_and_seed.py
  python3 scripts/reset_and_seed.py --no-truncate
  python3 scripts/reset_and_seed.py --dry-run
"""
import argparse, json, os, subprocess, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NS   = uuid.UUID("d1ce5eed-cafe-4a11-b0b5-face5eedface")

def uid(*parts):
    return str(uuid.uuid5(NS, ":".join(str(p) for p in parts)))

def lit(v):
    if v is None: return "NULL"
    return "'" + str(v).replace("'", "''") + "'"

def is_uuid(s):
    try: uuid.UUID(s); return True
    except: return False

FEATURES = [
    dict(key="Order", business_name="Dat hang",
         entry_method="PlaceOrder",
         entry_code="public decimal PlaceOrder(int quantity, decimal price) {\n    if (quantity <= 0) throw new ArgumentException(\"quantity\");\n    if (price < 0) throw new ArgumentException(\"price\");\n    return CalculateTotal(quantity, price);\n}",
         helper_method="CalculateTotal",
         helper_code="public decimal CalculateTotal(int quantity, decimal price) {\n    return quantity * price;\n}",
         q1="Ham PlaceOrder kiem tra du lieu nao truoc khi tinh tong tien?",
         a1="Ham tu choi quantity <= 0 va price < 0, sau do goi CalculateTotal de tinh quantity * price.",
         q2="Vi sao CalculateTotal tra ve decimal?",
         a2="Decimal giu do chinh xac tai chinh, tranh sai so dau phay dong."),
    dict(key="Inventory", business_name="Kiem tra ton kho",
         entry_method="ReserveStock",
         entry_code="public int ReserveStock(int stock, int requested) {\n    if (requested <= 0 || requested > stock)\n        throw new ArgumentException(\"requested\");\n    return RemainingStock(stock, requested);\n}",
         helper_method="RemainingStock",
         helper_code="public int RemainingStock(int stock, int requested) {\n    return stock - requested;\n}",
         q1="ReserveStock xu ly the nao khi so luong yeu cau lon hon ton kho?",
         a1="Neu requested <= 0 hoac requested > stock thi nem ArgumentException; neu hop le tra ve stock - requested.",
         q2="Neu stock=10, requested=3 thi ket qua la gi?",
         a2="Ket qua la 7; luong: ReserveStock validate -> RemainingStock(10,3) -> return 7."),
    dict(key="Discount", business_name="Ap dung giam gia",
         entry_method="ApplyDiscount",
         entry_code="public decimal ApplyDiscount(decimal total, int percent) {\n    if (total < 0 || percent < 0 || percent > 100)\n        throw new ArgumentException(\"discount\");\n    return DiscountedTotal(total, percent);\n}",
         helper_method="DiscountedTotal",
         helper_code="public decimal DiscountedTotal(decimal total, int percent) {\n    return total * (100 - percent) / 100m;\n}",
         q1="ApplyDiscount gioi han phan tram giam gia nhu the nao?",
         a1="percent phai trong [0,100] va total >= 0; percent=100 hop le tra ve 0; ngoai mien nem ArgumentException.",
         q2="Tac dung cua 100m trong DiscountedTotal la gi?",
         a2="100m ep kieu ve decimal de toan bo phep tinh trong khong gian decimal, tranh mat chinh xac."),
]

RUBRIC_CRITERIA = [
    dict(code="LOGIC",   name="Hieu logic dieu kien", statement="Giai thich dung dieu kien kiem tra dau vao.", order=1),
    dict(code="FLOW",    name="Mo ta luong goi ham",   statement="Neu dung thu tu goi ham va chieu truyen du lieu.", order=2),
    dict(code="EXAMPLE", name="Vi du minh hoa",        statement="Cung cap vi du so lieu cu the va dung ket qua.", order=3),
]

STUDENTS = [
    dict(email="student1@fpt.edu.vn", code="SE001"),
    dict(email="student2@fpt.edu.vn", code="SE002"),
]

def build_sql(truncate):
    sql = ["BEGIN;", "SET LOCAL standard_conforming_strings = on;"]

    def insert(table, **kw):
        cols = ", ".join('"' + k + '"' for k in kw)
        vals_list = []
        for v in kw.values():
            if v is None:
                vals_list.append("NULL")
            elif isinstance(v, str) and len(v) == 36 and v.count("-") == 4 and is_uuid(v):
                vals_list.append("'" + v + "'::uuid")
            else:
                vals_list.append(lit(v))
        sql.append("INSERT INTO " + table + " (" + cols + ") VALUES (" + ", ".join(vals_list) + ") ON CONFLICT DO NOTHING;")

    if truncate:
        sql.append("-- === TRUNCATE ===")
        for t in ["audit_events","session_students","assessment_sessions","course_projects","courses",
                  "rubric_scores","rubric_criteria","student_answers","student_submissions",
                  "data_flow_edges","business_flows","business_methods","generated_questions",
                  "businesses","method_sources","users","analysis_runs"]:
            sql.append('TRUNCATE TABLE "' + t + '" RESTART IDENTITY CASCADE;')

    sql.append("\n-- === RUBRIC CRITERIA ===")
    for rc in RUBRIC_CRITERIA:
        insert("rubric_criteria", Id=uid("rc", rc["code"]), Code=rc["code"],
               Name=rc["name"], Statement=rc["statement"], DisplayOrder=rc["order"])

    sql.append("\n-- === LECTURER ===")
    lecturer_id = uid("user", "lecturer@fpt.edu.vn")
    insert("users", Id=lecturer_id, Email="lecturer@fpt.edu.vn", StudentCode=None,
           Role="Lecturer", AnalysisRunId=None, CreatedAt="2026-10-01T00:00:00Z")

    all_run_ids = []
    for stu in STUDENTS:
        email, code = stu["email"], stu["code"]
        sql.append("\n-- === STUDENT: " + email + " ===")

        biz_tmpl = [{"business_name": f["business_name"],
                     "apis": [{"controller": f["key"]+"Service", "function": f["entry_method"]}]}
                    for f in FEATURES]

        # current run
        run_id = uid("run", "current", email)
        all_run_ids.append(run_id)
        insert("analysis_runs", Id=run_id,
               RepoUrl="https://github.com/pace-demo/" + code.lower() + "-shop",
               RepoName="[SEED] " + code + " - E-Commerce Service",
               AnalyzedCommitSha="abc" + uuid.uuid5(NS, email).hex[:7],
               AnalyzedAt="2026-10-15T08:00:00Z",
               BusinessTemplateJson=json.dumps(biz_tmpl, ensure_ascii=False),
               TemplateSource="Confirmed",
               TemplateUpdatedAt="2026-10-15T08:00:00Z",
               CreatedAt="2026-10-15T08:00:00Z")

        user_id = uid("user", email)
        insert("users", Id=user_id, Email=email, StudentCode=code, Role="Student",
               AnalysisRunId=run_id, CreatedAt="2026-10-01T00:00:00Z")

        # history run (bai cu da nop)
        hist_run_id = uid("run", "history", email)
        insert("analysis_runs", Id=hist_run_id,
               RepoUrl="https://github.com/pace-demo/" + code.lower() + "-shop-v1",
               RepoName="[SEED] " + code + " - E-Commerce Service (v1 history)",
               AnalyzedCommitSha="def" + uuid.uuid5(NS, email+"h").hex[:7],
               AnalyzedAt="2026-10-10T08:00:00Z",
               BusinessTemplateJson=json.dumps(biz_tmpl, ensure_ascii=False),
               TemplateSource="Confirmed",
               TemplateUpdatedAt="2026-10-10T08:00:00Z",
               CreatedAt="2026-10-10T08:00:00Z")

        # history submission (da nop + co feedback)
        hist_sub_id = uid("submission", "history", email)
        insert("student_submissions", Id=hist_sub_id, UserId=user_id,
               AnalysisRunId=hist_run_id,
               CreatedAt="2026-10-10T09:00:00Z", SubmittedAt="2026-10-10T09:45:00Z")

        # current submission - CHUA SUBMIT (SubmittedAt = NULL)
        cur_sub_id = uid("submission", "current", email)
        insert("student_submissions", Id=cur_sub_id, UserId=user_id,
               AnalysisRunId=run_id,
               CreatedAt="2026-10-18T08:00:00Z", SubmittedAt=None)

        for run_kind, run_id_x, sub_id_x in [
            ("current", run_id,      cur_sub_id),
            ("history", hist_run_id, hist_sub_id),
        ]:
            fmts = ["Sandwich", "Rubric", "Explainable"]
            for idx, f in enumerate(FEATURES):
                pfx       = email + ":" + run_kind + ":" + f["key"]
                biz_id    = uid(pfx, "business")
                entry_id  = uid(pfx, "entry")
                helper_id = uid(pfx, "helper")
                flow_id   = uid(pfx, "flow")

                insert("businesses", Id=biz_id, AnalysisRunId=run_id_x,
                       BusinessName=f["business_name"])

                for mid, mname, mcode, node in [
                    (entry_id,  f["entry_method"],  f["entry_code"],  "DecisionGateway"),
                    (helper_id, f["helper_method"], f["helper_code"], "Activity"),
                ]:
                    insert("method_sources", Id=mid, AnalysisRunId=run_id_x,
                           FilePath="Services/" + f["key"] + "Service.cs",
                           ClassName=f["key"] + "Service", MethodName=mname,
                           SourceCode=mcode, Language="CSharp", NodeType=node)
                    insert("business_methods", BusinessId=biz_id, MethodSourceId=mid)

                mermaid = ('flowchart TD\n  A["' + f["entry_method"] +
                           '"] -->|"Data valid"| B["' + f["helper_method"] + '"]\n' +
                           '  B -->|"Return"| A')
                insert("business_flows", Id=flow_id, BusinessId=biz_id,
                       EntryMethodId=entry_id, Name=f["entry_method"],
                       MermaidGraph=mermaid, CreatedAt="2026-10-15T08:00:00Z")

                for step, src, tgt, etype, cond, dlabel in [
                    (1, entry_id,  helper_id, "Call",   "Du lieu hop le",  "Tham so dau vao"),
                    (2, helper_id, entry_id,  "Return", None,              "Ket qua tinh toan"),
                ]:
                    insert("data_flow_edges",
                           Id=uid(pfx, "edge"+str(step)), FlowId=flow_id,
                           StepOrder=step, FromMethodId=src, ToMethodId=tgt,
                           EdgeType=etype, ConditionContext=cond, DataLabel=dlabel)

                q1_id = uid(pfx, "q1")
                q2_id = uid(pfx, "q2")
                insert("generated_questions", Id=q1_id, BusinessId=biz_id,
                       QuestionText=f["q1"], SuggestedAnswer=f["a1"],
                       Difficulty="Medium", ContextType="Raw",
                       CreatedAt="2026-10-15T08:05:00Z")
                insert("generated_questions", Id=q2_id, BusinessId=biz_id,
                       QuestionText=f["q2"], SuggestedAnswer=f["a2"],
                       Difficulty="Hard", ContextType="Raw",
                       CreatedAt="2026-10-15T08:05:00Z")

                fmt = fmts[idx % len(fmts)]
                for q_id, q_ans, q_num in [(q1_id, f["a1"], "ans1"), (q2_id, f["a2"], "ans2")]:
                    ans_id = uid(pfx, q_num)
                    fb = {"feedback_type": fmt, "student_answer_id": ans_id,
                          "comment": "[SEED] Feedback mau."}
                    if fmt == "Sandwich":
                        fb["positives"] = "Neu dung dieu kien validate."
                        fb["constructive_criticism"] = "Can giai thich them truong hop bien."
                        fb["next_steps"] = "Thu " + f["entry_method"] + "(0, 0) va kiem tra exception."
                    elif fmt == "Rubric":
                        fb["criteria"] = [
                            {"name": "Hieu logic", "score": 4, "comment": "Dung dieu kien."},
                            {"name": "Luong ham",  "score": 3, "comment": "Can ro hon."},
                        ]
                    else:
                        fb["code_evidence"] = f["entry_code"]
                        fb["logic_explanation"] = f["helper_method"] + " thuc hien sau khi validate."

                    insert("student_answers", Id=ans_id, UserId=user_id,
                           SubmissionId=sub_id_x, QuestionId=q_id,
                           AnswerText=q_ans, FeedbackFormat=fmt,
                           AiFeedback=json.dumps(fb, ensure_ascii=False),
                           StudentReply=None, CreatedAt="2026-10-18T08:15:00Z")

                    if fmt == "Rubric":
                        for rc in RUBRIC_CRITERIA[:2]:
                            insert("rubric_scores",
                                   Id=uid(pfx, q_num, "score", rc["code"]),
                                   StudentAnswerId=ans_id,
                                   CriterionId=uid("rc", rc["code"]),
                                   Score=4 if rc["code"] == "LOGIC" else 3)

    sql.append("\n-- === COURSE / SESSION ===")
    course_id = uid("course", "PRJ301")
    insert("courses", Id=course_id, Code="PRJ301",
           Name="Project Management and Professional Practice",
           Semester="Fall2026", LecturerId=lecturer_id)

    for run_id in all_run_ids:
        insert("course_projects", AnalysisRunId=run_id, CourseId=course_id,
               IsApproved=True, Notes="[SEED] Approved.",
               DecidedAt="2026-10-16T07:00:00Z")

    session_id = uid("session", "PRJ301", "mid")
    insert("assessment_sessions", Id=session_id, ProjectId=all_run_ids[0],
           Title="[SEED] Giua ky - Van dap code review",
           Status="CREATED",
           ScheduledStart="2026-10-20T08:00:00Z",
           ScheduledEnd="2026-10-20T10:00:00Z",
           ActualStartTime=None, ActualEndTime=None,
           RubricJson=json.dumps([{"code": rc["code"], "name": rc["name"], "maxScore": 5}
                                   for rc in RUBRIC_CRITERIA], ensure_ascii=False),
           Version=uid("version", "session", "mid"))

    for stu in STUDENTS:
        insert("session_students",
               SessionId=session_id, StudentId=uid("user", stu["email"]),
               EvaluationJson=None, EvaluatorId=None, EvaluatedAt=None)

    sql.append("\nCOMMIT;")
    sql.append("""
SELECT u."Email", u."StudentCode", u."Role", r."RepoName",
  (SELECT count(*) FROM businesses b WHERE b."AnalysisRunId"=r."Id") AS businesses,
  (SELECT count(*) FROM generated_questions q JOIN businesses b ON b."Id"=q."BusinessId" WHERE b."AnalysisRunId"=r."Id") AS questions,
  (SELECT count(*) FROM student_submissions s WHERE s."UserId"=u."Id" AND s."SubmittedAt" IS NULL) AS pending_submissions
FROM users u
LEFT JOIN analysis_runs r ON r."Id"=u."AnalysisRunId"
ORDER BY u."Role", u."Email";
""")
    return sql


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--no-truncate", action="store_true")
    parser.add_argument("--dry-run",     action="store_true")
    args = parser.parse_args()

    config = json.loads((ROOT / "PACE.API" / "appsettings.Development.json").read_text())
    connection = (os.environ.get("ConnectionStrings__DefaultConnection")
                  or config["ConnectionStrings"]["DefaultConnection"])
    parts = dict((k.strip().lower(), v.strip())
                 for k, v in (item.split("=", 1) for item in connection.split(";") if "=" in item))

    full_sql = "\n".join(build_sql(truncate=not args.no_truncate))

    if args.dry_run:
        print(full_sql); return

    env = os.environ.copy()
    env.update(PGPASSWORD=parts.get("password",""), PGCONNECT_TIMEOUT="15", PGSSLMODE="require")
    cmd = ["psql","-X","-v","ON_ERROR_STOP=1",
           "-h", parts.get("host","localhost"),
           "-p", parts.get("port","5432"),
           "-U", parts.get("username","postgres"),
           "-d", parts.get("database","postgres")]

    print("Connecting to database...")
    r = subprocess.run(cmd, input=full_sql, text=True, env=env, capture_output=True)
    if r.returncode != 0:
        print("ERROR:\n", r.stderr); raise SystemExit(1)

    out = r.stdout
    idx = out.rfind(" Email")
    print(out[idx:] if idx != -1 else out)
    print()
    print("=== Seed xong! ===")
    print("  [Lecturer] lecturer@fpt.edu.vn")
    print("  [Student]  student1@fpt.edu.vn  (SE001) - CHUA SUBMIT")
    print("  [Student]  student2@fpt.edu.vn  (SE002) - CHUA SUBMIT")
    print()
    print("Ca 2 student co: AnalysisRun hien tai + history, 3 Business, 6 Question,")
    print("Student answers voi AI feedback (Sandwich/Rubric/Explainable).")
    print("SubmittedAt = NULL -> co the test luong submit tu dau.")

if __name__ == "__main__":
    main()
