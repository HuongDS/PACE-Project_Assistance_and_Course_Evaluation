namespace PACE.Infrastructure.Persistence;

/// <summary>SQL thêm cột kỳ cho đề tài mẫu, dùng chung cho migration và bước khởi động. Chạy lại không đổi gì.</summary>
public static class CourseTopicSemesterSchema
{
    public const string Up = @"
                ALTER TABLE course_topics ADD COLUMN IF NOT EXISTS semester_id uuid;
                CREATE INDEX IF NOT EXISTS ""IX_course_topics_semester_id"" ON course_topics(semester_id);
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_course_topics_semesters_semester_id') THEN
                        ALTER TABLE course_topics
                        ADD CONSTRAINT ""FK_course_topics_semesters_semester_id""
                        FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE SET NULL;
                        -- Chỉ chạy một lần khi vừa thêm cột: gán đề tài cũ vào kỳ của nhóm đang chọn, không có thì kỳ mới nhất.
                        UPDATE course_topics t SET semester_id = COALESCE(
                            (SELECT c.semester_id FROM groups g JOIN classes c ON c.id = g.class_id JOIN semesters s ON s.id = c.semester_id
                              WHERE g.course_topic_id = t.id ORDER BY s.start_date DESC LIMIT 1),
                            (SELECT id FROM semesters ORDER BY start_date DESC LIMIT 1))
                        WHERE t.semester_id IS NULL;
                    END IF;
                END $$;
            ";
}
