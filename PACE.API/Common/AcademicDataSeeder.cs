using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Common;

public static class AcademicDataSeeder
{
    public static async Task SeedAcademicDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();

        try
        {
            // 1. Seed Semesters
            var fall2026 = await db.Semesters.FirstOrDefaultAsync(s => s.Name == "Fall 2026" || s.Name == "Fall2026");
            if (fall2026 == null)
            {
                fall2026 = new Semester
                {
                    Id = Guid.NewGuid(),
                    Name = "Fall 2026",
                    AcademicYear = "2026",
                    StartDate = new DateOnly(2026, 9, 1),
                    ActualStartDate = new DateOnly(2026, 9, 1),
                    EndDate = new DateOnly(2026, 12, 31)
                };
                db.Semesters.Add(fall2026);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded semester: Fall 2026");
            }

            var spring2026 = await db.Semesters.FirstOrDefaultAsync(s => s.Name == "Spring 2026" || s.Name == "Spring2026");
            if (spring2026 == null)
            {
                spring2026 = new Semester
                {
                    Id = Guid.NewGuid(),
                    Name = "Spring 2026",
                    AcademicYear = "2026",
                    StartDate = new DateOnly(2026, 1, 1),
                    ActualStartDate = new DateOnly(2026, 1, 1),
                    EndDate = new DateOnly(2026, 4, 30)
                };
                db.Semesters.Add(spring2026);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded semester: Spring 2026");
            }

            // 2. Seed Lecturer: lecturer@pace.edu.vn
            var lecturerEmail = "lecturer@pace.edu.vn";
            var lecturer = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == lecturerEmail);
            if (lecturer == null)
            {
                lecturer = new User
                {
                    Id = Guid.NewGuid(),
                    Email = lecturerEmail,
                    FullName = "Thầy Nguyễn Văn Giảng",
                    Role = UserRole.Lecturer,
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(lecturer);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded lecturer: lecturer@pace.edu.vn");
            }
            else
            {
                bool updated = false;
                if (lecturer.Role != UserRole.Lecturer)
                {
                    lecturer.Role = UserRole.Lecturer;
                    updated = true;
                }
                if (string.IsNullOrWhiteSpace(lecturer.FullName) || lecturer.FullName == lecturerEmail)
                {
                    lecturer.FullName = "Thầy Nguyễn Văn Giảng";
                    updated = true;
                }
                if (updated) await db.SaveChangesAsync();
            }

            // 3. Seed Courses
            var courseList = new[]
            {
                ("SWP391", "Software Development Project", 3),
                ("PRN231", "Building Cross-Platform Apps with .NET", 3),
                ("PRM392", "Mobile Programming", 3)
            };

            var courseEntities = new Dictionary<string, Course>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, name, credits) in courseList)
            {
                var course = await db.Courses.FirstOrDefaultAsync(c => c.Code.ToLower() == code.ToLower());
                if (course == null)
                {
                    course = new Course
                    {
                        Id = Guid.NewGuid(),
                        Code = code,
                        Name = name,
                        Credits = credits,
                        Semester = "Fall 2026",
                        LecturerId = lecturer.Id
                    };
                    db.Courses.Add(course);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded course: {Code}", code);
                }
                courseEntities[code] = course;
            }

            // 4. Seed Classes
            var classSpecs = new[]
            {
                ("SE1703", "SWP391", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)),
                ("SE1704", "PRN231", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31))
            };

            Class? se1703Class = null;
            foreach (var (className, courseCode, start, end) in classSpecs)
            {
                if (!courseEntities.TryGetValue(courseCode, out var c)) continue;

                var cls = await db.Classes.FirstOrDefaultAsync(cl => cl.SemesterId == fall2026.Id && cl.Name == className);
                if (cls == null)
                {
                    cls = new Class
                    {
                        Id = Guid.NewGuid(),
                        Name = className,
                        CourseId = c.Id,
                        SemesterId = fall2026.Id,
                        StartDate = start,
                        ActualStartDate = start,
                        EndDate = end
                    };
                    db.Classes.Add(cls);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Seeded class: {ClassName}", className);
                }
                if (className == "SE1703") se1703Class = cls;
            }

            // 5. Seed Students into SE1703
            if (se1703Class != null)
            {
                var sampleStudents = new[]
                {
                    ("SE170001", "Nguyễn Văn An", "an_se170001@fpt.edu.vn"),
                    ("SE170002", "Trần Thị Bình", "binh_se170002@fpt.edu.vn"),
                    ("SE170003", "Lê Hoàng Cường", "cuong_se170003@fpt.edu.vn"),
                    ("SE170004", "Phạm Minh Dũng", "dung_se170004@fpt.edu.vn"),
                    ("SE170005", "Hoàng Ngọc Em", "em_se170005@fpt.edu.vn"),
                    ("SE170006", "Đặng Quốc Gia", "gia_se170006@fpt.edu.vn"),
                    ("SE170007", "Bùi Thanh Hà", "ha_se170007@fpt.edu.vn"),
                    ("SE170008", "Võ Văn Khang", "khang_se170008@fpt.edu.vn")
                };

                foreach (var (code, fullName, email) in sampleStudents)
                {
                    var u = await db.Users.FirstOrDefaultAsync(x => x.StudentCode == code || x.Email.ToLower() == email.ToLower());
                    if (u == null)
                    {
                        u = new User
                        {
                            Id = Guid.NewGuid(),
                            StudentCode = code,
                            Email = email,
                            FullName = fullName,
                            Role = UserRole.Student,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.Users.Add(u);
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        bool updated = false;
                        if (string.IsNullOrEmpty(u.StudentCode)) { u.StudentCode = code; updated = true; }
                        if (string.IsNullOrEmpty(u.FullName) || u.FullName == code) { u.FullName = fullName; updated = true; }
                        if (updated) await db.SaveChangesAsync();
                    }
                }

                await uow.Classes.AddStudentsToClassAsync(se1703Class.Id, sampleStudents.Select(s => s.Item1));
                logger.LogInformation("Enrolled sample students into class SE1703");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi seed dữ liệu đào tạo (AcademicDataSeeder)");
        }
    }
}

