using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FCAIChat.Data;

/// <summary>ASP.NET Core Identity (ユーザー / ロール / ログイン情報など) 用の DbContext</summary>
/// <remarks>
/// テーブルの定義は基底クラスの IdentityDbContext が持つので、ここでは何も追加していない。
/// マイグレーションは Migrations フォルダーに出力する (dotnet ef コマンドでは --context ApplicationDbContext を指定)。
/// </remarks>
/// <param name="options">接続設定 (Program.cs で登録)</param>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options);
