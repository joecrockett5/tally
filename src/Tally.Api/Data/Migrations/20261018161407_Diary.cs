using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tally.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Diary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Meal = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    FoodId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceLabel = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Basis = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PortionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Unit = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Quantity = table.Column<double>(type: "double precision", nullable: false),
                    ServingName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ServingAmount = table.Column<double>(type: "double precision", nullable: true),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PerBasis_Carbohydrate = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_EnergyKcal = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_EnergyKj = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_Fat = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_Fibre = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_Protein = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_Salt = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_Saturates = table.Column<double>(type: "double precision", nullable: true),
                    PerBasis_Sugars = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Foods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "Foods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_FoodId",
                table: "DiaryEntries",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_ProfileId_CreatedAt",
                table: "DiaryEntries",
                columns: new[] { "ProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_ProfileId_Date",
                table: "DiaryEntries",
                columns: new[] { "ProfileId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaryEntries");
        }
    }
}
