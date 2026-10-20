using FCAIChat.AIAgents;
using FCAIChat.Data;
using FCAIChat.Hubs;
using FCAIChat.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDbContext<MessagesDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

// AI エージェント
// - 構成セクション "ChatAgent" を優先し、未設定なら従来の環境変数名から読み込む
builder.Services.AddOptions<ChatAgentOptions>()
                .Bind(builder.Configuration.GetSection(ChatAgentOptions.SectionName))
                .PostConfigure<IConfiguration>((options, configuration) => {
                    options.Endpoint    = FirstNonEmpty(options.Endpoint   , configuration["AZURE_OPENAI_ENDPOINT"]);
                    options.ApiKey      = FirstNonEmpty(options.ApiKey     , configuration["AZURE_OPENAI_API_KEY" ]);
                    options.BraveApiKey = FirstNonEmpty(options.BraveApiKey, configuration["BRAVE_API_KEY"        ]);

                    static string FirstNonEmpty(string value, string? fallback)
                        => string.IsNullOrEmpty(value) ? fallback ?? string.Empty : value;
                });
// AIAgent / IChatClient / MCP クライアントはアプリ全体で共有する (破棄はアプリ終了時に DI コンテナが行う)
builder.Services.AddSingleton<ChatAgent, MyChatAgent>();

// Register thread store service
builder.Services.AddSingleton<IThreadStore, InMemoryThreadStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseMigrationsEndPoint();
} else {
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();
app.MapHub<ChatHub>("/chatHub");

app.Run();
