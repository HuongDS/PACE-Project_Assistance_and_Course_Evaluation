namespace PACE.Infrastructure.Persistence;

/// <summary>SQL tạo bảng syllabus của môn, dùng chung cho migration và bước khởi động. Chạy lại không đổi gì.</summary>
public static class CourseSyllabusSchema
{
    public const string Up = @"
                CREATE TABLE IF NOT EXISTS course_syllabi (
                    course_id uuid NOT NULL,
                    source_id character varying(64),
                    name character varying(255) NOT NULL,
                    content jsonb NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_course_syllabi"" PRIMARY KEY (course_id),
                    CONSTRAINT ""FK_course_syllabi_courses_course_id""
                        FOREIGN KEY (course_id) REFERENCES courses(""Id"") ON DELETE CASCADE
                );
            ";
}
