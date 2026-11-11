CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE analysis_runs (
        "Id" uuid NOT NULL,
        "RepoUrl" character varying(500),
        "RepoName" character varying(255),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_analysis_runs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE rubric_criteria (
        "Id" uuid NOT NULL,
        "Code" character varying(50) NOT NULL,
        "Name" character varying(255) NOT NULL,
        "Statement" text NOT NULL,
        "DisplayOrder" integer NOT NULL,
        CONSTRAINT "PK_rubric_criteria" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE businesses (
        "Id" uuid NOT NULL,
        "AnalysisRunId" uuid NOT NULL,
        "BusinessName" text NOT NULL,
        CONSTRAINT "PK_businesses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_businesses_analysis_runs_AnalysisRunId" FOREIGN KEY ("AnalysisRunId") REFERENCES analysis_runs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "Email" character varying(255) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "AnalysisRunId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_users_StudentHasRepo" CHECK ("Role" = 'Admin' OR "AnalysisRunId" IS NOT NULL),
        CONSTRAINT "FK_users_analysis_runs_AnalysisRunId" FOREIGN KEY ("AnalysisRunId") REFERENCES analysis_runs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE generated_questions (
        "Id" uuid NOT NULL,
        "BusinessId" uuid NOT NULL,
        "QuestionText" text NOT NULL,
        "SuggestedAnswer" text,
        "Difficulty" character varying(20),
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_generated_questions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_generated_questions_businesses_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES businesses ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE student_answers (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "QuestionId" uuid NOT NULL,
        "AnswerText" text,
        "FeedbackFormat" character varying(20),
        "AiScore" double precision,
        "AiFeedback" text,
        "StudentReply" text,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_student_answers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_student_answers_generated_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES generated_questions ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_student_answers_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE TABLE rubric_scores (
        "Id" uuid NOT NULL,
        "StudentAnswerId" uuid NOT NULL,
        "CriterionId" uuid NOT NULL,
        "Score" integer NOT NULL,
        CONSTRAINT "PK_rubric_scores" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_rubric_scores_Score" CHECK ("Score" BETWEEN 1 AND 5),
        CONSTRAINT "FK_rubric_scores_rubric_criteria_CriterionId" FOREIGN KEY ("CriterionId") REFERENCES rubric_criteria ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_rubric_scores_student_answers_StudentAnswerId" FOREIGN KEY ("StudentAnswerId") REFERENCES student_answers ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000001', 'ACCURACY', 1, 'Độ chính xác kỹ thuật', 'Nhận xét đúng về mặt kỹ thuật so với code thực tế trong repo của nhóm tôi.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000002', 'FAIRNESS', 2, 'Công bằng', 'Điểm số và nhận xét phản ánh công bằng chất lượng câu trả lời của tôi.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000003', 'SPECIFICITY', 3, 'Cụ thể', 'Nhận xét chỉ rõ phần nào trong câu trả lời của tôi đúng, phần nào sai hoặc còn thiếu.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000004', 'ACTIONABILITY', 4, 'Có thể hành động', 'Sau khi đọc, tôi biết cụ thể cần làm gì để cải thiện câu trả lời.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000005', 'USEFULNESS', 5, 'Hữu ích', 'Nhận xét này giúp tôi hiểu rõ hơn luồng xử lý trong project của mình.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000006', 'CLARITY', 6, 'Rõ ràng', 'Nhận xét được trình bày rõ ràng, dễ hiểu.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000007', 'ACCEPTANCE', 7, 'Chấp nhận', 'Tôi đồng ý và chấp nhận nhận xét này.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000008', 'WILLINGNESS', 8, 'Sẵn sàng cải thiện', 'Tôi sẵn sàng sửa lại câu trả lời dựa trên nhận xét này.');
    INSERT INTO rubric_criteria ("Id", "Code", "DisplayOrder", "Name", "Statement")
    VALUES ('b2e1d3c5-0001-4d7f-8c2b-000000000009', 'AFFECT', 9, 'Cảm xúc', 'Đọc nhận xét này khiến tôi thấy được động viên, không bị nản.');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE INDEX "IX_businesses_AnalysisRunId" ON businesses ("AnalysisRunId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE INDEX "IX_generated_questions_BusinessId" ON generated_questions ("BusinessId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_rubric_criteria_Code" ON rubric_criteria ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE INDEX "IX_rubric_scores_CriterionId" ON rubric_scores ("CriterionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_rubric_scores_StudentAnswerId_CriterionId" ON rubric_scores ("StudentAnswerId", "CriterionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE INDEX "IX_student_answers_QuestionId" ON student_answers ("QuestionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_student_answers_UserId_QuestionId" ON student_answers ("UserId", "QuestionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE INDEX "IX_users_AnalysisRunId" ON users ("AnalysisRunId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013125910_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261013125910_InitialCreate', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    ALTER TABLE generated_questions ADD "ContextType" character varying(10) NOT NULL DEFAULT 'Raw';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE TABLE method_sources (
        "Id" uuid NOT NULL,
        "AnalysisRunId" uuid NOT NULL,
        "FilePath" character varying(500),
        "ClassName" character varying(255) NOT NULL,
        "MethodName" character varying(255) NOT NULL,
        "Parameters" text,
        "SourceCode" text NOT NULL,
        "Language" character varying(20) NOT NULL,
        "NodeType" character varying(30) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_method_sources" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_method_sources_analysis_runs_AnalysisRunId" FOREIGN KEY ("AnalysisRunId") REFERENCES analysis_runs ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE TABLE business_flows (
        "Id" uuid NOT NULL,
        "BusinessId" uuid NOT NULL,
        "EntryMethodId" uuid NOT NULL,
        "Name" character varying(500) NOT NULL,
        "MermaidGraph" text,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_business_flows" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_business_flows_businesses_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES businesses ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_business_flows_method_sources_EntryMethodId" FOREIGN KEY ("EntryMethodId") REFERENCES method_sources ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE TABLE business_methods (
        "BusinessId" uuid NOT NULL,
        "MethodSourceId" uuid NOT NULL,
        CONSTRAINT "PK_business_methods" PRIMARY KEY ("BusinessId", "MethodSourceId"),
        CONSTRAINT "FK_business_methods_businesses_BusinessId" FOREIGN KEY ("BusinessId") REFERENCES businesses ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_business_methods_method_sources_MethodSourceId" FOREIGN KEY ("MethodSourceId") REFERENCES method_sources ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE TABLE data_flow_edges (
        "Id" uuid NOT NULL,
        "FlowId" uuid NOT NULL,
        "StepOrder" integer NOT NULL,
        "FromMethodId" uuid NOT NULL,
        "ToMethodId" uuid NOT NULL,
        "EdgeType" character varying(10) NOT NULL,
        "ConditionContext" text,
        "DataLabel" text,
        CONSTRAINT "PK_data_flow_edges" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_data_flow_edges_business_flows_FlowId" FOREIGN KEY ("FlowId") REFERENCES business_flows ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_data_flow_edges_method_sources_FromMethodId" FOREIGN KEY ("FromMethodId") REFERENCES method_sources ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_data_flow_edges_method_sources_ToMethodId" FOREIGN KEY ("ToMethodId") REFERENCES method_sources ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE INDEX "IX_business_flows_BusinessId" ON business_flows ("BusinessId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE INDEX "IX_business_flows_EntryMethodId" ON business_flows ("EntryMethodId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE INDEX "IX_business_methods_MethodSourceId" ON business_methods ("MethodSourceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE UNIQUE INDEX "IX_data_flow_edges_FlowId_StepOrder" ON data_flow_edges ("FlowId", "StepOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE INDEX "IX_data_flow_edges_FromMethodId" ON data_flow_edges ("FromMethodId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE INDEX "IX_data_flow_edges_ToMethodId" ON data_flow_edges ("ToMethodId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    CREATE INDEX "IX_method_sources_AnalysisRunId_ClassName_MethodName" ON method_sources ("AnalysisRunId", "ClassName", "MethodName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013134643_AddSourceCodeAndDataFlow') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261013134643_AddSourceCodeAndDataFlow', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    DROP INDEX "IX_student_answers_UserId_QuestionId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    ALTER TABLE student_answers DROP COLUMN "AiScore";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    ALTER TABLE users ADD "StudentCode" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    ALTER TABLE student_answers ADD "SubmissionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    CREATE TABLE student_submissions (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "AnalysisRunId" uuid NOT NULL,
        "SubmittedAt" timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_student_submissions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_student_submissions_analysis_runs_AnalysisRunId" FOREIGN KEY ("AnalysisRunId") REFERENCES analysis_runs ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_student_submissions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    CREATE UNIQUE INDEX "IX_users_StudentCode" ON users ("StudentCode") WHERE "StudentCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    CREATE UNIQUE INDEX "IX_student_answers_SubmissionId_QuestionId" ON student_answers ("SubmissionId", "QuestionId") WHERE "SubmissionId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    CREATE INDEX "IX_student_answers_UserId" ON student_answers ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    CREATE INDEX "IX_student_submissions_AnalysisRunId" ON student_submissions ("AnalysisRunId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    CREATE INDEX "IX_student_submissions_UserId" ON student_submissions ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    ALTER TABLE student_answers ADD CONSTRAINT "FK_student_answers_student_submissions_SubmissionId" FOREIGN KEY ("SubmissionId") REFERENCES student_submissions ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014091624_UpdateStudentSubmissionSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014091624_UpdateStudentSubmissionSchema', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014164311_AddAnalysisRunBusinessTemplate') THEN
    ALTER TABLE analysis_runs ADD "BusinessTemplateJson" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014164311_AddAnalysisRunBusinessTemplate') THEN
    ALTER TABLE analysis_runs ADD "TemplateSource" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014164311_AddAnalysisRunBusinessTemplate') THEN
    ALTER TABLE analysis_runs ADD "TemplateUpdatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014164311_AddAnalysisRunBusinessTemplate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014164311_AddAnalysisRunBusinessTemplate', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017144245_AddCreatedAtToStudentSubmission') THEN
    ALTER TABLE student_submissions ALTER COLUMN "SubmittedAt" DROP NOT NULL;
    ALTER TABLE student_submissions ALTER COLUMN "SubmittedAt" DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017144245_AddCreatedAtToStudentSubmission') THEN
    ALTER TABLE student_submissions ADD "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017144245_AddCreatedAtToStudentSubmission') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017144245_AddCreatedAtToStudentSubmission', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    ALTER TABLE users DROP CONSTRAINT "CK_users_StudentHasRepo";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE TABLE audit_events (
        "Id" uuid NOT NULL,
        "ActorId" uuid NOT NULL,
        "ActorName" character varying(255) NOT NULL,
        "ResourceId" uuid NOT NULL,
        "EventType" character varying(80) NOT NULL,
        "Details" character varying(4000) NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_audit_events" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE TABLE courses (
        "Id" uuid NOT NULL,
        "Code" character varying(40) NOT NULL,
        "Name" character varying(255) NOT NULL,
        "Semester" character varying(40) NOT NULL,
        "LecturerId" uuid NOT NULL,
        CONSTRAINT "PK_courses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_courses_users_LecturerId" FOREIGN KEY ("LecturerId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE TABLE course_projects (
        "AnalysisRunId" uuid NOT NULL,
        "CourseId" uuid NOT NULL,
        "IsApproved" boolean,
        "Notes" character varying(4000),
        "DecidedAt" timestamp with time zone,
        CONSTRAINT "PK_course_projects" PRIMARY KEY ("AnalysisRunId"),
        CONSTRAINT "FK_course_projects_analysis_runs_AnalysisRunId" FOREIGN KEY ("AnalysisRunId") REFERENCES analysis_runs ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_course_projects_courses_CourseId" FOREIGN KEY ("CourseId") REFERENCES courses ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE TABLE assessment_sessions (
        "Id" uuid NOT NULL,
        "ProjectId" uuid NOT NULL,
        "Title" character varying(255) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "ScheduledStart" timestamp with time zone NOT NULL,
        "ScheduledEnd" timestamp with time zone NOT NULL,
        "ActualStartTime" timestamp with time zone,
        "ActualEndTime" timestamp with time zone,
        "RubricJson" text NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_assessment_sessions" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_session_status" CHECK ("Status" IN ('CREATED','IN_PROGRESS','COMPLETED')),
        CONSTRAINT "FK_assessment_sessions_course_projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES course_projects ("AnalysisRunId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE TABLE session_students (
        "SessionId" uuid NOT NULL,
        "StudentId" uuid NOT NULL,
        "EvaluationJson" text,
        "EvaluatorId" uuid,
        "EvaluatedAt" timestamp with time zone,
        CONSTRAINT "PK_session_students" PRIMARY KEY ("SessionId", "StudentId"),
        CONSTRAINT "FK_session_students_assessment_sessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES assessment_sessions ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_session_students_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    ALTER TABLE users ADD CONSTRAINT "CK_users_StudentHasRepo" CHECK ("Role" IN ('Admin', 'Lecturer') OR "AnalysisRunId" IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE INDEX "IX_assessment_sessions_ProjectId_ScheduledStart" ON assessment_sessions ("ProjectId", "ScheduledStart");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE INDEX "IX_audit_events_ResourceId_Timestamp" ON audit_events ("ResourceId", "Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE INDEX "IX_course_projects_CourseId" ON course_projects ("CourseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE UNIQUE INDEX "IX_courses_Code_Semester_LecturerId" ON courses ("Code", "Semester", "LecturerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE INDEX "IX_courses_LecturerId" ON courses ("LecturerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    CREATE INDEX "IX_session_students_StudentId" ON session_students ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017173036_TeachingWorkflows') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017173036_TeachingWorkflows', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017174201_AnalysisProvenance') THEN
    ALTER TABLE analysis_runs ADD "AnalyzedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017174201_AnalysisProvenance') THEN
    ALTER TABLE analysis_runs ADD "AnalyzedCommitSha" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017174201_AnalysisProvenance') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017174201_AnalysisProvenance', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021010006_AllowStudentsWithoutRepository') THEN
    ALTER TABLE users DROP CONSTRAINT "CK_users_StudentHasRepo";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021010006_AllowStudentsWithoutRepository') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021010006_AllowStudentsWithoutRepository', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021012445_ArchiveRepositoryAnalysisHistory') THEN
    ALTER TABLE analysis_runs ADD "AnalyzedRepoUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021012445_ArchiveRepositoryAnalysisHistory') THEN
    ALTER TABLE analysis_runs ADD "IsArchived" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021012445_ArchiveRepositoryAnalysisHistory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021012445_ArchiveRepositoryAnalysisHistory', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    ALTER TABLE users ADD address text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    ALTER TABLE users ADD full_name character varying(255) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    ALTER TABLE users ADD phone character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    ALTER TABLE courses ADD credits integer NOT NULL DEFAULT 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE lecturer_profiles (
        lecturer_code character varying(20) NOT NULL,
        user_id uuid NOT NULL,
        academic_title character varying(100) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_lecturer_profiles" PRIMARY KEY (lecturer_code),
        CONSTRAINT "FK_lecturer_profiles_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE rooms (
        id uuid NOT NULL,
        code character varying(50) NOT NULL,
        building character varying(100),
        CONSTRAINT "PK_rooms" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE semesters (
        id uuid NOT NULL,
        name character varying(100) NOT NULL,
        academic_year character varying(20) NOT NULL,
        start_date date NOT NULL,
        actual_start_date date NOT NULL,
        end_date date NOT NULL,
        CONSTRAINT "PK_semesters" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE student_profiles (
        student_code character varying(20) NOT NULL,
        user_id uuid NOT NULL,
        enrollment_year integer NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_student_profiles" PRIMARY KEY (student_code),
        CONSTRAINT "FK_student_profiles_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE time_slots (
        id uuid NOT NULL,
        period_number integer NOT NULL,
        start_time time without time zone NOT NULL,
        end_time time without time zone NOT NULL,
        CONSTRAINT "PK_time_slots" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE classes (
        id uuid NOT NULL,
        course_id uuid NOT NULL,
        semester_id uuid,
        name character varying(100),
        start_date date NOT NULL,
        actual_start_date date NOT NULL,
        end_date date NOT NULL,
        CONSTRAINT "PK_classes" PRIMARY KEY (id),
        CONSTRAINT "FK_classes_courses_course_id" FOREIGN KEY (course_id) REFERENCES courses ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_classes_semesters_semester_id" FOREIGN KEY (semester_id) REFERENCES semesters (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE class_schedules (
        id uuid NOT NULL,
        class_id uuid NOT NULL,
        lecturer_code character varying(20) NOT NULL,
        room_id uuid NOT NULL,
        time_slot_id uuid NOT NULL,
        day_of_week integer NOT NULL,
        valid_from date NOT NULL,
        valid_to date NOT NULL,
        CONSTRAINT "PK_class_schedules" PRIMARY KEY (id),
        CONSTRAINT "FK_class_schedules_classes_class_id" FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE,
        CONSTRAINT "FK_class_schedules_lecturer_profiles_lecturer_code" FOREIGN KEY (lecturer_code) REFERENCES lecturer_profiles (lecturer_code) ON DELETE RESTRICT,
        CONSTRAINT "FK_class_schedules_rooms_room_id" FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_class_schedules_time_slots_time_slot_id" FOREIGN KEY (time_slot_id) REFERENCES time_slots (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE groups (
        id uuid NOT NULL,
        class_id uuid NOT NULL,
        name character varying(100) NOT NULL,
        topic_name character varying(255),
        analysis_run_id uuid,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_groups" PRIMARY KEY (id),
        CONSTRAINT "FK_groups_analysis_runs_analysis_run_id" FOREIGN KEY (analysis_run_id) REFERENCES analysis_runs ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_groups_classes_class_id" FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE oral_exams (
        id uuid NOT NULL,
        class_id uuid NOT NULL,
        title character varying(255) NOT NULL,
        description text,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_oral_exams" PRIMARY KEY (id),
        CONSTRAINT "FK_oral_exams_classes_class_id" FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE student_classes (
        student_code character varying(20) NOT NULL,
        class_id uuid NOT NULL,
        status integer NOT NULL DEFAULT 1,
        CONSTRAINT "PK_student_classes" PRIMARY KEY (student_code, class_id),
        CONSTRAINT "FK_student_classes_classes_class_id" FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE,
        CONSTRAINT "FK_student_classes_student_profiles_student_code" FOREIGN KEY (student_code) REFERENCES student_profiles (student_code) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE class_sessions (
        id uuid NOT NULL,
        class_id uuid NOT NULL,
        schedule_id uuid,
        learn_date date NOT NULL,
        time_slot_id uuid NOT NULL,
        room_id uuid NOT NULL,
        lecturer_code character varying(20) NOT NULL,
        is_cancelled boolean NOT NULL DEFAULT FALSE,
        note text,
        CONSTRAINT "PK_class_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_class_sessions_class_schedules_schedule_id" FOREIGN KEY (schedule_id) REFERENCES class_schedules (id) ON DELETE SET NULL,
        CONSTRAINT "FK_class_sessions_classes_class_id" FOREIGN KEY (class_id) REFERENCES classes (id) ON DELETE CASCADE,
        CONSTRAINT "FK_class_sessions_lecturer_profiles_lecturer_code" FOREIGN KEY (lecturer_code) REFERENCES lecturer_profiles (lecturer_code) ON DELETE RESTRICT,
        CONSTRAINT "FK_class_sessions_rooms_room_id" FOREIGN KEY (room_id) REFERENCES rooms (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_class_sessions_time_slots_time_slot_id" FOREIGN KEY (time_slot_id) REFERENCES time_slots (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE group_members (
        group_id uuid NOT NULL,
        student_code character varying(20) NOT NULL,
        role character varying(50) NOT NULL DEFAULT 'Member',
        joined_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_group_members" PRIMARY KEY (group_id, student_code),
        CONSTRAINT "FK_group_members_groups_group_id" FOREIGN KEY (group_id) REFERENCES groups (id) ON DELETE CASCADE,
        CONSTRAINT "FK_group_members_student_profiles_student_code" FOREIGN KEY (student_code) REFERENCES student_profiles (student_code) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE oral_questions (
        id uuid NOT NULL,
        oral_exam_id uuid NOT NULL,
        generated_question_id uuid,
        content text NOT NULL,
        reference_context text,
        expected_keywords text,
        CONSTRAINT "PK_oral_questions" PRIMARY KEY (id),
        CONSTRAINT "FK_oral_questions_generated_questions_generated_question_id" FOREIGN KEY (generated_question_id) REFERENCES generated_questions ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_oral_questions_oral_exams_oral_exam_id" FOREIGN KEY (oral_exam_id) REFERENCES oral_exams (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE student_oral_sessions (
        id uuid NOT NULL,
        oral_exam_id uuid NOT NULL,
        student_code character varying(20) NOT NULL,
        examiner_code character varying(20),
        start_time timestamp with time zone NOT NULL,
        end_time timestamp with time zone,
        audio_record_url text,
        screen_record_url text,
        ai_evaluation_summary text,
        final_feedback text,
        CONSTRAINT "PK_student_oral_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_student_oral_sessions_lecturer_profiles_examiner_code" FOREIGN KEY (examiner_code) REFERENCES lecturer_profiles (lecturer_code) ON DELETE RESTRICT,
        CONSTRAINT "FK_student_oral_sessions_oral_exams_oral_exam_id" FOREIGN KEY (oral_exam_id) REFERENCES oral_exams (id) ON DELETE CASCADE,
        CONSTRAINT "FK_student_oral_sessions_student_profiles_student_code" FOREIGN KEY (student_code) REFERENCES student_profiles (student_code) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE attendance (
        id uuid NOT NULL,
        session_id uuid NOT NULL,
        student_code character varying(20) NOT NULL,
        status integer NOT NULL,
        recorded_at timestamp with time zone,
        comments text,
        CONSTRAINT "PK_attendance" PRIMARY KEY (id),
        CONSTRAINT "FK_attendance_class_sessions_session_id" FOREIGN KEY (session_id) REFERENCES class_sessions (id) ON DELETE CASCADE,
        CONSTRAINT "FK_attendance_student_profiles_student_code" FOREIGN KEY (student_code) REFERENCES student_profiles (student_code) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE TABLE student_oral_answers (
        id uuid NOT NULL,
        session_id uuid NOT NULL,
        question_id uuid NOT NULL,
        student_answer text,
        feedback text,
        CONSTRAINT "PK_student_oral_answers" PRIMARY KEY (id),
        CONSTRAINT "FK_student_oral_answers_oral_questions_question_id" FOREIGN KEY (question_id) REFERENCES oral_questions (id) ON DELETE CASCADE,
        CONSTRAINT "FK_student_oral_answers_student_oral_sessions_session_id" FOREIGN KEY (session_id) REFERENCES student_oral_sessions (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_attendance_session_id" ON attendance (session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_attendance_student_code" ON attendance (student_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_schedules_class_id" ON class_schedules (class_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_schedules_lecturer_code" ON class_schedules (lecturer_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_schedules_room_id" ON class_schedules (room_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_schedules_time_slot_id" ON class_schedules (time_slot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_sessions_class_id" ON class_sessions (class_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_sessions_lecturer_code" ON class_sessions (lecturer_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_sessions_room_id" ON class_sessions (room_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_sessions_schedule_id" ON class_sessions (schedule_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_class_sessions_time_slot_id" ON class_sessions (time_slot_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_classes_course_id" ON classes (course_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_classes_semester_id" ON classes (semester_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_group_members_student_code" ON group_members (student_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE UNIQUE INDEX "IX_groups_analysis_run_id" ON groups (analysis_run_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_groups_class_id" ON groups (class_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE UNIQUE INDEX "IX_lecturer_profiles_user_id" ON lecturer_profiles (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_oral_exams_class_id" ON oral_exams (class_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_oral_questions_generated_question_id" ON oral_questions (generated_question_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_oral_questions_oral_exam_id" ON oral_questions (oral_exam_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE UNIQUE INDEX "IX_rooms_code" ON rooms (code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_student_classes_class_id" ON student_classes (class_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_student_oral_answers_question_id" ON student_oral_answers (question_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_student_oral_answers_session_id" ON student_oral_answers (session_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_student_oral_sessions_examiner_code" ON student_oral_sessions (examiner_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_student_oral_sessions_oral_exam_id" ON student_oral_sessions (oral_exam_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE INDEX "IX_student_oral_sessions_student_code" ON student_oral_sessions (student_code);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    CREATE UNIQUE INDEX "IX_student_profiles_user_id" ON student_profiles (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111122558_UpdateDbSchemaTo2026') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111122558_UpdateDbSchemaTo2026', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111125115_DropRubricTables') THEN
    DROP TABLE rubric_scores;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111125115_DropRubricTables') THEN
    DROP TABLE rubric_criteria;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111125115_DropRubricTables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111125115_DropRubricTables', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111164543_AddPasswordHashToUser') THEN
    ALTER TABLE users ADD password_hash character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111164543_AddPasswordHashToUser') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111164543_AddPasswordHashToUser', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111170335_AddRefreshTokenToUser') THEN
    ALTER TABLE users ADD refresh_token character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111170335_AddRefreshTokenToUser') THEN
    ALTER TABLE users ADD refresh_token_expiry_time timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111170335_AddRefreshTokenToUser') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111170335_AddRefreshTokenToUser', '9.0.4');
    END IF;
END $EF$;
COMMIT;

