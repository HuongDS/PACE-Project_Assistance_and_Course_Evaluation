using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task DropRubricTables()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";

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
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";

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

            // Ensure Course exists and assigned to Lecturer
            var courseId = Guid.NewGuid();
            await using var getCourseCmd = new NpgsqlCommand("SELECT \"Id\" FROM courses LIMIT 1", conn);
            var existingCourse = await getCourseCmd.ExecuteScalarAsync();

            if (existingCourse != null && existingCourse != DBNull.Value)
            {
                courseId = (Guid)existingCourse;
                await using var updateCourseCmd = new NpgsqlCommand("UPDATE courses SET \"LecturerId\" = @lid WHERE \"Id\" = @cid", conn);
                updateCourseCmd.Parameters.AddWithValue("lid", lecturerUserId);
                updateCourseCmd.Parameters.AddWithValue("cid", courseId);
                await updateCourseCmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insertCourseCmd = new NpgsqlCommand(@"
                    INSERT INTO courses (""Id"", ""Code"", ""Name"", ""Credits"", ""Semester"", ""LecturerId"")
                    VALUES (@cid, 'PRN231', 'Building Cross-Platform Web Applications with .NET', 3, 'FA26', @lid)", conn);
                insertCourseCmd.Parameters.AddWithValue("cid", courseId);
                insertCourseCmd.Parameters.AddWithValue("lid", lecturerUserId);
                await insertCourseCmd.ExecuteNonQueryAsync();
            }

            // Ensure Class exists
            var classId = Guid.NewGuid();
            await using var getClassCmd = new NpgsqlCommand("SELECT id FROM classes LIMIT 1", conn);
            var existingClass = await getClassCmd.ExecuteScalarAsync();

            if (existingClass != null && existingClass != DBNull.Value)
            {
                classId = (Guid)existingClass;
                await using var updateClassCmd = new NpgsqlCommand("UPDATE classes SET course_id = @cid, name = COALESCE(name, 'SE1701') WHERE id = @clid", conn);
                updateClassCmd.Parameters.AddWithValue("cid", courseId);
                updateClassCmd.Parameters.AddWithValue("clid", classId);
                await updateClassCmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var insertClassCmd = new NpgsqlCommand(@"
                    INSERT INTO classes (id, course_id, name, start_date, actual_start_date, end_date)
                    VALUES (@clid, @cid, 'SE1701', '2026-09-01', '2026-09-01', '2026-12-31')", conn);
                insertClassCmd.Parameters.AddWithValue("clid", classId);
                insertClassCmd.Parameters.AddWithValue("cid", courseId);
                await insertClassCmd.ExecuteNonQueryAsync();
            }

            // Ensure Group exists and points to this repo and class
            var gid = Guid.NewGuid();
            await using var groupCmd = new NpgsqlCommand(@"
                INSERT INTO groups (id, class_id, name, topic_name, created_at)
                VALUES (@gid, @clid, 'Nhóm 01', 'Hệ thống Quản lý và Đánh giá Khóa học (PACE)', now())
                ON CONFLICT (id) DO NOTHING", conn);
            groupCmd.Parameters.AddWithValue("gid", gid);
            groupCmd.Parameters.AddWithValue("clid", classId);
            await groupCmd.ExecuteNonQueryAsync();

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
        var connStr = "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";
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
                    INSERT INTO courses (""Id"", ""Code"", ""Name"", ""credits"", ""Semester"", ""LecturerId"")
                    VALUES (@cid, @code, @name, 3, 'FA26', @lid)", conn);
                insC.Parameters.AddWithValue("cid", cid);
                insC.Parameters.AddWithValue("code", cCode);
                insC.Parameters.AddWithValue("name", cName);
                insC.Parameters.AddWithValue("lid", lecturerId);
                await insC.ExecuteNonQueryAsync();
            }

            // Class
            var clid = Guid.NewGuid();
            await using var getClCmd = new NpgsqlCommand("SELECT id FROM classes WHERE name = @cln LIMIT 1", conn);
            getClCmd.Parameters.AddWithValue("cln", clName);
            var exCl = await getClCmd.ExecuteScalarAsync();
            if (exCl != null && exCl != DBNull.Value) clid = (Guid)exCl;
            else
            {
                await using var insCl = new NpgsqlCommand(@"
                    INSERT INTO classes (id, course_id, semester_id, name, start_date, actual_start_date, end_date)
                    VALUES (@clid, @cid, @sid, @name, '2026-09-01', '2026-09-01', '2026-12-31')", conn);
                insCl.Parameters.AddWithValue("clid", clid);
                insCl.Parameters.AddWithValue("cid", cid);
                insCl.Parameters.AddWithValue("sid", semesterId);
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
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";

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
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";

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
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection") 
                      ?? "Host=ep-rapid-silence-azrlmoxx-pooler.c-3.ap-southeast-1.aws.neon.tech;Port=5432;Database=neondb;Username=neondb_owner;Password=npg_rF8HIqysK0RA;SSL Mode=Require;Channel Binding=Require";

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
}
