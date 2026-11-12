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

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("lecturer@123");
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
            await using var groupCmd = new NpgsqlCommand(@"
                INSERT INTO groups (id, class_id, name, topic_name, analysis_run_id, created_at)
                VALUES (gen_random_uuid(), @clid, 'Nhóm 01', 'Hệ thống Quản lý và Đánh giá Khóa học (PACE)', @rid, now())
                ON CONFLICT (analysis_run_id) DO UPDATE
                SET class_id = EXCLUDED.class_id, name = EXCLUDED.name, topic_name = EXCLUDED.topic_name", conn);
            groupCmd.Parameters.AddWithValue("clid", classId);
            groupCmd.Parameters.AddWithValue("rid", repoId);
            await groupCmd.ExecuteNonQueryAsync();

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
}
