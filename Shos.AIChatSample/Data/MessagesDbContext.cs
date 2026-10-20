using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Data;

public class MessagesDbContext : DbContext
{
    public virtual DbSet<Message> Messages { get; set; }

    public MessagesDbContext(DbContextOptions<MessagesDbContext> options)
        : base(options)
    {}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CreatedAt は UTC で保存しているので、読み出し時に必ず DateTimeKind.Utc にする
        // (datetime2 は Kind を保持しないため。これにより ToString("o") に "Z" が付く)
        modelBuilder.Entity<Message>()
                    .Property(message => message.CreatedAt)
                    .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

public class Message
{
    public int      Id        { get; set; }
    public string   Content   { get; set; } = string.Empty;
    public string   UserName  { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
