using Microsoft.EntityFrameworkCore;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Data;

public class SchoolScheduleDbContext : DbContext
{
    public SchoolScheduleDbContext(DbContextOptions<SchoolScheduleDbContext> options) : base(options)
    {
    }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RoomTeacherAssignment> RoomTeacherAssignments => Set<RoomTeacherAssignment>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<TeacherUnavailability> TeacherUnavailabilities => Set<TeacherUnavailability>();
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();
    public DbSet<ClassSubjectGroup> ClassSubjectGroups => Set<ClassSubjectGroup>();
    public DbSet<ScheduleSettings> ScheduleSettings => Set<ScheduleSettings>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<ScheduledLesson> ScheduledLessons => Set<ScheduledLesson>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<Substitution> Substitutions => Set<Substitution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Room ---
        modelBuilder.Entity<Room>(e =>
        {
            e.Property(r => r.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(r => r.Name).IsUnique();

            e.HasOne(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- RoomType ---
        modelBuilder.Entity<RoomType>(e =>
        {
            e.Property(rt => rt.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(rt => rt.Name).IsUnique();

            // Стартовый набор типов, чтобы список не был пустым при первом запуске —
            // дальше администратор пополняет его сам на странице "Кабинеты".
            e.HasData(
                new { Id = 1, Name = "Обычный" },
                new { Id = 2, Name = "Спортзал" },
                new { Id = 3, Name = "Лаборатория" },
                new { Id = 4, Name = "Компьютерный класс" },
                new { Id = 5, Name = "Актовый зал" },
                new { Id = 6, Name = "Мастерская" });
        });

        // --- RoomTeacherAssignment ---
        modelBuilder.Entity<RoomTeacherAssignment>(e =>
        {
            e.HasOne(a => a.Room)
                .WithMany(r => r.AssignedTeachers)
                .HasForeignKey(a => a.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Teacher)
                .WithMany(t => t.AssignedRooms)
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(a => new { a.RoomId, a.TeacherId }).IsUnique();
        });

        // --- Subject ---
        modelBuilder.Entity<Subject>(e =>
        {
            e.Property(s => s.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(s => s.Name).IsUnique();

            e.HasOne(s => s.RequiredRoomType)
                .WithMany(rt => rt.SubjectsRequiringThis)
                .HasForeignKey(s => s.RequiredRoomTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- Teacher ---
        modelBuilder.Entity<Teacher>(e =>
        {
            e.Property(t => t.FullName).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<TeacherSubject>(e =>
        {
            e.HasOne(ts => ts.Teacher)
                .WithMany(t => t.TeacherSubjects)
                .HasForeignKey(ts => ts.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(ts => ts.Subject)
                .WithMany(s => s.TeacherSubjects)
                .HasForeignKey(ts => ts.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(ts => new { ts.TeacherId, ts.SubjectId }).IsUnique();
        });

        modelBuilder.Entity<TeacherUnavailability>(e =>
        {
            e.HasOne(u => u.Teacher)
                .WithMany(t => t.Unavailabilities)
                .HasForeignKey(u => u.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- SchoolClass ---
        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.Property(c => c.Name).IsRequired().HasMaxLength(20);
            e.HasIndex(c => c.Name).IsUnique();

            e.HasOne(c => c.HomeRoom)
                .WithMany()
                .HasForeignKey(c => c.HomeRoomId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- ClassSubjectGroup ---
        modelBuilder.Entity<ClassSubjectGroup>(e =>
        {
            e.HasOne(g => g.Class)
                .WithMany(c => c.SubjectGroups)
                .HasForeignKey(g => g.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(g => g.Subject)
                .WithMany(s => s.ClassSubjectGroups)
                .HasForeignKey(g => g.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(g => g.Teacher)
                .WithMany(t => t.ClassSubjectGroups)
                .HasForeignKey(g => g.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            // Класс+предмет+подгруппа — уникальная комбинация. SQL считает NULL != NULL, поэтому
            // обычный составной уникальный индекс пропустил бы две записи "весь класс" (GroupLabel=null)
            // для одного и того же предмета — нужны два частичных индекса на разные случаи.
            e.HasIndex(g => new { g.ClassId, g.SubjectId, g.GroupLabel })
                .IsUnique()
                .HasFilter("\"GroupLabel\" IS NOT NULL");
            e.HasIndex(g => new { g.ClassId, g.SubjectId })
                .IsUnique()
                .HasFilter("\"GroupLabel\" IS NULL");
        });

        // --- TimeSlot ---
        modelBuilder.Entity<TimeSlot>(e =>
        {
            e.HasIndex(t => new { t.Shift, t.Day, t.PeriodNumber }).IsUnique();
        });

        // --- ScheduledLesson ---
        modelBuilder.Entity<ScheduledLesson>(e =>
        {
            e.HasOne(l => l.ClassSubjectGroup)
                .WithMany(g => g.ScheduledLessons)
                .HasForeignKey(l => l.ClassSubjectGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Teacher)
                .WithMany(t => t.ScheduledLessons)
                .HasForeignKey(l => l.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(l => l.Room)
                .WithMany(r => r.ScheduledLessons)
                .HasForeignKey(l => l.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(l => l.TimeSlot)
                .WithMany(t => t.ScheduledLessons)
                .HasForeignKey(l => l.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- Absence ---
        modelBuilder.Entity<Absence>(e =>
        {
            e.HasOne(a => a.Teacher)
                .WithMany(t => t.Absences)
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Substitution ---
        modelBuilder.Entity<Substitution>(e =>
        {
            e.HasOne(s => s.ScheduledLesson)
                .WithMany(l => l.Substitutions)
                .HasForeignKey(s => s.ScheduledLessonId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.SubstituteTeacher)
                .WithMany()
                .HasForeignKey(s => s.SubstituteTeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.SubstituteRoom)
                .WithMany()
                .HasForeignKey(s => s.SubstituteRoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // На одном уроке в одну дату может быть только одна запись о замене.
            e.HasIndex(s => new { s.ScheduledLessonId, s.Date }).IsUnique();
        });
    }
}
