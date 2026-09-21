using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Courier.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DeactivatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReactivatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UnchangedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Subject = table.Column<string>(type: "TEXT", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    VoiceRecordingPath = table.Column<string>(type: "TEXT", nullable: true),
                    AudienceDescription = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Ward = table.Column<string>(type: "TEXT", nullable: true),
                    Age = table.Column<int>(type: "INTEGER", nullable: true),
                    BirthMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    BirthDay = table.Column<int>(type: "INTEGER", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    LcrEmail = table.Column<string>(type: "TEXT", nullable: true),
                    LcrPhone = table.Column<string>(type: "TEXT", nullable: true),
                    PreferredChannel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeactivatedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FirstSeenOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LastSeenOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Normalized = table.Column<string>(type: "TEXT", nullable: true),
                    AreaCodeAssumed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    IsPreferred = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LastSeenInLcrOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EnteredInLcrOn = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactPoints_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MessageBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CostMicros = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageDeliveries_MessageBatches_MessageBatchId",
                        column: x => x.MessageBatchId,
                        principalTable: "MessageBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageDeliveries_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Field = table.Column<string>(type: "TEXT", nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonChanges_ImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "ImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonChanges_People_PersonId",
                        column: x => x.PersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPoints_PersonId_Kind",
                table: "ContactPoints",
                columns: new[] { "PersonId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPoints_PersonId_Kind_Normalized",
                table: "ContactPoints",
                columns: new[] { "PersonId", "Kind", "Normalized" },
                unique: true,
                filter: "\"Normalized\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPoints_Source_EnteredInLcrOn_LastSeenInLcrOn",
                table: "ContactPoints",
                columns: new[] { "Source", "EnteredInLcrOn", "LastSeenInLcrOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRuns_ImportedAt",
                table: "ImportRuns",
                column: "ImportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRuns_Sha256",
                table: "ImportRuns",
                column: "Sha256");

            migrationBuilder.CreateIndex(
                name: "IX_MessageBatches_CreatedAt",
                table: "MessageBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveries_MessageBatchId_Status",
                table: "MessageDeliveries",
                columns: new[] { "MessageBatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveries_PersonId",
                table: "MessageDeliveries",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_People_IsActive",
                table: "People",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_People_LastName_FirstName",
                table: "People",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_People_Ward",
                table: "People",
                column: "Ward");

            migrationBuilder.CreateIndex(
                name: "IX_PersonChanges_ImportRunId",
                table: "PersonChanges",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonChanges_PersonId",
                table: "PersonChanges",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactPoints");

            migrationBuilder.DropTable(
                name: "MessageDeliveries");

            migrationBuilder.DropTable(
                name: "PersonChanges");

            migrationBuilder.DropTable(
                name: "MessageBatches");

            migrationBuilder.DropTable(
                name: "ImportRuns");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
