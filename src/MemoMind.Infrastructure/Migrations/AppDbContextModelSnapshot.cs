using System;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace MemoMind.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
public partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.8")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("MemoMind.Core.Models.CalendarEvent", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("EventTitle")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("EndTime")
                .HasColumnType("TEXT");

            b.Property<string>("Notes")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<int?>("RelatedTaskId")
                .HasColumnType("INTEGER");

            b.Property<DateTime>("StartTime")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("CalendarEvents");
        });

        modelBuilder.Entity("MemoMind.Core.Models.EmotionLog", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("EmotionLabel")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<int>("EmotionScore")
                .HasColumnType("INTEGER");

            b.HasKey("Id");

            b.ToTable("EmotionLogs");
        });

        modelBuilder.Entity("MemoMind.Core.Models.FileWorkspace", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("DisplayName")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime?>("LastOpenedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Path")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("FileWorkspaces");
        });

        modelBuilder.Entity("MemoMind.Core.Models.PomodoroSession", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<bool>("IsCompleted")
                .HasColumnType("INTEGER");

            b.Property<int?>("TaskId")
                .HasColumnType("INTEGER");

            b.Property<int>("DurationMinutes")
                .HasColumnType("INTEGER");

            b.Property<DateTime?>("EndTime")
                .HasColumnType("TEXT");

            b.Property<DateTime>("StartTime")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("PomodoroSessions");
        });

        modelBuilder.Entity("MemoMind.Core.Models.MemoryItem", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Category")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("Memories");
        });

        modelBuilder.Entity("MemoMind.Core.Models.ChatMessageRecord", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<bool>("IsUserMessage")
                .HasColumnType("INTEGER");

            b.Property<string>("Sender")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("Time")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("ChatMessages");
        });

        modelBuilder.Entity("MemoMind.Core.Models.TaskItem", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            b.Property<DateTime?>("CompletedAt")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<DateTime?>("DueDate")
                .HasColumnType("TEXT");

            b.Property<bool>("IsUrgent")
                .HasColumnType("INTEGER");

            b.Property<string>("SourceType")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Status")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Description")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("Title")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("Tasks");
        });
    }
}
