using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MemoMind.Infrastructure.Migrations;

/// <summary>
/// EF Core 数据库迁移：创建 ChatMessages 表。
///
/// 表结构：
/// - Id: INTEGER 主键自增
/// - Sender: TEXT 发送者名称
/// - Content: TEXT 消息正文
/// - Time: TEXT 发送时间（DateTime 在 SQLite 中以文本存储）
/// - IsUserMessage: INTEGER 布尔值（SQLite 无 native bool 类型）
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("202605150001_AddChatMessages")]
public partial class AddChatMessages : Migration
{
    /// <summary>执行迁移：创建 ChatMessages 表</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Sender = table.Column<string>(type: "TEXT", nullable: false),
                Content = table.Column<string>(type: "TEXT", nullable: false),
                Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsUserMessage = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
            });
    }

    /// <summary>回滚迁移：删除 ChatMessages 表</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChatMessages");
    }
}
