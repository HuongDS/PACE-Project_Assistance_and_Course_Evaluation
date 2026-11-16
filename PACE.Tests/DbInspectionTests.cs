using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PACE.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace PACE.Tests;

public class DbInspectionTests
{
    private readonly ITestOutputHelper _output;

    public DbInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("PACE_TEST_POSTGRES")
        ?? throw new InvalidOperationException("Set PACE_TEST_POSTGRES to a disposable PostgreSQL database migrated with EF Core.");

    [Fact]
    public async Task DropRubricTables()
    {
        var connStr = ConnectionString;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        await using var dropCmd = new NpgsqlCommand(@"
            DROP TABLE IF EXISTS rubric_scores CASCADE;
            DROP TABLE IF EXISTS rubric_criteria CASCADE;", conn);
        await dropCmd.ExecuteNonQueryAsync();

        _output.WriteLine("Dropped rubric_scores and rubric_criteria tables successfully!");

        // Verify they are gone
        await using var checkCmd = new NpgsqlCommand(@"
            SELECT table_name FROM information_schema.tables 
            WHERE table_schema = 'public' AND table_name IN ('rubric_criteria', 'rubric_scores');", conn);
        await using (var reader = await checkCmd.ExecuteReaderAsync())
        {
            var remaining = new List<string>();
            while (await reader.ReadAsync())
            {
                remaining.Add(reader.GetString(0));
            }
            Assert.Empty(remaining);
        }

        await using var migCmd = new NpgsqlCommand(@"
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20261111125115_DropRubricTables', '9.0.4')
            ON CONFLICT (""MigrationId"") DO NOTHING;", conn);
        await migCmd.ExecuteNonQueryAsync();

        _output.WriteLine("Remaining rubric tables in DB: 0. Migration recorded successfully!");
    }

    [Fact]
    public async Task SeedLecturerAccount()
    {
        var connStr = ConnectionString;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("PacePassword#2026");
        var lecturerUserId = Guid.NewGuid();

        // 1. Check if lecturer exists
        await using var checkUserCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE LOWER(\"Email\") IN ('lecturer@pace.edu.vn', 'lecturer')", conn);
        var existingId = await checkUserCmd.ExecuteScalarAsync();

        if (existingId != null && existingId != DBNull.Value)
        {
            lecturerUserId = (Guid)existingId;
            await using var updateUserCmd = new NpgsqlCommand(@"
                UPDATE users 
                SET ""Role"" = 'Lecturer', 
                    ""full_name"" = 'Thầy Nguyễn Văn Giảng', 
                    ""password_hash"" = @hash 
                WHERE ""Id"" = @id", conn);
            updateUserCmd.Parameters.AddWithValue("hash", passwordHash);
            updateUserCmd.Parameters.AddWithValue("id", lecturerUserId);
            await updateUserCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"Updated existing lecturer account: {lecturerUserId}");
        }
        else
        {
            await using var insertUserCmd = new NpgsqlCommand(@"
                INSERT INTO users (""Id"", ""Email"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                VALUES (@id, 'lecturer@pace.edu.vn', 'Thầy Nguyễn Văn Giảng', 'Lecturer', @hash, now())", conn);
            insertUserCmd.Parameters.AddWithValue("id", lecturerUserId);
            insertUserCmd.Parameters.AddWithValue("hash", passwordHash);
            await insertUserCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"Created new lecturer account: {lecturerUserId}");
        }

        // 2. Ensure lecturer_profiles
        await using var profileCmd = new NpgsqlCommand(@"
            INSERT INTO lecturer_profiles (lecturer_code, user_id, academic_title, created_at)
            VALUES ('GV001', @uid, 'Thạc sĩ / Giảng viên', now())
            ON CONFLICT (lecturer_code) DO UPDATE 
            SET user_id = EXCLUDED.user_id, academic_title = EXCLUDED.academic_title", conn);
        profileCmd.Parameters.AddWithValue("uid", lecturerUserId);
        await profileCmd.ExecuteNonQueryAsync();
        _output.WriteLine("Upserted lecturer_profiles record for GV001");

        // 3. Link with Course, Class and AnalysisRun so the lecturer has real data immediately
        await using var getRepoCmd = new NpgsqlCommand("SELECT \"Id\" FROM analysis_runs WHERE NOT \"IsArchived\" LIMIT 1", conn);
        var repoIdObj = await getRepoCmd.ExecuteScalarAsync();

        if (repoIdObj != null && repoIdObj != DBNull.Value)
        {
            var repoId = (Guid)repoIdObj;

            // Ensure Course exists
            var courseId = Guid.NewGuid();
            await using var getCourseCmd = new NpgsqlCommand("SELECT \"Id\" FROM courses LIMIT 1", conn);
            var existingCourse = await getCourseCmd.ExecuteScalarAsync();

            if (existingCourse != null && existingCourse != DBNull.Value)
            {
                courseId = (Guid)existingCourse;
            }
            else
            {
                await using var insertCourseCmd = new NpgsqlCommand(@"
                    INSERT INTO courses (""Id"", ""Code"", ""Name"", ""Credits"")
                    VALUES (@cid, 'PRN231', 'Building Cross-Platform Web Applications with .NET', 3)", conn);
                insertCourseCmd.Parameters.AddWithValue("cid", courseId);
                await insertCourseCmd.ExecuteNonQueryAsync();
            }

            // Ensure lecturer_id column exists on classes
            await using var ensureColCmd = new NpgsqlCommand(@"
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS lecturer_id uuid;
                CREATE INDEX IF NOT EXISTS ""IX_classes_lecturer_id"" ON classes(lecturer_id);
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'courses' AND column_name = 'LecturerId') THEN
                        UPDATE classes SET lecturer_id = c.""LecturerId"" FROM courses c WHERE classes.course_id = c.""Id"" AND classes.lecturer_id IS NULL;
                    END IF;
                END $$;", conn);
            await ensureColCmd.ExecuteNonQueryAsync();

            // Ensure Class exists
            var classId = Guid.NewGuid();
            await using var getClassCmd = new NpgsqlCommand("SELECT id FROM classes LIMIT 1", conn);
            var existingClass = await getClassCmd.ExecuteScalarAsync();

            if (existingClass != null && existingClass != DBNull.Value)
            {
                classId = (Guid)existingClass;
                await using var updateClassCmd = new NpgsqlCommand("UPDATE classes SET course_id = @cid, lecturer_id = @lid, name = COALESCE(name, 'SE1701') WHERE id = @clid", conn);
                updateClassCmd.Parameters.AddWithValue("cid", courseId);
                updateClassCmd.Parameters.AddWithValue("lid", lecturerUserId);
                updateClassCmd.Parameters.AddWithValue("clid", classId);
                await updateClassCmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insertClassCmd = new NpgsqlCommand(@"
                    INSERT INTO classes (id, course_id, lecturer_id, name, start_date, actual_start_date, end_date)
                    VALUES (@clid, @cid, @lid, 'SE1701', '2026-09-01', '2026-09-01', '2026-12-31')", conn);
                insertClassCmd.Parameters.AddWithValue("clid", classId);
                insertClassCmd.Parameters.AddWithValue("cid", courseId);
                insertClassCmd.Parameters.AddWithValue("lid", lecturerUserId);
                await insertClassCmd.ExecuteNonQueryAsync();
            }

            // Ensure Group exists and points to this repo and class (idempotent, avoid duplicates)
            await using var checkGroupCmd = new NpgsqlCommand("SELECT id FROM groups WHERE class_id = @clid LIMIT 1", conn);
            checkGroupCmd.Parameters.AddWithValue("clid", classId);
            var existingGid = await checkGroupCmd.ExecuteScalarAsync();
            Guid gid;

            if (existingGid != null && existingGid != DBNull.Value)
            {
                gid = (Guid)existingGid;
            }
            else
            {
                gid = Guid.NewGuid();
                await using var groupCmd = new NpgsqlCommand(@"
                    INSERT INTO groups (id, class_id, name, topic_name, created_at)
                    VALUES (@gid, @clid, 'Nhóm 01', 'Hệ thống Quản lý và Đánh giá Khóa học (PACE)', now())", conn);
                groupCmd.Parameters.AddWithValue("gid", gid);
                groupCmd.Parameters.AddWithValue("clid", classId);
                await groupCmd.ExecuteNonQueryAsync();
            }

            await using var updRunCmd = new NpgsqlCommand(@"
                UPDATE analysis_runs SET group_id = @gid WHERE ""Id"" = @rid", conn);
            updRunCmd.Parameters.AddWithValue("gid", gid);
            updRunCmd.Parameters.AddWithValue("rid", repoId);
            await updRunCmd.ExecuteNonQueryAsync();

            // Ensure CourseProject exists
            await using var cpCmd = new NpgsqlCommand(@"
                INSERT INTO course_projects (""AnalysisRunId"", ""CourseId"")
                VALUES (@rid, @cid)
                ON CONFLICT (""AnalysisRunId"") DO UPDATE
                SET ""CourseId"" = EXCLUDED.""CourseId""", conn);
            cpCmd.Parameters.AddWithValue("rid", repoId);
            cpCmd.Parameters.AddWithValue("cid", courseId);
            await cpCmd.ExecuteNonQueryAsync();

            _output.WriteLine($"Successfully linked repo {repoId} with Course {courseId}, Class {classId}, Group, and Lecturer {lecturerUserId}!");
        }
    }

    [Fact]
    public async Task SeedClassAndProjectData()
    {
        var connStr = ConnectionString;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. Semester FA26
        var semesterId = Guid.NewGuid();
        await using var semCmd = new NpgsqlCommand("SELECT id FROM semesters WHERE name = 'FA26' LIMIT 1", conn);
        var semObj = await semCmd.ExecuteScalarAsync();
        if (semObj != null && semObj != DBNull.Value)
        {
            semesterId = (Guid)semObj;
        }
        else
        {
            await using var insSem = new NpgsqlCommand(@"
                INSERT INTO semesters (id, name, academic_year, start_date, actual_start_date, end_date)
                VALUES (@id, 'FA26', '2026-2027', '2026-09-01', '2026-09-01', '2026-12-31')", conn);
            insSem.Parameters.AddWithValue("id", semesterId);
            await insSem.ExecuteNonQueryAsync();
        }

        // 2. Lecturer
        await using var lecCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE LOWER(\"Email\") IN ('lecturer@pace.edu.vn', 'lecturer') LIMIT 1", conn);
        var lecObj = await lecCmd.ExecuteScalarAsync();
        Guid lecturerId = lecObj != null && lecObj != DBNull.Value ? (Guid)lecObj : Guid.NewGuid();

        // 3. Main Student: student@pace.edu.vn
        var studentId = Guid.NewGuid();
        await using var stuCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE LOWER(\"Email\") = 'student@pace.edu.vn' LIMIT 1", conn);
        var stuObj = await stuCmd.ExecuteScalarAsync();
        if (stuObj != null && stuObj != DBNull.Value)
        {
            studentId = (Guid)stuObj;
            await using var updStu = new NpgsqlCommand(@"
                UPDATE users SET ""StudentCode"" = 'SE170001', ""full_name"" = 'Nguyễn Văn Sinh Viên'
                WHERE ""Id"" = @id", conn);
            updStu.Parameters.AddWithValue("id", studentId);
            await updStu.ExecuteNonQueryAsync();
        }
        else
        {
            var pwHash = BCrypt.Net.BCrypt.HashPassword("PacePassword#2026");
            await using var insStu = new NpgsqlCommand(@"
                INSERT INTO users (""Id"", ""Email"", ""StudentCode"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                VALUES (@id, 'student@pace.edu.vn', 'SE170001', 'Nguyễn Văn Sinh Viên', 'Student', @hash, now())", conn);
            insStu.Parameters.AddWithValue("id", studentId);
            insStu.Parameters.AddWithValue("hash", pwHash);
            await insStu.ExecuteNonQueryAsync();
        }

        // Student Profile SE170001
        await using var profCmd = new NpgsqlCommand(@"
            INSERT INTO student_profiles (student_code, user_id, enrollment_year, created_at)
            VALUES ('SE170001', @uid, 2023, now())
            ON CONFLICT (student_code) DO UPDATE SET user_id = EXCLUDED.user_id", conn);
        profCmd.Parameters.AddWithValue("uid", studentId);
        await profCmd.ExecuteNonQueryAsync();

        // Extra students for rich test data
        var sampleStudents = new[]
        {
            ("SE170002", "Trần Thị Mai", "mai.tt@fpt.edu.vn"),
            ("SE170003", "Lê Hoàng Nam", "nam.lh@fpt.edu.vn"),
            ("SE170004", "Phạm Quốc Bảo", "bao.pq@fpt.edu.vn"),
            ("SE170005", "Võ Minh Thư", "thu.vm@fpt.edu.vn"),
            ("SE170006", "Đặng Quang Huy", "huy.dq@fpt.edu.vn"),
            ("SE170007", "Ngô Bảo Ngọc", "ngoc.nb@fpt.edu.vn"),
            ("SE170008", "Hoàng Văn Đức", "duc.hv@fpt.edu.vn")
        };

        foreach (var (code, name, email) in sampleStudents)
        {
            var uid = Guid.NewGuid();
            await using var checkCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE \"StudentCode\" = @c OR LOWER(\"Email\") = @e LIMIT 1", conn);
            checkCmd.Parameters.AddWithValue("c", code);
            checkCmd.Parameters.AddWithValue("e", email);
            var exUid = await checkCmd.ExecuteScalarAsync();
            if (exUid != null && exUid != DBNull.Value) uid = (Guid)exUid;
            else
            {
                var defHash = BCrypt.Net.BCrypt.HashPassword("PacePassword#2026");
                await using var insUser = new NpgsqlCommand(@"
                    INSERT INTO users (""Id"", ""Email"", ""StudentCode"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                    VALUES (@uid, @email, @code, @name, 'Student', @hash, now())", conn);
                insUser.Parameters.AddWithValue("uid", uid);
                insUser.Parameters.AddWithValue("email", email);
                insUser.Parameters.AddWithValue("code", code);
                insUser.Parameters.AddWithValue("name", name);
                insUser.Parameters.AddWithValue("hash", defHash);
                await insUser.ExecuteNonQueryAsync();
            }

            await using var insProf = new NpgsqlCommand(@"
                INSERT INTO student_profiles (student_code, user_id, enrollment_year, created_at)
                VALUES (@code, @uid, 2023, now())
                ON CONFLICT (student_code) DO UPDATE SET user_id = EXCLUDED.user_id", conn);
            insProf.Parameters.AddWithValue("code", code);
            insProf.Parameters.AddWithValue("uid", uid);
            await insProf.ExecuteNonQueryAsync();
        }

        // 4. Create 3 Courses & Classes
        var coursesData = new[]
        {
            ("PRN231", "Building Cross-Platform Web Applications with .NET", "SE1701", "Nhóm 01 - PACE Core Team", "Hệ thống Quản lý và Đánh giá Khóa học (PACE)", "https://github.com/HuongDS/PACE-Project_Assistance_and_Course_Evaluation/tree/main/mock-data-test/mock-project"),
            ("SWP391", "Software Development Project", "SE1702", "Nhóm 03 - E-Commerce AI", "Nền tảng thương mại điện tử tích hợp trợ lý AI gợi ý sản phẩm", "https://github.com/HuongDS/ecommerce-ai-platform"),
            ("PRM392", "Mobile Programming (Android & Flutter)", "SE1703", "Nhóm 02 - Smart Campus Mobile", "Ứng dụng điểm danh và quản lý học tập Smart Campus", "https://github.com/HuongDS/smart-campus-mobile-app")
        };

        int courseIndex = 0;
        foreach (var (cCode, cName, clName, grpName, topic, repoUrl) in coursesData)
        {
            courseIndex++;
            var cid = Guid.NewGuid();
            await using var getCCmd = new NpgsqlCommand("SELECT \"Id\" FROM courses WHERE \"Code\" = @code LIMIT 1", conn);
            getCCmd.Parameters.AddWithValue("code", cCode);
            var exC = await getCCmd.ExecuteScalarAsync();
            if (exC != null && exC != DBNull.Value) cid = (Guid)exC;
            else
            {
                await using var insC = new NpgsqlCommand(@"
                    INSERT INTO courses (""Id"", ""Code"", ""Name"", ""credits"")
                    VALUES (@cid, @code, @name, 3)", conn);
                insC.Parameters.AddWithValue("cid", cid);
                insC.Parameters.AddWithValue("code", cCode);
                insC.Parameters.AddWithValue("name", cName);
                await insC.ExecuteNonQueryAsync();
            }

            // Ensure lecturer_id column exists on classes
            await using var ensureColCmd2 = new NpgsqlCommand(@"
                ALTER TABLE classes ADD COLUMN IF NOT EXISTS lecturer_id uuid;
                CREATE INDEX IF NOT EXISTS ""IX_classes_lecturer_id"" ON classes(lecturer_id);", conn);
            await ensureColCmd2.ExecuteNonQueryAsync();

            // Class
            var clid = Guid.NewGuid();
            await using var getClCmd = new NpgsqlCommand("SELECT id FROM classes WHERE name = @cln LIMIT 1", conn);
            getClCmd.Parameters.AddWithValue("cln", clName);
            var exCl = await getClCmd.ExecuteScalarAsync();
            if (exCl != null && exCl != DBNull.Value)
            {
                clid = (Guid)exCl;
                await using var upCl = new NpgsqlCommand("UPDATE classes SET lecturer_id = @lid WHERE id = @clid", conn);
                upCl.Parameters.AddWithValue("lid", lecturerId);
                upCl.Parameters.AddWithValue("clid", clid);
                await upCl.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insCl = new NpgsqlCommand(@"
                    INSERT INTO classes (id, course_id, semester_id, lecturer_id, name, start_date, actual_start_date, end_date)
                    VALUES (@clid, @cid, @sid, @lid, @name, '2026-09-01', '2026-09-01', '2026-12-31')", conn);
                insCl.Parameters.AddWithValue("clid", clid);
                insCl.Parameters.AddWithValue("cid", cid);
                insCl.Parameters.AddWithValue("sid", semesterId);
                insCl.Parameters.AddWithValue("lid", lecturerId);
                insCl.Parameters.AddWithValue("name", clName);
                await insCl.ExecuteNonQueryAsync();
            }

            // Add student@pace.edu.vn and some sample students to class
            await using var addStuCl = new NpgsqlCommand(@"
                INSERT INTO student_classes (student_code, class_id, status)
                VALUES ('SE170001', @clid, 1)
                ON CONFLICT (student_code, class_id) DO NOTHING", conn);
            addStuCl.Parameters.AddWithValue("clid", clid);
            await addStuCl.ExecuteNonQueryAsync();

            for (int i = 0; i < 4; i++)
            {
                var sCode = sampleStudents[(courseIndex + i) % sampleStudents.Length].Item1;
                await using var addExtra = new NpgsqlCommand(@"
                    INSERT INTO student_classes (student_code, class_id, status)
                    VALUES (@sc, @clid, 1)
                    ON CONFLICT (student_code, class_id) DO NOTHING", conn);
                addExtra.Parameters.AddWithValue("sc", sCode);
                addExtra.Parameters.AddWithValue("clid", clid);
                await addExtra.ExecuteNonQueryAsync();
            }

            // Repository (AnalysisRun)
            var rid = Guid.NewGuid();
            await using var getRunCmd = new NpgsqlCommand(@"SELECT ""Id"" FROM analysis_runs WHERE LOWER(""RepoUrl"") = LOWER(@url) LIMIT 1", conn);
            getRunCmd.Parameters.AddWithValue("url", repoUrl);
            var exRun = await getRunCmd.ExecuteScalarAsync();
            if (exRun != null && exRun != DBNull.Value) rid = (Guid)exRun;
            else
            {
                var repoName = cCode == "PRN231" ? "TEST UPDATE GENERATE QUESTION" : (cCode == "SWP391" ? "ecommerce-ai-platform" : "smart-campus-mobile-app");
                await using var insRun = new NpgsqlCommand(@"
                    INSERT INTO analysis_runs (""Id"", ""RepoUrl"", ""RepoName"", ""IsArchived"", ""CreatedAt"")
                    VALUES (@rid, @url, @rn, false, now())", conn);
                insRun.Parameters.AddWithValue("rid", rid);
                insRun.Parameters.AddWithValue("url", repoUrl);
                insRun.Parameters.AddWithValue("rn", repoName);
                await insRun.ExecuteNonQueryAsync();

                // Sample businesses
                if (cCode == "SWP391")
                {
                    await using var bCmd = new NpgsqlCommand(@"
                        INSERT INTO businesses (""Id"", ""AnalysisRunId"", ""BusinessName"")
                        VALUES 
                        (gen_random_uuid(), @rid, 'Quản lý giỏ hàng & Đơn hàng'),
                        (gen_random_uuid(), @rid, 'Tích hợp Cổng thanh toán VNPay'),
                        (gen_random_uuid(), @rid, 'Gợi ý sản phẩm thông minh (AI)')", conn);
                    bCmd.Parameters.AddWithValue("rid", rid);
                    await bCmd.ExecuteNonQueryAsync();
                }
                else if (cCode == "PRM392")
                {
                    await using var bCmd2 = new NpgsqlCommand(@"
                        INSERT INTO businesses (""Id"", ""AnalysisRunId"", ""BusinessName"")
                        VALUES 
                        (gen_random_uuid(), @rid, 'Điểm danh tự động qua QR Code'),
                        (gen_random_uuid(), @rid, 'Xem lịch học & Thông báo thời gian thực')", conn);
                    bCmd2.Parameters.AddWithValue("rid", rid);
                    await bCmd2.ExecuteNonQueryAsync();
                }
            }

            // CourseProject
            await using var cpCmd = new NpgsqlCommand(@"
                INSERT INTO course_projects (""AnalysisRunId"", ""CourseId"")
                VALUES (@rid, @cid)
                ON CONFLICT (""AnalysisRunId"") DO UPDATE SET ""CourseId"" = EXCLUDED.""CourseId""", conn);
            cpCmd.Parameters.AddWithValue("rid", rid);
            cpCmd.Parameters.AddWithValue("cid", cid);
            await cpCmd.ExecuteNonQueryAsync();

            // Group
            var gid = Guid.NewGuid();
            await using var getGCmd = new NpgsqlCommand(@"
                SELECT g.id FROM groups g
                JOIN analysis_runs a ON a.group_id = g.id
                WHERE a.""Id"" = @rid LIMIT 1", conn);
            getGCmd.Parameters.AddWithValue("rid", rid);
            var exG = await getGCmd.ExecuteScalarAsync();
            if (exG != null && exG != DBNull.Value)
            {
                gid = (Guid)exG;
                await using var updG = new NpgsqlCommand("UPDATE groups SET class_id = @clid, name = @gn, topic_name = @tp WHERE id = @gid", conn);
                updG.Parameters.AddWithValue("clid", clid);
                updG.Parameters.AddWithValue("gn", grpName);
                updG.Parameters.AddWithValue("tp", topic);
                updG.Parameters.AddWithValue("gid", gid);
                await updG.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insG = new NpgsqlCommand(@"
                    INSERT INTO groups (id, class_id, name, topic_name, created_at)
                    VALUES (@gid, @clid, @gn, @tp, now())", conn);
                insG.Parameters.AddWithValue("gid", gid);
                insG.Parameters.AddWithValue("clid", clid);
                insG.Parameters.AddWithValue("gn", grpName);
                insG.Parameters.AddWithValue("tp", topic);
                await insG.ExecuteNonQueryAsync();

                await using var linkRun = new NpgsqlCommand(@"
                    UPDATE analysis_runs SET group_id = @gid WHERE ""Id"" = @rid", conn);
                linkRun.Parameters.AddWithValue("gid", gid);
                linkRun.Parameters.AddWithValue("rid", rid);
                await linkRun.ExecuteNonQueryAsync();
            }

            // Group member: SE170001
            await using var addGm = new NpgsqlCommand(@"
                INSERT INTO group_members (group_id, student_code, role, joined_at)
                VALUES (@gid, 'SE170001', 'Leader', now())
                ON CONFLICT (group_id, student_code) DO NOTHING", conn);
            addGm.Parameters.AddWithValue("gid", gid);
            await addGm.ExecuteNonQueryAsync();
        }

        _output.WriteLine("SeedClassAndProjectData completed successfully with 3 courses, 3 classes, groups, and repos!");
    }

    [Fact]
    public async Task MigrateToMultipleReposPerGroup()
    {
        var connStr = ConnectionString;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. Thêm cột group_id vào analysis_runs nếu chưa có
        await using var addColCmd = new NpgsqlCommand(@"
            ALTER TABLE analysis_runs 
            ADD COLUMN IF NOT EXISTS group_id uuid REFERENCES groups(id) ON DELETE SET NULL;", conn);
        await addColCmd.ExecuteNonQueryAsync();

        // 2. Chuyển dữ liệu từ groups.analysis_run_id sang analysis_runs.group_id
        await using var migrateDataCmd = new NpgsqlCommand(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'groups' AND column_name = 'analysis_run_id'
                ) THEN
                    UPDATE analysis_runs a
                    SET group_id = g.id
                    FROM groups g
                    WHERE (g.analysis_run_id = a.""Id"") AND a.group_id IS NULL;
                END IF;
            END $$;", conn);
        await migrateDataCmd.ExecuteNonQueryAsync();

        // 3. Xoá FK, index và cột analysis_run_id trên bảng groups
        await using var dropOldColCmd = new NpgsqlCommand(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'groups' AND column_name = 'analysis_run_id'
                ) THEN
                    ALTER TABLE groups DROP CONSTRAINT IF EXISTS ""FK_groups_analysis_runs_analysis_run_id"";
                    DROP INDEX IF EXISTS ""IX_groups_analysis_run_id"";
                    ALTER TABLE groups DROP COLUMN analysis_run_id;
                END IF;
            END $$;", conn);
        await dropOldColCmd.ExecuteNonQueryAsync();

        // 4. Tạo index trên analysis_runs(group_id)
        await using var addIdxCmd = new NpgsqlCommand(@"
            CREATE INDEX IF NOT EXISTS ""IX_analysis_runs_group_id"" ON analysis_runs(group_id);", conn);
        await addIdxCmd.ExecuteNonQueryAsync();

        _output.WriteLine("MigrateToMultipleReposPerGroup completed successfully!");
    }

    [Fact]
    public async Task MigrateAddOralExamToAssessmentSessions()
    {
        var connStr = ConnectionString;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. Kiểm tra / tạo bảng oral_exams nếu chưa có
        await using var createOralExamsCmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS oral_exams (
                id uuid NOT NULL CONSTRAINT PK_oral_exams PRIMARY KEY,
                title character varying(255) NOT NULL,
                description text,
                course_id uuid NOT NULL,
                semester_id uuid NOT NULL,
                status integer NOT NULL DEFAULT 0,
                start_date timestamp with time zone NOT NULL,
                end_date timestamp with time zone,
                created_at timestamp with time zone NOT NULL DEFAULT (now()),
                created_by_user_id uuid
            );", conn);
        await createOralExamsCmd.ExecuteNonQueryAsync();

        // 2. Thêm cột oral_exam_id vào assessment_sessions
        await using var addOralExamIdColCmd = new NpgsqlCommand(@"
            ALTER TABLE assessment_sessions 
            ADD COLUMN IF NOT EXISTS oral_exam_id uuid;

            CREATE INDEX IF NOT EXISTS ""IX_assessment_sessions_oral_exam_id"" 
            ON assessment_sessions(oral_exam_id);

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_assessment_sessions_oral_exams_oral_exam_id'
                ) THEN
                    ALTER TABLE assessment_sessions
                    ADD CONSTRAINT ""FK_assessment_sessions_oral_exams_oral_exam_id""
                    FOREIGN KEY (oral_exam_id) REFERENCES oral_exams(id) ON DELETE SET NULL;
                END IF;
            END $$;", conn);
        await addOralExamIdColCmd.ExecuteNonQueryAsync();

        // 3. Thêm cột group_id vào analysis_runs nếu chưa có
        await using var addGroupIdCmd = new NpgsqlCommand(@"
            ALTER TABLE analysis_runs 
            ADD COLUMN IF NOT EXISTS group_id uuid;

            CREATE INDEX IF NOT EXISTS ""IX_analysis_runs_group_id"" 
            ON analysis_runs(group_id);

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_analysis_runs_groups_group_id'
                ) THEN
                    ALTER TABLE analysis_runs
                    ADD CONSTRAINT ""FK_analysis_runs_groups_group_id""
                    FOREIGN KEY (group_id) REFERENCES groups(id) ON DELETE SET NULL;
                END IF;
            END $$;", conn);
        await addGroupIdCmd.ExecuteNonQueryAsync();

        // 4. Kiểm tra các cột trong assessment_sessions
        await using var checkColsCmd = new NpgsqlCommand(@"
            SELECT column_name, data_type 
            FROM information_schema.columns 
            WHERE table_name = 'assessment_sessions'
            ORDER BY ordinal_position;", conn);
        await using var reader = await checkColsCmd.ExecuteReaderAsync();
        _output.WriteLine("Columns of assessment_sessions:");
        while (await reader.ReadAsync())
        {
            _output.WriteLine($"- {reader.GetString(0)} ({reader.GetString(1)})");
        }

        _output.WriteLine("MigrateAddOralExamToAssessmentSessions completed successfully!");
    }

    [Fact]
    public async Task VerifyAssessmentSessionsQuery()
    {
        var connStr = ConnectionString;

        var options = new DbContextOptionsBuilder<PACE.Infrastructure.Persistence.AppDbContext>()
            .UseNpgsql(connStr)
            .Options;

        await using var db = new PACE.Infrastructure.Persistence.AppDbContext(options);

        // Truy vấn assessment_sessions với EF Core
        var sessions = await db.AssessmentSessions
            .Include(s => s.Project)
            .Include(s => s.OralExam)
            .Take(10)
            .ToListAsync();

        _output.WriteLine($"Successfully queried {sessions.Count} assessment sessions via EF Core!");
        foreach (var s in sessions)
        {
            _output.WriteLine($"- Session Id: {s.Id}, Title: {s.Title}, Status: {s.Status}, OralExamId: {s.OralExamId}");
        }

        Assert.NotNull(sessions);
    }

    [Fact]
    public async Task MigrateAndDropLecturerIdFromCourses()
    {
        var connStr = ConnectionString;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        // 1. Đồng bộ __EFMigrationsHistory cho các migration cũ đã có trong schema
        await using var syncHistoryCmd = new NpgsqlCommand(@"
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES
            ('20261013125910_InitialCreate', '9.0.4'),
            ('20261013134643_AddSourceCodeAndDataFlow', '9.0.4'),
            ('20261014091624_UpdateStudentSubmissionSchema', '9.0.4'),
            ('20261014164311_AddAnalysisRunBusinessTemplate', '9.0.4'),
            ('20261017144245_AddCreatedAtToStudentSubmission', '9.0.4'),
            ('20261017173036_TeachingWorkflows', '9.0.4'),
            ('20261017174201_AnalysisProvenance', '9.0.4'),
            ('20261114121657_AddOralExamToAssessmentSessions', '9.0.4')
            ON CONFLICT (""MigrationId"") DO NOTHING;", conn);
        await syncHistoryCmd.ExecuteNonQueryAsync();

        // 2. Chạy migration AddLecturerToClassAndDecoupleCourse:
        // - Thêm lecturer_id vào classes
        // - Copy LecturerId từ courses sang classes
        // - Drop constraints, indexes và cột LecturerId, Semester khỏi courses
        // - Tạo index unique mới
        await using var migrateCmd = new NpgsqlCommand(@"
            ALTER TABLE classes ADD COLUMN IF NOT EXISTS lecturer_id uuid;
            CREATE INDEX IF NOT EXISTS ""IX_classes_lecturer_id"" ON classes(lecturer_id);
            DO $$
            DECLARE
                user_pk_col text;
            BEGIN
                SELECT column_name INTO user_pk_col
                FROM information_schema.columns
                WHERE table_name = 'users' AND column_name IN ('Id', 'id')
                LIMIT 1;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_classes_users_lecturer_id') THEN
                    IF user_pk_col = 'Id' THEN
                        ALTER TABLE classes
                        ADD CONSTRAINT ""FK_classes_users_lecturer_id""
                        FOREIGN KEY (lecturer_id) REFERENCES users(""Id"") ON DELETE SET NULL;
                    ELSE
                        ALTER TABLE classes
                        ADD CONSTRAINT ""FK_classes_users_lecturer_id""
                        FOREIGN KEY (lecturer_id) REFERENCES users(id) ON DELETE SET NULL;
                    END IF;
                END IF;
            END $$;

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'courses' AND column_name = 'LecturerId'
                ) THEN
                    UPDATE classes SET lecturer_id = c.""LecturerId""
                    FROM courses c
                    WHERE classes.course_id = c.""Id"" AND classes.lecturer_id IS NULL;
                END IF;
            END $$;

            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_courses_users_LecturerId') THEN
                    ALTER TABLE courses DROP CONSTRAINT ""FK_courses_users_LecturerId"";
                END IF;
                IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_courses_Code_Semester_LecturerId') THEN
                    DROP INDEX ""IX_courses_Code_Semester_LecturerId"";
                END IF;
                IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_courses_LecturerId') THEN
                    DROP INDEX ""IX_courses_LecturerId"";
                END IF;

                -- Xóa hoàn toàn cột LecturerId và Semester khỏi bảng courses
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'courses' AND column_name = 'LecturerId') THEN
                    ALTER TABLE courses DROP COLUMN ""LecturerId"";
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'courses' AND column_name = 'Semester') THEN
                    ALTER TABLE courses DROP COLUMN ""Semester"";
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'courses' AND column_name = 'lecturer_id') THEN
                    ALTER TABLE courses DROP COLUMN lecturer_id;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'courses' AND column_name = 'semester') THEN
                    ALTER TABLE courses DROP COLUMN semester;
                END IF;
            END $$;

            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_courses_Code"" ON courses(""Code"");

            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_classes_course_id_semester_id_name') THEN
                    CREATE UNIQUE INDEX ""IX_classes_course_id_semester_id_name"" ON classes(course_id, semester_id, name);
                END IF;
            END $$;

            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20261115163000_AddLecturerToClassAndDecoupleCourse', '9.0.4')
            ON CONFLICT (""MigrationId"") DO NOTHING;", conn);
        await migrateCmd.ExecuteNonQueryAsync();

        // 3. Kiểm tra các cột thực tế của bảng courses trong database
        await using var checkColsCmd = new NpgsqlCommand(@"
            SELECT column_name, data_type 
            FROM information_schema.columns 
            WHERE table_name = 'courses'
            ORDER BY ordinal_position;", conn);
        await using var reader = await checkColsCmd.ExecuteReaderAsync();
        var columns = new List<string>();
        _output.WriteLine("=== REAL DATABASE COLUMNS IN 'courses' TABLE ===");
        while (await reader.ReadAsync())
        {
            var colName = reader.GetString(0);
            var colType = reader.GetString(1);
            columns.Add(colName);
            _output.WriteLine($"- {colName} ({colType})");
        }

        // 4. Assertions
        Assert.DoesNotContain("LecturerId", columns, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Semester", columns, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(columns, c => string.Equals(c, "Code", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(columns, c => string.Equals(c, "Name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SeedAccountsForLecturerAdminAndStudent()
    {
        var connStr = ConnectionString;

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        const string rawPassword = "PacePassword#2026";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

        // ==========================================
        // 1. ADMIN ACCOUNT: admin@pace.edu.vn / manager
        // ==========================================
        var adminId = Guid.NewGuid();
        await using var findAdminCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE LOWER(\"Email\") = 'admin@pace.edu.vn' LIMIT 1", conn);
        var existingAdminId = await findAdminCmd.ExecuteScalarAsync();

        if (existingAdminId != null && existingAdminId != DBNull.Value)
        {
            adminId = (Guid)existingAdminId;
            await using var updAdminCmd = new NpgsqlCommand(@"
                UPDATE users 
                SET ""Role"" = 'Admin', 
                    ""full_name"" = 'Quản trị viên Hệ thống', 
                    ""password_hash"" = @hash
                WHERE ""Id"" = @id", conn);
            updAdminCmd.Parameters.AddWithValue("hash", passwordHash);
            updAdminCmd.Parameters.AddWithValue("id", adminId);
            await updAdminCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"[Admin] Updated existing user {adminId} (admin@pace.edu.vn)");
        }
        else
        {
            await using var insAdminCmd = new NpgsqlCommand(@"
                INSERT INTO users (""Id"", ""Email"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                VALUES (@id, 'admin@pace.edu.vn', 'Quản trị viên Hệ thống', 'Admin', @hash, now())", conn);
            insAdminCmd.Parameters.AddWithValue("id", adminId);
            insAdminCmd.Parameters.AddWithValue("hash", passwordHash);
            await insAdminCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"[Admin] Created new admin user {adminId}");
        }

        // Cập nhật thêm nếu còn user 'manager' độc lập
        await using var updManagerCmd = new NpgsqlCommand(@"
            UPDATE users SET ""password_hash"" = @hash, ""Role"" = 'Admin' 
            WHERE LOWER(""Email"") = 'manager'", conn);
        updManagerCmd.Parameters.AddWithValue("hash", passwordHash);
        await updManagerCmd.ExecuteNonQueryAsync();
        await updManagerCmd.ExecuteNonQueryAsync();

        // ==========================================
        // 2. LECTURER ACCOUNT: lecturer@pace.edu.vn
        // ==========================================
        var lecturerId = Guid.NewGuid();
        await using var findLecCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE LOWER(\"Email\") = 'lecturer@pace.edu.vn' LIMIT 1", conn);
        var existingLecId = await findLecCmd.ExecuteScalarAsync();

        if (existingLecId != null && existingLecId != DBNull.Value)
        {
            lecturerId = (Guid)existingLecId;
            await using var updLecCmd = new NpgsqlCommand(@"
                UPDATE users 
                SET ""Role"" = 'Lecturer', 
                    ""full_name"" = 'Thầy Nguyễn Văn Giảng', 
                    ""password_hash"" = @hash 
                WHERE ""Id"" = @id", conn);
            updLecCmd.Parameters.AddWithValue("hash", passwordHash);
            updLecCmd.Parameters.AddWithValue("id", lecturerId);
            await updLecCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"[Lecturer] Updated existing lecturer user {lecturerId}");
        }
        else
        {
            await using var insLecCmd = new NpgsqlCommand(@"
                INSERT INTO users (""Id"", ""Email"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                VALUES (@id, 'lecturer@pace.edu.vn', 'Thầy Nguyễn Văn Giảng', 'Lecturer', @hash, now())", conn);
            insLecCmd.Parameters.AddWithValue("id", lecturerId);
            insLecCmd.Parameters.AddWithValue("hash", passwordHash);
            await insLecCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"[Lecturer] Created new lecturer user {lecturerId}");
        }

        // Upsert lecturer_profiles (GV001)
        await using var upsertLecProf = new NpgsqlCommand(@"
            INSERT INTO lecturer_profiles (lecturer_code, user_id, academic_title, created_at)
            VALUES ('GV001', @uid, 'Thạc sĩ / Giảng viên', now())
            ON CONFLICT (lecturer_code) DO UPDATE 
            SET user_id = EXCLUDED.user_id, academic_title = EXCLUDED.academic_title", conn);
        upsertLecProf.Parameters.AddWithValue("uid", lecturerId);
        await upsertLecProf.ExecuteNonQueryAsync();
        _output.WriteLine("[Lecturer] Upserted lecturer_profile GV001");

        // Gán giảng viên vào các lớp giảng dạy (SE1703, SE1704, SE1701, SE1702 nếu có)
        await using var assignLecClass = new NpgsqlCommand(@"
            UPDATE classes 
            SET lecturer_id = @lid 
            WHERE name IN ('SE1701', 'SE1702', 'SE1703', 'SE1704')", conn);
        assignLecClass.Parameters.AddWithValue("lid", lecturerId);
        var classesUpdated = await assignLecClass.ExecuteNonQueryAsync();
        _output.WriteLine($"[Lecturer] Assigned lecturer to {classesUpdated} classes");

        // ==========================================
        // 3. STUDENT ACCOUNT: student@pace.edu.vn / SE170001
        // ==========================================
        var studentId = Guid.NewGuid();
        await using var findStuCmd = new NpgsqlCommand(@"
            SELECT ""Id"" FROM users 
            WHERE LOWER(""Email"") = 'student@pace.edu.vn' OR ""StudentCode"" = 'SE170001' 
            LIMIT 1", conn);
        var existingStuId = await findStuCmd.ExecuteScalarAsync();

        if (existingStuId != null && existingStuId != DBNull.Value)
        {
            studentId = (Guid)existingStuId;
            await using var updStuCmd = new NpgsqlCommand(@"
                UPDATE users 
                SET ""Role"" = 'Student', 
                    ""StudentCode"" = 'SE170001',
                    ""Email"" = 'student@pace.edu.vn',
                    ""full_name"" = 'Nguyễn Văn Sinh Viên', 
                    ""password_hash"" = @hash 
                WHERE ""Id"" = @id", conn);
            updStuCmd.Parameters.AddWithValue("hash", passwordHash);
            updStuCmd.Parameters.AddWithValue("id", studentId);
            await updStuCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"[Student] Updated existing student user {studentId}");
        }
        else
        {
            await using var insStuCmd = new NpgsqlCommand(@"
                INSERT INTO users (""Id"", ""Email"", ""StudentCode"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                VALUES (@id, 'student@pace.edu.vn', 'SE170001', 'Nguyễn Văn Sinh Viên', 'Student', @hash, now())", conn);
            insStuCmd.Parameters.AddWithValue("id", studentId);
            insStuCmd.Parameters.AddWithValue("hash", passwordHash);
            await insStuCmd.ExecuteNonQueryAsync();
            _output.WriteLine($"[Student] Created new student user {studentId}");
        }

        // Upsert student_profiles (SE170001)
        await using var upsertStuProf = new NpgsqlCommand(@"
            INSERT INTO student_profiles (student_code, user_id, enrollment_year, created_at)
            VALUES ('SE170001', @uid, 2026, now())
            ON CONFLICT (student_code) DO UPDATE 
            SET user_id = EXCLUDED.user_id, enrollment_year = 2026", conn);
        upsertStuProf.Parameters.AddWithValue("uid", studentId);
        await upsertStuProf.ExecuteNonQueryAsync();
        _output.WriteLine("[Student] Upserted student_profile SE170001");

        // Gán student SE170001 vào lớp SE1703 và SE1701
        await using var enrollStuCmd = new NpgsqlCommand(@"
            INSERT INTO student_classes (student_code, class_id, status)
            SELECT 'SE170001', id, 1
            FROM classes
            WHERE name IN ('SE1701', 'SE1703')
            ON CONFLICT (student_code, class_id) DO NOTHING", conn);
        var enrolledCount = await enrollStuCmd.ExecuteNonQueryAsync();
        _output.WriteLine($"[Student] Enrolled SE170001 into classes: {enrolledCount} rows affected");

        // ==========================================
        // 4. VERIFY LOGIN INTEGRITY FOR ALL 3 ACCOUNTS
        // ==========================================
        var accountsToCheck = new[]
        {
            ("Admin", "admin@pace.edu.vn"),
            ("Lecturer", "lecturer@pace.edu.vn"),
            ("Student", "student@pace.edu.vn")
        };

        foreach (var (expectedRole, email) in accountsToCheck)
        {
            await using var verifyCmd = new NpgsqlCommand(@"
                SELECT ""Id"", ""Role"", ""password_hash"", ""full_name"" 
                FROM users 
                WHERE LOWER(""Email"") = @e", conn);
            verifyCmd.Parameters.AddWithValue("e", email);
            await using var reader = await verifyCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), $"Account {email} must exist in users table");

            var roleStr = reader.GetString(1);
            var storedHash = reader.GetString(2);
            var name = reader.GetString(3);

            Assert.Equal(expectedRole, roleStr);
            Assert.True(BCrypt.Net.BCrypt.Verify(rawPassword, storedHash), $"Password verify failed for {email}");
            _output.WriteLine($"[VERIFIED] {expectedRole} ({email}) - Name: {name} - PasswordHash matches '{rawPassword}'");
        }
    }

    [Fact]
    public async Task InspectStudentGroupsAndClasses()
    {
        var connStr = ConnectionString;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        _output.WriteLine("=== CLASSES FOR SE170001 ===");
        await using var clCmd = new NpgsqlCommand(@"
            SELECT c.id, c.name, c.course_id, co.""Code"", co.""Name""
            FROM student_classes sc
            JOIN classes c ON c.id = sc.class_id
            LEFT JOIN courses co ON co.""Id"" = c.course_id
            WHERE sc.student_code = 'SE170001';", conn);
        await using (var r = await clCmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                _output.WriteLine($"ClassId: {r[0]} | Name: {r[1]} | CourseId: {r[2]} | CourseCode: {r[3]} - {r[4]}");
            }
        }

        _output.WriteLine("\n=== GROUPS FOR SE170001 ===");
        await using var gCmd = new NpgsqlCommand(@"
            SELECT g.id, g.class_id, c.name as class_name, g.name as group_name, g.topic_name, gm.role
            FROM group_members gm
            JOIN groups g ON g.id = gm.group_id
            LEFT JOIN classes c ON c.id = g.class_id
            WHERE gm.student_code = 'SE170001';", conn);
        await using (var r2 = await gCmd.ExecuteReaderAsync())
        {
            while (await r2.ReadAsync())
            {
                _output.WriteLine($"GroupId: {r2[0]} | ClassId: {r2[1]} ({r2[2]}) | GroupName: {r2[3]} | Topic: {r2[4]} | Role: {r2[5]}");
            }
        }
    }

    [Fact]
    public async Task SeedGroupAndTopicWorkflowData()
    {
        var connStr = ConnectionString;
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        var defHash = BCrypt.Net.BCrypt.HashPassword("PacePassword#2026");

        // 1. Users & Profiles
        var students = new[]
        {
            ("SE170001", "Nguyễn Văn Sinh Viên", "student@pace.edu.vn"),
            ("SE170002", "Trần Thị Mai", "mai.tt@fpt.edu.vn"),
            ("SE170003", "Lê Hoàng Nam", "nam.lh@fpt.edu.vn"),
            ("SE170004", "Phạm Quốc Bảo", "bao.pq@fpt.edu.vn"),
            ("SE170005", "Võ Minh Thư", "thu.vm@fpt.edu.vn"),
            ("SE170006", "Đặng Quang Huy", "huy.dq@fpt.edu.vn"),
            ("SE170007", "Ngô Bảo Ngọc", "ngoc.nb@fpt.edu.vn"),
            ("SE170008", "Hoàng Văn Đức", "duc.hv@fpt.edu.vn"),
        };

        var studentIds = new Dictionary<string, Guid>();
        foreach (var (code, name, email) in students)
        {
            var sid = Guid.NewGuid();
            await using var chkCmd = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE \"StudentCode\" = @c OR LOWER(\"Email\") = @e LIMIT 1", conn);
            chkCmd.Parameters.AddWithValue("c", code);
            chkCmd.Parameters.AddWithValue("e", email.ToLower());
            var exId = await chkCmd.ExecuteScalarAsync();
            if (exId != null && exId != DBNull.Value)
            {
                sid = (Guid)exId;
                await using var updUser = new NpgsqlCommand(@"
                    UPDATE users SET ""StudentCode"" = @c, ""full_name"" = @n, ""password_hash"" = @h
                    WHERE ""Id"" = @id", conn);
                updUser.Parameters.AddWithValue("c", code);
                updUser.Parameters.AddWithValue("n", name);
                updUser.Parameters.AddWithValue("h", defHash);
                updUser.Parameters.AddWithValue("id", sid);
                await updUser.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insUser = new NpgsqlCommand(@"
                    INSERT INTO users (""Id"", ""Email"", ""StudentCode"", ""full_name"", ""Role"", ""password_hash"", ""CreatedAt"")
                    VALUES (@id, @e, @c, @n, 'Student', @h, now())", conn);
                insUser.Parameters.AddWithValue("id", sid);
                insUser.Parameters.AddWithValue("e", email.ToLower());
                insUser.Parameters.AddWithValue("c", code);
                insUser.Parameters.AddWithValue("n", name);
                insUser.Parameters.AddWithValue("h", defHash);
                await insUser.ExecuteNonQueryAsync();
            }
            studentIds[code] = sid;

            await using var upsertProf = new NpgsqlCommand(@"
                INSERT INTO student_profiles (student_code, user_id, enrollment_year, created_at)
                VALUES (@c, @uid, 2023, now())
                ON CONFLICT (student_code) DO UPDATE SET user_id = EXCLUDED.user_id", conn);
            upsertProf.Parameters.AddWithValue("c", code);
            upsertProf.Parameters.AddWithValue("uid", sid);
            await upsertProf.ExecuteNonQueryAsync();
        }

        // Lecturer
        var lecturerId = Guid.NewGuid();
        await using var chkLec = new NpgsqlCommand("SELECT \"Id\" FROM users WHERE LOWER(\"Email\") = 'lecturer@pace.edu.vn' LIMIT 1", conn);
        var exLec = await chkLec.ExecuteScalarAsync();
        if (exLec != null && exLec != DBNull.Value)
        {
            lecturerId = (Guid)exLec;
        }

        // 2. Courses & Classes
        var courseData = new[]
        {
            ("PRN231", "Building Cross-Platform Web Applications with .NET", "SE1701"),
            ("PRM392", "Mobile Programming (Android & Flutter)", "SE1703")
        };

        var classIds = new Dictionary<string, Guid>();
        var courseIds = new Dictionary<string, Guid>();

        foreach (var (code, name, clName) in courseData)
        {
            var cid = Guid.NewGuid();
            await using var cCmd = new NpgsqlCommand("SELECT \"Id\" FROM courses WHERE \"Code\" = @c LIMIT 1", conn);
            cCmd.Parameters.AddWithValue("c", code);
            var exC = await cCmd.ExecuteScalarAsync();
            if (exC != null && exC != DBNull.Value) cid = (Guid)exC;
            else
            {
                await using var insC = new NpgsqlCommand(@"
                    INSERT INTO courses (""Id"", ""Code"", ""Name"", ""credits"")
                    VALUES (@id, @c, @n, 3)", conn);
                insC.Parameters.AddWithValue("id", cid);
                insC.Parameters.AddWithValue("c", code);
                insC.Parameters.AddWithValue("n", name);
                await insC.ExecuteNonQueryAsync();
            }
            courseIds[code] = cid;

            var clid = Guid.NewGuid();
            await using var clCmd = new NpgsqlCommand("SELECT id FROM classes WHERE name = @cln LIMIT 1", conn);
            clCmd.Parameters.AddWithValue("cln", clName);
            var exCl = await clCmd.ExecuteScalarAsync();
            if (exCl != null && exCl != DBNull.Value)
            {
                clid = (Guid)exCl;
                await using var updCl = new NpgsqlCommand("UPDATE classes SET lecturer_id = @lid, course_id = @cid WHERE id = @id", conn);
                updCl.Parameters.AddWithValue("lid", lecturerId);
                updCl.Parameters.AddWithValue("cid", cid);
                updCl.Parameters.AddWithValue("id", clid);
                await updCl.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insCl = new NpgsqlCommand(@"
                    INSERT INTO classes (id, course_id, lecturer_id, name, start_date, actual_start_date, end_date)
                    VALUES (@id, @cid, @lid, @name, '2026-09-01', '2026-09-01', '2026-12-31')", conn);
                insCl.Parameters.AddWithValue("id", clid);
                insCl.Parameters.AddWithValue("cid", cid);
                insCl.Parameters.AddWithValue("lid", lecturerId);
                insCl.Parameters.AddWithValue("name", clName);
                await insCl.ExecuteNonQueryAsync();
            }
            classIds[clName] = clid;
        }

        // Enroll students
        // SE1701: SE170001, SE170002, SE170003, SE170004, SE170005
        foreach (var sc in new[] { "SE170001", "SE170002", "SE170003", "SE170004", "SE170005" })
        {
            await using var enCmd = new NpgsqlCommand(@"
                INSERT INTO student_classes (student_code, class_id, status)
                VALUES (@sc, @clid, 1)
                ON CONFLICT (student_code, class_id) DO UPDATE SET status = 1", conn);
            enCmd.Parameters.AddWithValue("sc", sc);
            enCmd.Parameters.AddWithValue("clid", classIds["SE1701"]);
            await enCmd.ExecuteNonQueryAsync();
        }

        // SE1703: SE170006, SE170007, SE170008
        foreach (var sc in new[] { "SE170006", "SE170007", "SE170008" })
        {
            await using var enCmd = new NpgsqlCommand(@"
                INSERT INTO student_classes (student_code, class_id, status)
                VALUES (@sc, @clid, 1)
                ON CONFLICT (student_code, class_id) DO UPDATE SET status = 1", conn);
            enCmd.Parameters.AddWithValue("sc", sc);
            enCmd.Parameters.AddWithValue("clid", classIds["SE1703"]);
            await enCmd.ExecuteNonQueryAsync();
        }

        // 3. Milestones
        // SE1701: GroupRegistration OPEN (deadline in 7 days)
        await using var msCmd1 = new NpgsqlCommand(@"
            INSERT INTO milestones (id, class_id, type, title, description, deadline, is_locked, order_index, created_at)
            VALUES 
            (gen_random_uuid(), @cl1, 0, 'Đăng ký nhóm đồ án', 'Thời hạn sinh viên tự lập nhóm và mời thành viên.', now() + interval '7 days', false, 1, now()),
            (gen_random_uuid(), @cl1, 1, 'Nộp đề xuất đề tài', 'Thời hạn nhóm chốt đề tài mẫu hoặc đề xuất mới.', now() + interval '14 days', false, 2, now()),
            (gen_random_uuid(), @cl1, 3, 'Nộp báo cáo cuối kỳ', 'Hạn nộp source code và tài liệu đồ án.', now() + interval '45 days', false, 3, now())
            ON CONFLICT DO NOTHING;", conn);
        msCmd1.Parameters.AddWithValue("cl1", classIds["SE1701"]);
        await msCmd1.ExecuteNonQueryAsync();

        // SE1703: GroupRegistration LOCKED (deadline passed)
        await using var msCmd2 = new NpgsqlCommand(@"
            INSERT INTO milestones (id, class_id, type, title, description, deadline, is_locked, order_index, created_at)
            VALUES 
            (gen_random_uuid(), @cl2, 0, 'Đăng ký nhóm đồ án', 'Hạn chốt lập nhóm (Đã khóa).', now() - interval '2 days', true, 1, now()),
            (gen_random_uuid(), @cl2, 1, 'Nộp đề xuất đề tài', 'Hạn nộp đề tài đồ án.', now() + interval '5 days', false, 2, now())
            ON CONFLICT DO NOTHING;", conn);
        msCmd2.Parameters.AddWithValue("cl2", classIds["SE1703"]);
        await msCmd2.ExecuteNonQueryAsync();

        // 4. Sample Course Topics (Kho đề tài mẫu)
        var topicsData = new[]
        {
            (courseIds["PRN231"], "Hệ thống Quản lý và Đánh giá Khóa học (PACE 2.0)", "Nền tảng hỗ trợ giảng viên đánh giá đồ án, phân tích commit Git và tổ chức phiên thi vấn đáp trực tuyến.", ".NET 9 Web API, Next.js 14, PostgreSQL, Docker", 1),
            (courseIds["PRN231"], "Nền tảng E-Learning và Thi trắc nghiệm trực tuyến", "Hệ thống quản lý khóa học online, tạo ngân hàng đề thi và chống gian lận trong thi trực tuyến.", "ASP.NET Core, React, SQL Server / Postgres", 2),
            (courseIds["PRN231"], "Hệ thống Quản lý Ký túc xá Đại học thông minh", "Quản lý phòng ở sinh viên, tự động tính tiền điện nước, xuất hóa đơn và thanh toán qua QR Code.", "Blazor / .NET API, TailwindCSS, PostgreSQL", 1),
            (courseIds["PRN231"], "Cổng thông tin Việc làm và Tuyển dụng sinh viên FPT", "Kết nối sinh viên với các doanh nghiệp đối tác, hỗ trợ nộp CV và phỏng vấn trực tuyến.", "C# .NET, React, Redis, Azure Blob Storage", 2),
            (courseIds["PRM392"], "Ứng dụng điểm danh và quản lý học tập Smart Campus", "Ứng dụng di động quét mã QR động để điểm danh lớp học, xem thời khóa biểu và điểm số.", "Flutter, Dart, Firebase, .NET Web API", 1),
            (courseIds["PRM392"], "Ứng dụng theo dõi sức khỏe và luyện tập thể thao", "Theo dõi bước chân, nhịp tim, gợi ý thực đơn và bài tập cho sinh viên hàng ngày.", "React Native / Kotlin, GraphQL, PostgreSQL", 2)
        };

        foreach (var (cid, title, desc, reqs, maxGrp) in topicsData)
        {
            await using var insTop = new NpgsqlCommand(@"
                INSERT INTO course_topics (id, course_id, title, description, requirements, max_groups, is_active, created_at)
                VALUES (gen_random_uuid(), @cid, @title, @desc, @reqs, @maxGrp, true, now())
                ON CONFLICT DO NOTHING;", conn);
            insTop.Parameters.AddWithValue("cid", cid);
            insTop.Parameters.AddWithValue("title", title);
            insTop.Parameters.AddWithValue("desc", desc);
            insTop.Parameters.AddWithValue("reqs", reqs);
            insTop.Parameters.AddWithValue("maxGrp", maxGrp);
            await insTop.ExecuteNonQueryAsync();
        }

        // 5. Clean up duplicate / orphaned test groups in SE1701 to keep clean data
        await using var cleanGroupsCmd = new NpgsqlCommand(@"
            DELETE FROM group_change_requests WHERE group_id IN (SELECT id FROM groups WHERE class_id = @cl1 AND name NOT IN ('Nhóm 01 - PACE Core Team'));
            DELETE FROM group_invitations WHERE group_id IN (SELECT id FROM groups WHERE class_id = @cl1 AND name NOT IN ('Nhóm 01 - PACE Core Team'));
            DELETE FROM group_members WHERE group_id IN (SELECT id FROM groups WHERE class_id = @cl1 AND name NOT IN ('Nhóm 01 - PACE Core Team'));
            DELETE FROM groups WHERE class_id = @cl1 AND name NOT IN ('Nhóm 01 - PACE Core Team');", conn);
        cleanGroupsCmd.Parameters.AddWithValue("cl1", classIds["SE1701"]);
        await cleanGroupsCmd.ExecuteNonQueryAsync();

        // 6. Setup Nhóm 01 in SE1701
        var g1Id = Guid.NewGuid();
        await using var chkG1 = new NpgsqlCommand("SELECT id FROM groups WHERE class_id = @cl1 AND name = 'Nhóm 01 - PACE Core Team' LIMIT 1", conn);
        chkG1.Parameters.AddWithValue("cl1", classIds["SE1701"]);
        var exG1 = await chkG1.ExecuteScalarAsync();
        if (exG1 != null && exG1 != DBNull.Value) g1Id = (Guid)exG1;
        else
        {
            await using var insG1 = new NpgsqlCommand(@"
                INSERT INTO groups (id, class_id, name, topic_name, created_at)
                VALUES (@id, @cl1, 'Nhóm 01 - PACE Core Team', 'Hệ thống Quản lý và Đánh giá Khóa học (PACE 2.0)', now())", conn);
            insG1.Parameters.AddWithValue("id", g1Id);
            insG1.Parameters.AddWithValue("cl1", classIds["SE1701"]);
            await insG1.ExecuteNonQueryAsync();
        }

        // Members of G1: SE170001 (Leader), SE170002 (Member)
        await using var insGm1 = new NpgsqlCommand(@"
            INSERT INTO group_members (group_id, student_code, role, joined_at)
            VALUES (@gid, 'SE170001', 'Leader', now())
            ON CONFLICT (group_id, student_code) DO UPDATE SET role = 'Leader';", conn);
        insGm1.Parameters.AddWithValue("gid", g1Id);
        await insGm1.ExecuteNonQueryAsync();

        await using var insGm2 = new NpgsqlCommand(@"
            INSERT INTO group_members (group_id, student_code, role, joined_at)
            VALUES (@gid, 'SE170002', 'Member', now())
            ON CONFLICT (group_id, student_code) DO UPDATE SET role = 'Member';", conn);
        insGm2.Parameters.AddWithValue("gid", g1Id);
        await insGm2.ExecuteNonQueryAsync();

        // Ensure AnalysisRun and CourseProject for G1
        var runId = Guid.NewGuid();
        await using var chkRun = new NpgsqlCommand(@"SELECT ""Id"" FROM analysis_runs WHERE group_id = @gid LIMIT 1", conn);
        chkRun.Parameters.AddWithValue("gid", g1Id);
        var exRun = await chkRun.ExecuteScalarAsync();
        if (exRun != null && exRun != DBNull.Value) runId = (Guid)exRun;
        else
        {
            await using var insRun = new NpgsqlCommand(@"
                INSERT INTO analysis_runs (""Id"", group_id, ""RepoName"", ""RepoUrl"", ""IsArchived"", ""CreatedAt"")
                VALUES (@id, @gid, 'Hệ thống Quản lý và Đánh giá Khóa học (PACE 2.0)', 'https://github.com/HuongDS/PACE-Project_Assistance_and_Course_Evaluation', false, now())", conn);
            insRun.Parameters.AddWithValue("id", runId);
            insRun.Parameters.AddWithValue("gid", g1Id);
            await insRun.ExecuteNonQueryAsync();
        }

        await using var insCp = new NpgsqlCommand(@"
            INSERT INTO course_projects (""AnalysisRunId"", ""CourseId"", ""IsApproved"", ""Notes"", ""DecidedAt"")
            VALUES (@rid, @cid, NULL, NULL, NULL)
            ON CONFLICT (""AnalysisRunId"") DO UPDATE SET ""CourseId"" = EXCLUDED.""CourseId"";", conn);
        insCp.Parameters.AddWithValue("rid", runId);
        insCp.Parameters.AddWithValue("cid", courseIds["PRN231"]);
        await insCp.ExecuteNonQueryAsync();

        // Group Invitation: G1 invites SE170003 (status Pending)
        await using var delOldInv = new NpgsqlCommand("DELETE FROM group_invitations WHERE group_id = @gid", conn);
        delOldInv.Parameters.AddWithValue("gid", g1Id);
        await delOldInv.ExecuteNonQueryAsync();

        await using var insInv = new NpgsqlCommand(@"
            INSERT INTO group_invitations (id, group_id, invited_student_code, invited_by_user_id, status, created_at)
            VALUES (gen_random_uuid(), @gid, 'SE170003', @uid, 0, now())", conn);
        insInv.Parameters.AddWithValue("gid", g1Id);
        insInv.Parameters.AddWithValue("uid", studentIds["SE170001"]);
        await insInv.ExecuteNonQueryAsync();

        // Group Change Request in SE1701: Type = ChangeTopic (status Pending)
        await using var delOldCr = new NpgsqlCommand("DELETE FROM group_change_requests WHERE group_id = @gid", conn);
        delOldCr.Parameters.AddWithValue("gid", g1Id);
        await delOldCr.ExecuteNonQueryAsync();

        await using var insCr = new NpgsqlCommand(@"
            INSERT INTO group_change_requests (id, group_id, created_by_user_id, title, type, status, reason, payload_json, created_at)
            VALUES (gen_random_uuid(), @gid, @uid, 'Yêu cầu đổi đề tài đồ án', 0, 0, 'Nhóm muốn bổ sung phân hệ chấm điểm AI và đổi tên đề tài thành PACE AI Platform.', '{""newTopicName"":""PACE AI - Nền tảng Đánh giá Đồ án Tự động""}', now())", conn);
        insCr.Parameters.AddWithValue("gid", g1Id);
        insCr.Parameters.AddWithValue("uid", studentIds["SE170001"]);
        await insCr.ExecuteNonQueryAsync();

        // 7. Setup Nhóm 02 in SE1703
        var g2Id = Guid.NewGuid();
        await using var chkG2 = new NpgsqlCommand("SELECT id FROM groups WHERE class_id = @cl2 AND name = 'Nhóm 02 - Mobile Devs' LIMIT 1", conn);
        chkG2.Parameters.AddWithValue("cl2", classIds["SE1703"]);
        var exG2 = await chkG2.ExecuteScalarAsync();
        if (exG2 != null && exG2 != DBNull.Value) g2Id = (Guid)exG2;
        else
        {
            await using var insG2 = new NpgsqlCommand(@"
                INSERT INTO groups (id, class_id, name, topic_name, created_at)
                VALUES (@id, @cl2, 'Nhóm 02 - Mobile Devs', 'Ứng dụng điểm danh và quản lý học tập Smart Campus', now())", conn);
            insG2.Parameters.AddWithValue("id", g2Id);
            insG2.Parameters.AddWithValue("cl2", classIds["SE1703"]);
            await insG2.ExecuteNonQueryAsync();
        }

        await using var insGm3 = new NpgsqlCommand(@"
            INSERT INTO group_members (group_id, student_code, role, joined_at)
            VALUES (@gid, 'SE170006', 'Leader', now())
            ON CONFLICT (group_id, student_code) DO UPDATE SET role = 'Leader';", conn);
        insGm3.Parameters.AddWithValue("gid", g2Id);
        await insGm3.ExecuteNonQueryAsync();

        await using var insGm4 = new NpgsqlCommand(@"
            INSERT INTO group_members (group_id, student_code, role, joined_at)
            VALUES (@gid, 'SE170007', 'Member', now())
            ON CONFLICT (group_id, student_code) DO UPDATE SET role = 'Member';", conn);
        insGm4.Parameters.AddWithValue("gid", g2Id);
        await insGm4.ExecuteNonQueryAsync();

        // Change Request in SE1703: RemoveMember (SE170007 wants to leave)
        await using var delOldCr2 = new NpgsqlCommand("DELETE FROM group_change_requests WHERE group_id = @gid", conn);
        delOldCr2.Parameters.AddWithValue("gid", g2Id);
        await delOldCr2.ExecuteNonQueryAsync();

        await using var insCr2 = new NpgsqlCommand(@"
            INSERT INTO group_change_requests (id, group_id, created_by_user_id, title, type, status, reason, payload_json, created_at)
            VALUES (gen_random_uuid(), @gid, @uid, 'Đơn xin rút khỏi nhóm đồ án', 1, 0, 'Em bị trùng lịch thực tập tại doanh nghiệp nên xin rút khỏi môn học.', '{""studentCode"":""SE170007"",""fullName"":""Ngô Bảo Ngọc""}', now())", conn);
        insCr2.Parameters.AddWithValue("gid", g2Id);
        insCr2.Parameters.AddWithValue("uid", studentIds["SE170007"]);
        await insCr2.ExecuteNonQueryAsync();

        _output.WriteLine("SeedGroupAndTopicWorkflowData completed successfully!");
    }
}
