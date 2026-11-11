using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDbSchemaTo2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "credits",
                table: "courses",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "lecturer_profiles",
                columns: table => new
                {
                    lecturer_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecturer_profiles", x => x.lecturer_code);
                    table.ForeignKey(
                        name: "FK_lecturer_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    building = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    academic_year = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semesters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                columns: table => new
                {
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_year = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_profiles", x => x.student_code);
                    table.ForeignKey(
                        name: "FK_student_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_slots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classes", x => x.id);
                    table.ForeignKey(
                        name: "FK_classes_courses_course_id",
                        column: x => x.course_id,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_classes_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "class_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_schedules_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_schedules_lecturer_profiles_lecturer_code",
                        column: x => x.lecturer_code,
                        principalTable: "lecturer_profiles",
                        principalColumn: "lecturer_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_schedules_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_schedules_time_slots_time_slot_id",
                        column: x => x.time_slot_id,
                        principalTable: "time_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    topic_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    analysis_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_groups_analysis_runs_analysis_run_id",
                        column: x => x.analysis_run_id,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_groups_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oral_exams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oral_exams", x => x.id);
                    table.ForeignKey(
                        name: "FK_oral_exams_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_classes",
                columns: table => new
                {
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_classes", x => new { x.student_code, x.class_id });
                    table.ForeignKey(
                        name: "FK_student_classes_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_classes_student_profiles_student_code",
                        column: x => x.student_code,
                        principalTable: "student_profiles",
                        principalColumn: "student_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "class_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    learn_date = table.Column<DateOnly>(type: "date", nullable: false),
                    time_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecturer_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_class_sessions_class_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "class_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_class_sessions_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_sessions_lecturer_profiles_lecturer_code",
                        column: x => x.lecturer_code,
                        principalTable: "lecturer_profiles",
                        principalColumn: "lecturer_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_sessions_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_sessions_time_slots_time_slot_id",
                        column: x => x.time_slot_id,
                        principalTable: "time_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Member"),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => new { x.group_id, x.student_code });
                    table.ForeignKey(
                        name: "FK_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_members_student_profiles_student_code",
                        column: x => x.student_code,
                        principalTable: "student_profiles",
                        principalColumn: "student_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oral_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oral_exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    reference_context = table.Column<string>(type: "text", nullable: true),
                    expected_keywords = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oral_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_oral_questions_generated_questions_generated_question_id",
                        column: x => x.generated_question_id,
                        principalTable: "generated_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_oral_questions_oral_exams_oral_exam_id",
                        column: x => x.oral_exam_id,
                        principalTable: "oral_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_oral_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    oral_exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    examiner_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    audio_record_url = table.Column<string>(type: "text", nullable: true),
                    screen_record_url = table.Column<string>(type: "text", nullable: true),
                    ai_evaluation_summary = table.Column<string>(type: "text", nullable: true),
                    final_feedback = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_oral_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_oral_sessions_lecturer_profiles_examiner_code",
                        column: x => x.examiner_code,
                        principalTable: "lecturer_profiles",
                        principalColumn: "lecturer_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_oral_sessions_oral_exams_oral_exam_id",
                        column: x => x.oral_exam_id,
                        principalTable: "oral_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_oral_sessions_student_profiles_student_code",
                        column: x => x.student_code,
                        principalTable: "student_profiles",
                        principalColumn: "student_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    comments = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_class_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_attendance_student_profiles_student_code",
                        column: x => x.student_code,
                        principalTable: "student_profiles",
                        principalColumn: "student_code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_oral_answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_answer = table.Column<string>(type: "text", nullable: true),
                    feedback = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_oral_answers", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_oral_answers_oral_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "oral_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_oral_answers_student_oral_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "student_oral_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_session_id",
                table: "attendance",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_student_code",
                table: "attendance",
                column: "student_code");

            migrationBuilder.CreateIndex(
                name: "IX_class_schedules_class_id",
                table: "class_schedules",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_schedules_lecturer_code",
                table: "class_schedules",
                column: "lecturer_code");

            migrationBuilder.CreateIndex(
                name: "IX_class_schedules_room_id",
                table: "class_schedules",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_schedules_time_slot_id",
                table: "class_schedules",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_class_id",
                table: "class_sessions",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_lecturer_code",
                table: "class_sessions",
                column: "lecturer_code");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_room_id",
                table: "class_sessions",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_schedule_id",
                table: "class_sessions",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_sessions_time_slot_id",
                table: "class_sessions",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_course_id",
                table: "classes",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_classes_semester_id",
                table: "classes",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_student_code",
                table: "group_members",
                column: "student_code");

            migrationBuilder.CreateIndex(
                name: "IX_groups_analysis_run_id",
                table: "groups",
                column: "analysis_run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_class_id",
                table: "groups",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_lecturer_profiles_user_id",
                table: "lecturer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oral_exams_class_id",
                table: "oral_exams",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_oral_questions_generated_question_id",
                table: "oral_questions",
                column: "generated_question_id");

            migrationBuilder.CreateIndex(
                name: "IX_oral_questions_oral_exam_id",
                table: "oral_questions",
                column: "oral_exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_code",
                table: "rooms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_classes_class_id",
                table: "student_classes",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_oral_answers_question_id",
                table: "student_oral_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_oral_answers_session_id",
                table: "student_oral_answers",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_oral_sessions_examiner_code",
                table: "student_oral_sessions",
                column: "examiner_code");

            migrationBuilder.CreateIndex(
                name: "IX_student_oral_sessions_oral_exam_id",
                table: "student_oral_sessions",
                column: "oral_exam_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_oral_sessions_student_code",
                table: "student_oral_sessions",
                column: "student_code");

            migrationBuilder.CreateIndex(
                name: "IX_student_profiles_user_id",
                table: "student_profiles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "student_classes");

            migrationBuilder.DropTable(
                name: "student_oral_answers");

            migrationBuilder.DropTable(
                name: "class_sessions");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "oral_questions");

            migrationBuilder.DropTable(
                name: "student_oral_sessions");

            migrationBuilder.DropTable(
                name: "class_schedules");

            migrationBuilder.DropTable(
                name: "oral_exams");

            migrationBuilder.DropTable(
                name: "student_profiles");

            migrationBuilder.DropTable(
                name: "lecturer_profiles");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "time_slots");

            migrationBuilder.DropTable(
                name: "classes");

            migrationBuilder.DropTable(
                name: "semesters");

            migrationBuilder.DropColumn(
                name: "address",
                table: "users");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "credits",
                table: "courses");
        }
    }
}
