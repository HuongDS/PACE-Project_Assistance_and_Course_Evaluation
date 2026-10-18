#!/usr/bin/env python3
"""Seed isolated FE fixtures using the configured development PostgreSQL database.
Usage: python3 scripts/seed_fe_demo.py --email your-google-email@example.com
Only inserts fixtures; refuses to reuse an existing unrelated account.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
NS = uuid.UUID('08e218fe-85cc-497f-a700-214ed573d68d')

def ident(name):
    return str(uuid.uuid5(NS, name))

def literal(value):
    if value is None:
        return 'NULL'
    return "'" + str(value).replace("'", "''") + "'"

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--email', required=True)
    args = parser.parse_args()
    email = args.email.strip().lower()
    if '@' not in email or any(c.isspace() for c in email):
        parser.error('A valid Google email is required')
    config = json.loads((ROOT / 'PACE.API/appsettings.Development.json').read_text())
    connection = os.environ.get('ConnectionStrings__DefaultConnection') or config['ConnectionStrings']['DefaultConnection']
    parts = dict((k.strip().lower(), v.strip()) for k, v in
                 (item.split('=', 1) for item in connection.split(';') if '=' in item))
    env = os.environ.copy()
    env.update(PGPASSWORD=parts.get('password', ''), PGCONNECT_TIMEOUT='10', PGSSLMODE='require')
    command = ['psql', '-X', '-v', 'ON_ERROR_STOP=1', '-h', parts.get('host', 'localhost'),
               '-p', parts.get('port', '5432'), '-U', parts.get('username', 'postgres'),
               '-d', parts.get('database', 'postgres')]
    user_id = ident('student:' + email)
    student_code = 'DEMO' + uuid.UUID(user_id).hex[:8].upper()
    sql = ['BEGIN;', 'SET LOCAL standard_conforming_strings = on;']
    def insert(table, **values):
        columns = ', '.join('"' + key + '"' for key in values)
        sql.append(f'INSERT INTO {table} ({columns}) VALUES ({", ".join(literal(v) for v in values.values())}) ON CONFLICT DO NOTHING;')

    # Fail the transaction rather than silently changing an existing real account.
    sql.append(f'''DO $guard$ BEGIN
        IF EXISTS (SELECT 1 FROM users WHERE "Email" = {literal(email)} AND "Id" <> {literal(user_id)}::uuid)
        THEN RAISE EXCEPTION 'Email already belongs to another account; no data seeded'; END IF;
        IF EXISTS (SELECT 1 FROM users WHERE "Id" = {literal(user_id)}::uuid AND ("Email" <> {literal(email)} OR "Role" <> 'Student'))
        THEN RAISE EXCEPTION 'Demo account identity changed; no data seeded'; END IF;
    END $guard$;''')
    features = [
        ('Order', 'Đặt hàng', 'PlaceOrder',
         'public decimal PlaceOrder(int quantity, decimal price) { if (quantity <= 0) throw new ArgumentException("quantity"); if (price < 0) throw new ArgumentException("price"); return CalculateTotal(quantity, price); }',
         'CalculateTotal', 'public decimal CalculateTotal(int quantity, decimal price) { return quantity * price; }',
         'Hàm PlaceOrder kiểm tra dữ liệu nào trước khi tính tổng tiền và vì sao?',
         'Hàm từ chối quantity <= 0 và price < 0 bằng ArgumentException, sau đó gọi CalculateTotal để tính quantity * price.',
         'Vì sao CalculateTotal trả về decimal và tổng tiền được tính như thế nào?'),
        ('Inventory', 'Kiểm tra tồn kho', 'ReserveStock',
         'public int ReserveStock(int stock, int requested) { if (requested <= 0 || requested > stock) throw new ArgumentException("requested"); return RemainingStock(stock, requested); }',
         'RemainingStock', 'public int RemainingStock(int stock, int requested) { return stock - requested; }',
         'ReserveStock xử lý thế nào khi số lượng yêu cầu lớn hơn tồn kho?',
         'Nếu requested <= 0 hoặc requested > stock thì ném ArgumentException; chỉ khi hợp lệ mới trả về stock - requested.',
         'Nếu stock = 10 và requested = 3 thì kết quả là gì? Giải thích luồng gọi hàm.'),
        ('Discount', 'Áp dụng giảm giá', 'ApplyDiscount',
         'public decimal ApplyDiscount(decimal total, int percent) { if (total < 0 || percent < 0 || percent > 100) throw new ArgumentException("discount"); return DiscountedTotal(total, percent); }',
         'DiscountedTotal', 'public decimal DiscountedTotal(decimal total, int percent) { return total * (100 - percent) / 100m; }',
         'ApplyDiscount giới hạn phần trăm giảm giá như thế nào? Điều gì xảy ra với percent = 100?',
         'percent phải nằm trong [0,100] và total >= 0. percent = 100 hợp lệ và trả về 0; ngoài miền hợp lệ sẽ ném ArgumentException.',
         'Giải thích tác dụng của 100m trong công thức DiscountedTotal.')
    ]
    current_repo = ident('repo:current:' + email)
    history_repo = ident('repo:history:' + email)
    for kind, repo_id in [('current', current_repo), ('history', history_repo)]:
        insert('analysis_runs', Id=repo_id, RepoName=f'[DEMO] PACE Shop - {kind}',
               RepoUrl=f'https://github.com/pace-demo/fe-shop-{kind}', TemplateSource='Confirmed',
               BusinessTemplateJson=json.dumps([{'business_name': f[1], 'apis': [{'controller': f[0]+'Service', 'function': f[2]}]} for f in features], ensure_ascii=False))
    insert('users', Id=user_id, Email=email, StudentCode=student_code, Role='Student', AnalysisRunId=current_repo)
    submission_id = ident('submission:history:' + email)
    insert('student_submissions', Id=submission_id, UserId=user_id, AnalysisRunId=history_repo,
           CreatedAt='2026-10-17T03:00:00Z', SubmittedAt='2026-10-17T03:15:00Z')
    questions = []
    for kind, repo_id in [('current', current_repo), ('history', history_repo)]:
        for index, f in enumerate(features):
            key, name, entry, code, helper, helper_code, question, suggested, second = f
            prefix = f'{email}:{kind}:{key}'
            business_id, entry_id, helper_id, flow_id = [ident(prefix + ':' + part) for part in ['business', 'entry', 'helper', 'flow']]
            insert('businesses', Id=business_id, AnalysisRunId=repo_id, BusinessName=name)
            for method_id, method_name, source, node in [(entry_id, entry, code, 'DecisionGateway'), (helper_id, helper, helper_code, 'Activity')]:
                insert('method_sources', Id=method_id, AnalysisRunId=repo_id, FilePath=f'Services/{key}Service.cs',
                       ClassName=key+'Service', MethodName=method_name, SourceCode=source, Language='CSharp', NodeType=node)
                insert('business_methods', BusinessId=business_id, MethodSourceId=method_id)
            insert('business_flows', Id=flow_id, BusinessId=business_id, EntryMethodId=entry_id,
                   Name=entry, MermaidGraph=f'flowchart TD\n A["{entry}"] -->|"Dữ liệu hợp lệ"| B["{helper}"]\n B -->|"Trả kết quả"| A')
            for step, source, target, edge_type in [(1, entry_id, helper_id, 'Call'), (2, helper_id, entry_id, 'Return')]:
                insert('data_flow_edges', Id=ident(prefix+f':edge:{step}'), FlowId=flow_id, StepOrder=step,
                       FromMethodId=source, ToMethodId=target, EdgeType=edge_type,
                       ConditionContext='Dữ liệu hợp lệ' if step == 1 else None, DataLabel='Tham số đầu vào' if step == 1 else 'Kết quả tính toán')
            question_id = ident(prefix+':question:1')
            for number, text, reference in [(1, question, suggested), (2, second, helper_code)]:
                insert('generated_questions', Id=ident(prefix+f':question:{number}'), BusinessId=business_id,
                       QuestionText=text, SuggestedAnswer=reference, Difficulty='Medium', ContextType='Raw')
            if kind == 'current':
                questions.append({'businessId': business_id, 'questionId': question_id, 'answerText': suggested})
            else:
                answer_id = ident(prefix+':answer')
                formats = ['Sandwich', 'Rubric', 'Explainable']
                feedback = {'feedback_type': formats[index], 'student_answer_id': answer_id, 'comment': '[DEMO] Feedback mẫu được seed để kiểm thử FE, không phải kết quả gọi AI.'}
                if index == 0:
                    feedback.update(positives='Bạn đã nêu đúng các kiểm tra đầu vào.', constructive_criticism='Cần giải thích thêm trường hợp biên.', next_steps='Thử quantity = 0 và price = 0.')
                elif index == 1:
                    feedback['criteria'] = [{'name': 'Hiểu logic điều kiện', 'score': 4, 'comment': 'Giải thích đúng điều kiện tồn kho.'}, {'name': 'Giải thích luồng', 'score': 4, 'comment': 'Nêu đúng hàm tính tồn kho còn lại.'}]
                    criterion_id = ident('demo:criterion:logic')
                    insert('rubric_criteria', Id=criterion_id, Code='DEMO_LOGIC', Name='Hiểu logic', Statement='Giải thích đúng điều kiện kiểm tra và kết quả.', DisplayOrder=100)
                else:
                    feedback.update(code_evidence=code, logic_explanation='Điều kiện bảo vệ miền giá trị; percent = 100 trả về 0 trong công thức.')
                insert('student_answers', Id=answer_id, UserId=user_id, SubmissionId=submission_id, QuestionId=question_id,
                       AnswerText=suggested, FeedbackFormat=formats[index], AiFeedback=json.dumps(feedback, ensure_ascii=False),
                       CreatedAt='2026-10-17T03:15:00Z', StudentReply='[DEMO] Em sẽ bổ sung các trường hợp biên.' if index == 0 else None)
                if index == 1:
                    insert('rubric_scores', Id=ident(prefix+':score'), StudentAnswerId=answer_id, CriterionId=criterion_id, Score=4)
    sql.append('COMMIT;')
    sql.append(f'''SELECT u."Email", u."StudentCode", u."Role", r."RepoName", (SELECT count(*) FROM businesses b WHERE b."AnalysisRunId"=r."Id") AS businesses, (SELECT count(*) FROM generated_questions q JOIN businesses b ON b."Id"=q."BusinessId" WHERE b."AnalysisRunId"=r."Id") AS questions FROM users u JOIN analysis_runs r ON r."Id"=u."AnalysisRunId" WHERE u."Id"={literal(user_id)}::uuid;''')
    result = subprocess.run(command, input='\n'.join(sql), text=True, env=env, capture_output=True)
    if result.returncode:
        # psql errors can contain data but never the connection string or password.
        raise SystemExit(result.stderr)
    print(result.stdout[result.stdout.rfind(' Email'): ] if ' Email' in result.stdout else result.stdout)
    fixture = {'email': email, 'studentCode': student_code, 'userId': user_id,
               'analysisRunId': current_repo, 'historyAnalysisRunId': history_repo,
               'historySubmissionId': submission_id, 'submitRequest': {'answers': questions},
               'note': 'Synthetic demo fixtures; repository URLs are labels, not cloneable repositories. Google login must use the email above.'}
    target = ROOT / 'docs/fe-demo-fixture.json'
    target.write_text(json.dumps(fixture, ensure_ascii=False, indent=2)+'\n')
    print('Fixture:', target)

if __name__ == '__main__':
    main()
