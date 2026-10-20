using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Data;

/// <summary>チャットメッセージ用の DbContext</summary>
/// <remarks>
/// Identity 用の <see cref="ApplicationDbContext"/> と同じ DB を使うが、マイグレーションは別に管理する
/// (dotnet ef コマンドでは --context MessagesDbContext を指定し、出力先は Migrations/MessagesDb)。
/// </remarks>
/// <param name="options">接続設定 (Program.cs で登録)</param>
public class MessagesDbContext(DbContextOptions<MessagesDbContext> options) : DbContext(options)
{
    /// <summary>チャットメッセージ (全ユーザーで共有するチャットログ)</summary>
    public virtual DbSet<Message> Messages { get; set; }

    /// <summary>モデルの構成</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // CreatedAt は UTC で保存しているので、読み出し時に必ず DateTimeKind.Utc にする
        // (datetime2 は Kind を保持しないため。これにより ToString("o") に "Z" が付く)
        // - 保存時: そのまま / 読み出し時: Kind を Utc に設定
        // - DB の列の型は変わらないので、マイグレーションは不要
        modelBuilder.Entity<Message>()
                    .Property(message => message.CreatedAt)
                    .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

/// <summary>チャットメッセージ 1 件</summary>
public class Message
{
    /// <summary>ID (DB への保存時に自動で採番される)</summary>
    public int      Id        { get; set; }
    /// <summary>本文</summary>
    public string   Content   { get; set; } = string.Empty;
    /// <summary>送信者名 (ユーザー名、または AI エージェント名)</summary>
    public string   UserName  { get; set; } = string.Empty;
    /// <summary>送信日時 (UTC)</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
