using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PennyPath.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSpending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => new { x.OwnerId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpendingProfiles",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendingProfiles", x => x.OwnerId);
                    table.ForeignKey(
                        name: "FK_SpendingProfiles_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseSubcategories",
                columns: table => new
                {
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubcategoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseSubcategories", x => new { x.OwnerId, x.CategoryId, x.SubcategoryId });
                    table.ForeignKey(
                        name: "FK_ExpenseSubcategories_ExpenseCategories_OwnerId_CategoryId",
                        columns: x => new { x.OwnerId, x.CategoryId },
                        principalTable: "ExpenseCategories",
                        principalColumns: new[] { "OwnerId", "CategoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubcategoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.CheckConstraint("CK_Expenses_Name", "length(trim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = trim(\"Name\")");
                    table.CheckConstraint("CK_Expenses_PositiveAmount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_Expenses_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_OwnerId_CategoryId",
                        columns: x => new { x.OwnerId, x.CategoryId },
                        principalTable: "ExpenseCategories",
                        principalColumns: new[] { "OwnerId", "CategoryId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseSubcategories_OwnerId_CategoryId_Subcategor~",
                        columns: x => new { x.OwnerId, x.CategoryId, x.SubcategoryId },
                        principalTable: "ExpenseSubcategories",
                        principalColumns: new[] { "OwnerId", "CategoryId", "SubcategoryId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_OwnerId_CategoryId_SubcategoryId",
                table: "Expenses",
                columns: new[] { "OwnerId", "CategoryId", "SubcategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_OwnerId_PurchaseDate_RecordedAtUtc_Id",
                table: "Expenses",
                columns: new[] { "OwnerId", "PurchaseDate", "RecordedAtUtc", "Id" },
                descending: new[] { false, true, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "SpendingProfiles");

            migrationBuilder.DropTable(
                name: "ExpenseSubcategories");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");
        }
    }
}
