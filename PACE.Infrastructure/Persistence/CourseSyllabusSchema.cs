namespace PACE.Infrastructure.Persistence;

/// <summary>
/// SQL tạo bảng syllabus theo kỳ, dùng chung cho migration và bước khởi động. Chạy lại không đổi gì.
/// Bảng bản đầu (mỗi môn một syllabus) được nâng cấp tại chỗ: syllabus cũ áp dụng từ kỳ sớm nhất.
/// </summary>
public static class CourseSyllabusSchema
{
    public const string Up = @"
                CREATE TABLE IF NOT EXISTS course_syllabi (
                    id uuid NOT NULL,
                    course_id uuid NOT NULL,
                    semester_id uuid NOT NULL,
                    source_id character varying(64),
                    name character varying(255) NOT NULL,
                    content jsonb NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_course_syllabi"" PRIMARY KEY (id),
                    CONSTRAINT ""FK_course_syllabi_courses_course_id""
                        FOREIGN KEY (course_id) REFERENCES courses(""Id"") ON DELETE CASCADE
                );
                ALTER TABLE course_syllabi ADD COLUMN IF NOT EXISTS id uuid;
                ALTER TABLE course_syllabi ADD COLUMN IF NOT EXISTS semester_id uuid;
                UPDATE course_syllabi SET id = md5(course_id::text)::uuid WHERE id IS NULL;
                UPDATE course_syllabi SET semester_id = (SELECT id FROM semesters ORDER BY start_date LIMIT 1) WHERE semester_id IS NULL;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_index i JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
                               WHERE i.indrelid = 'course_syllabi'::regclass AND i.indisprimary AND a.attname = 'course_id') THEN
                        ALTER TABLE course_syllabi DROP CONSTRAINT ""PK_course_syllabi"";
                        ALTER TABLE course_syllabi ALTER COLUMN id SET NOT NULL;
                        ALTER TABLE course_syllabi ADD CONSTRAINT ""PK_course_syllabi"" PRIMARY KEY (id);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_course_syllabi_semesters_semester_id') THEN
                        ALTER TABLE course_syllabi ALTER COLUMN semester_id SET NOT NULL;
                        ALTER TABLE course_syllabi ADD CONSTRAINT ""FK_course_syllabi_semesters_semester_id""
                            FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE RESTRICT;
                    END IF;
                END $$;
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_course_syllabi_course_id_semester_id"" ON course_syllabi(course_id, semester_id);
                CREATE INDEX IF NOT EXISTS ""IX_course_syllabi_semester_id"" ON course_syllabi(semester_id);
            ";
}
