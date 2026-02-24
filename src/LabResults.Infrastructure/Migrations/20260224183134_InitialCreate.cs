using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LabResults.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    PatientIdValue = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResultStatus = table.Column<string>(type: "text", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultType = table.Column<string>(type: "text", nullable: true),
                    ResultNumeric = table.Column<decimal>(type: "numeric", nullable: true),
                    ResultUnit = table.Column<string>(type: "text", nullable: true),
                    ResultRefMin = table.Column<decimal>(type: "numeric", nullable: true),
                    ResultRefMax = table.Column<decimal>(type: "numeric", nullable: true),
                    ResultNotes = table.Column<string>(type: "text", nullable: true),
                    ResultCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidationNotes = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_Status",
                table: "Samples",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Samples");
        }
    }
}
