// アプリケーションのエントリーポイント
// - サービス (DB / 認証 / Razor Pages / SignalR / AI エージェント) を DI コンテナに登録し、
//   HTTP リクエストの処理パイプラインを組み立てて起動する
using FCAIChat.AIAgents;
using FCAIChat.Data;
using FCAIChat.Hubs;
using FCAIChat.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- サービスの登録 ----

// データベース
// - Identity (ユーザー情報) 用とチャットメッセージ用の 2 つの DbContext が、同じ接続文字列 (同じ DB) を使う
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDbContext<MessagesDbContext>(options => options.UseSqlServer(connectionString));
// 開発時に、マイグレーション未適用などの DB エラーを分かりやすいページで表示する
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 認証 (ASP.NET Core Identity の既定 UI: ユーザー登録 / ログイン / ログアウトなど)
// - RequireConfirmedAccount = true なので、登録後にメールアドレスの確認が済むまでログインできない
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();

// 画面 (Razor Pages) と、リアルタイム通信 (SignalR)
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// AI エージェントの接続設定
// - 構成セクション "ChatAgent" を優先し、未設定なら従来の環境変数名から読み込む
builder.Services.AddOptions<ChatAgentOptions>()
                .Bind(builder.Configuration.GetSection(ChatAgentOptions.SectionName))
                .PostConfigure<IConfiguration>((options, configuration) => {
                    options.Endpoint    = FirstNonEmpty(options.Endpoint   , configuration["AZURE_OPENAI_ENDPOINT"]);
                    options.ApiKey      = FirstNonEmpty(options.ApiKey     , configuration["AZURE_OPENAI_API_KEY" ]);
                    options.BraveApiKey = FirstNonEmpty(options.BraveApiKey, configuration["BRAVE_API_KEY"        ]);

                    // value が空なら fallback を使う
                    static string FirstNonEmpty(string value, string? fallback)
                        => string.IsNullOrEmpty(value) ? fallback ?? string.Empty : value;
                });
// AI エージェント本体
// - AIAgent / IChatClient / MCP クライアントはアプリ全体で共有する (破棄はアプリ終了時に DI コンテナが行う)
builder.Services.AddSingleton<ChatAgent, MyChatAgent>();

// エージェントとの会話 (AgentSession) の保存先
// - インメモリ実装なので、アプリを再起動すると会話の文脈は消える
builder.Services.AddSingleton<IThreadStore, InMemoryThreadStore>();

var app = builder.Build();

// ---- HTTP リクエストの処理パイプライン ----

if (app.Environment.IsDevelopment()) {
    // 開発時: マイグレーションを画面から適用できるエンドポイントを有効にする
    app.UseMigrationsEndPoint();
} else {
    // 本番時: 例外発生時は /Error ページを表示する
    app.UseExceptionHandler("/Error");
    // HSTS の既定値は 30 日。本番環境では変更を検討すること (https://aka.ms/aspnetcore-hsts)
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
// [Authorize] 属性による認可を有効にする (認証ミドルウェアは Identity の登録により自動で追加される)
app.UseAuthorization();

// 静的ファイル (wwwroot)、Razor Pages、SignalR のハブをルーティングに登録する
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapHub<ChatHub>("/chatHub");

app.Run();
