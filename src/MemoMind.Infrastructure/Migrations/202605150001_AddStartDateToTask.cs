using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemoMind.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202605150001_AddStartDateToTask")]
public partial class AddStartDateToTask : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "StartDate",
            table: "Tasks",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StartDate",
            table: "Tasks");
    }
}
