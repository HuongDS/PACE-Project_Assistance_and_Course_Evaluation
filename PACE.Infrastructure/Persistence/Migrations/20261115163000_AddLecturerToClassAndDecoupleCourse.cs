using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PACE.Infrastructure.Persistence;

#nullable disable

namespace PACE.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261115163000_AddLecturerToClassAndDecoupleCourse")]
    public partial class AddLecturerToClassAndDecoupleCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lecturer_id",
                table: "classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
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
            ");

            migrationBuilder.CreateIndex(
                name: "IX_classes_lecturer_id",
                table: "classes",
                column: "lecturer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_users_lecturer_id",
                table: "classes",
                column: "lecturer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_classes_course_id_semester_id_name",
                table: "classes",
                columns: new[] { "course_id", "semester_id", "name" },
                unique: true);

            migrationBuilder.Sql(@"
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
                END $$;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_courses_Code",
                table: "courses",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_courses_Code",
                table: "courses");

            migrationBuilder.DropIndex(
                name: "IX_classes_course_id_semester_id_name",
                table: "classes");

            migrationBuilder.DropForeignKey(
                name: "FK_classes_users_lecturer_id",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_lecturer_id",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "lecturer_id",
                table: "classes");
        }
    }
}
