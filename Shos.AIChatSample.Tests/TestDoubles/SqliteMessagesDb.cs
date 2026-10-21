using FCAIChat.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Tests.TestDoubles;

/// <summary>テスト用のメッセージ DB (SQLite のインメモリ DB)</summary>
/// <remarks>
/// SQL Server LocalDB を使わずに、値コンバーターや ExecuteDeleteAsync を含めて実際の DB と同じように動かすために SQLite を使う。
/// インメモリ DB は接続を閉じると消えるので、このオブジェクトの破棄まで接続を開いたままにする。
/// </remarks>
public sealed class SqliteMessagesDb : IDisposable
{
    readonly SqliteConnection connection = new("DataSource=:memory:");

    /// <summary>DB を作成する (テーブルも作る)</summary>
    public SqliteMessagesDb()
    {
        connection.Open();
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    /// <summary>同じ DB に接続する新しい DbContext を作る</summary>
    /// <remarks>変更追跡のキャッシュを使わずに DB から読み直したいときは、新しい DbContext を使う。</remarks>
    public MessagesDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<MessagesDbContext>().UseSqlite(connection).Options);

    /// <summary>メッセージを追加する</summary>
    /// <returns>追加したメッセージ (ID は採番済み)</returns>
    public Message[] AddMessages(params Message[] messages)
    {
        using var dbContext = CreateDbContext();
        dbContext.Messages.AddRange(messages);
        dbContext.SaveChanges();
        return messages;
    }

    /// <inheritdoc/>
    public void Dispose() => connection.Dispose();
}
