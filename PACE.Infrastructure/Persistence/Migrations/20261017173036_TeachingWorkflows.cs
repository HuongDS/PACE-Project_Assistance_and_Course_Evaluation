using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeachingWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_users_StudentHasRepo",
                table: "users");

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Semester = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LecturerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_courses_users_LecturerId",
                        column: x => x.LecturerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_projects",
                columns: table => new
                {
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_projects", x => x.AnalysisRunId);
                    table.ForeignKey(
                        name: "FK_course_projects_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_course_projects_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualStartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RubricJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_sessions", x => x.Id);
                    table.CheckConstraint("CK_session_status", "\"Status\" IN ('CREATED','IN_PROGRESS','COMPLETED')");
                    table.ForeignKey(
                        name: "FK_assessment_sessions_course_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "course_projects",
                        principalColumn: "AnalysisRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_students",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationJson = table.Column<string>(type: "text", nullable: true),
                    EvaluatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_students", x => new { x.SessionId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_session_students_assessment_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "assessment_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_students_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_StudentHasRepo",
                table: "users",
                sql: "\"Role\" IN ('Admin', 'Lecturer') OR \"AnalysisRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_assessment_sessions_ProjectId_ScheduledStart",
                table: "assessment_sessions",
                columns: new[] { "ProjectId", "ScheduledStart" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ResourceId_Timestamp",
                table: "audit_events",
                columns: new[] { "ResourceId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_course_projects_CourseId",
                table: "course_projects",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_courses_Code_Semester_LecturerId",
                table: "courses",
                columns: new[] { "Code", "Semester", "LecturerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courses_LecturerId",
                table: "courses",
                column: "LecturerId");

            migrationBuilder.CreateIndex(
                name: "IX_session_students_StudentId",
                table: "session_students",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "session_students");

            migrationBuilder.DropTable(
                name: "assessment_sessions");

            migrationBuilder.DropTable(
                name: "course_projects");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_users_StudentHasRepo",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_StudentHasRepo",
                table: "users",
                sql: "\"Role\" = 'Admin' OR \"AnalysisRunId\" IS NOT NULL");
        }
    }
}
