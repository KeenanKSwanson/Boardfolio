using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Boardfolio.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BuildMediaLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuildMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KeyboardBuildId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildMedia_Builds_KeyboardBuildId",
                        column: x => x.KeyboardBuildId,
                        principalTable: "Builds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildMedia_KeyboardBuildId",
                table: "BuildMedia",
                column: "KeyboardBuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildMedia");
        }
    }
}
