using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tally.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing entries move onto the fixed ids of the four default meals (DefaultMeals).
            migrationBuilder.AddColumn<Guid>(
                name: "MealId",
                table: "DiaryEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "DiaryEntries" SET "MealId" = CASE "Meal"
                    WHEN 'Breakfast' THEN '0a11e000-0000-4000-8000-000000000001'::uuid
                    WHEN 'Lunch' THEN '0a11e000-0000-4000-8000-000000000002'::uuid
                    WHEN 'Dinner' THEN '0a11e000-0000-4000-8000-000000000003'::uuid
                    ELSE '0a11e000-0000-4000-8000-000000000004'::uuid
                END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MealId",
                table: "DiaryEntries",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Meal",
                table: "DiaryEntries");

            migrationBuilder.CreateTable(
                name: "MealPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    MealIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    MealNames = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlans_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_ProfileId_EffectiveFrom",
                table: "MealPlans",
                columns: new[] { "ProfileId", "EffectiveFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealPlans");

            // Entries in custom meals have no equivalent and fall back to Snacks.
            migrationBuilder.AddColumn<string>(
                name: "Meal",
                table: "DiaryEntries",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Snacks");

            migrationBuilder.Sql("""
                UPDATE "DiaryEntries" SET "Meal" = CASE "MealId"
                    WHEN '0a11e000-0000-4000-8000-000000000001'::uuid THEN 'Breakfast'
                    WHEN '0a11e000-0000-4000-8000-000000000002'::uuid THEN 'Lunch'
                    WHEN '0a11e000-0000-4000-8000-000000000003'::uuid THEN 'Dinner'
                    ELSE 'Snacks'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Meal",
                table: "DiaryEntries",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24,
                oldDefaultValue: "Snacks");

            migrationBuilder.DropColumn(
                name: "MealId",
                table: "DiaryEntries");
        }
    }
}
