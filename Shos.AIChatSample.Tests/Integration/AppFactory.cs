using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace FCAIChat.Tests.Integration;

/// <summary>テスト用にアプリ全体 (Program.cs) を起動するファクトリ</summary>
/// <remarks>
/// 環境を "Testing" にして、開発者の User Secrets (Development 環境でだけ読み込まれる) の影響を受けないようにする。
/// DB (SQL Server LocalDB) にアクセスしないテストだけで使うこと。
/// </remarks>
public class AppFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseEnvironment("Testing")
                  // テスト結果が読みにくくならないように、アプリのログは出力しない
                  .ConfigureLogging(logging => logging.ClearProviders());
}
