using FCAIChat.AIAgents;
using FCAIChat.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FCAIChat.Tests.Integration;

/// <summary>Program.cs での <see cref="ChatAgentOptions"/> の読み込み (構成セクション → 従来の環境変数名) と DI 登録のテスト</summary>
public class ChatAgentOptionsConfigurationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    // 指定した構成で起動したアプリから ChatAgentOptions を取得する
    // - 実行環境の環境変数の影響を受けないように、すべての項目を明示的に設定する
    ChatAgentOptions GetOptions(Dictionary<string, string?> settings)
    {
        Dictionary<string, string?> allSettings = new() {
            ["ChatAgent:Endpoint"      ] = "",
            ["ChatAgent:ApiKey"        ] = "",
            ["ChatAgent:DeploymentName"] = "",
            ["ChatAgent:BraveApiKey"   ] = "",
            ["AZURE_OPENAI_ENDPOINT"   ] = "",
            ["AZURE_OPENAI_API_KEY"    ] = "",
            ["BRAVE_API_KEY"           ] = ""
        };
        foreach (var (key, value) in settings)
            allSettings[key] = value;

        using var customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(allSettings)));
        return customFactory.Services.GetRequiredService<IOptions<ChatAgentOptions>>().Value;
    }

    [Fact]
    public void 構成セクションの値を使う()
    {
        var options = GetOptions(new() {
            ["ChatAgent:Endpoint"      ] = "https://section.openai.azure.com/",
            ["ChatAgent:ApiKey"        ] = "section-api-key",
            ["ChatAgent:DeploymentName"] = "gpt-4o",
            ["ChatAgent:BraveApiKey"   ] = "section-brave-key"
        });

        Assert.Equal("https://section.openai.azure.com/", options.Endpoint      );
        Assert.Equal("section-api-key"                  , options.ApiKey        );
        Assert.Equal("gpt-4o"                           , options.DeploymentName);
        Assert.Equal("section-brave-key"                , options.BraveApiKey   );
    }

    [Fact]
    public void 構成セクションが空なら従来の環境変数名の値を使う()
    {
        var options = GetOptions(new() {
            ["AZURE_OPENAI_ENDPOINT"] = "https://env.openai.azure.com/",
            ["AZURE_OPENAI_API_KEY" ] = "env-api-key",
            ["BRAVE_API_KEY"        ] = "env-brave-key"
        });

        Assert.Equal("https://env.openai.azure.com/"     , options.Endpoint      );
        Assert.Equal("env-api-key"                       , options.ApiKey        );
        Assert.Equal(ChatAgentOptions.DefaultDeploymentName, options.DeploymentName);
        Assert.Equal("env-brave-key"                     , options.BraveApiKey   );
    }

    [Fact]
    public void 両方あれば構成セクションの値を優先する()
    {
        var options = GetOptions(new() {
            ["ChatAgent:Endpoint"   ] = "https://section.openai.azure.com/",
            ["AZURE_OPENAI_ENDPOINT"] = "https://env.openai.azure.com/",
            ["ChatAgent:ApiKey"     ] = "section-api-key",
            ["AZURE_OPENAI_API_KEY" ] = "env-api-key",
            ["ChatAgent:BraveApiKey"] = "section-brave-key",
            ["BRAVE_API_KEY"        ] = "env-brave-key"
        });

        Assert.Equal("https://section.openai.azure.com/", options.Endpoint   );
        Assert.Equal("section-api-key"                  , options.ApiKey     );
        Assert.Equal("section-brave-key"                , options.BraveApiKey);
    }

    [Fact]
    public void どちらもなければ空文字になる()
    {
        var options = GetOptions([]);

        Assert.Equal(string.Empty, options.Endpoint   );
        Assert.Equal(string.Empty, options.ApiKey     );
        Assert.Equal(string.Empty, options.BraveApiKey);
    }

    [Fact]
    public void ChatAgentとIThreadStoreはシングルトンとして登録されている()
    {
        var services = factory.Services;

        Assert.IsType<MyChatAgent>        (services.GetRequiredService<ChatAgent>());
        Assert.Same(services.GetRequiredService<ChatAgent>(), services.GetRequiredService<ChatAgent>());
        Assert.IsType<InMemoryThreadStore>(services.GetRequiredService<IThreadStore>());
        Assert.Same(services.GetRequiredService<IThreadStore>(), services.GetRequiredService<IThreadStore>());
    }

    [Fact]
    public async Task 接続設定がなければエージェントはエラーメッセージを返す()
    {
        // 設定漏れでも例外でハブが落ちず、エラーメッセージが応答になること
        // (GetChatClient より先に MCP サーバーの起動が試みられるが、BRAVE_API_KEY が空なので npx は起動しない)
        var options = GetOptions([]);
        await using var agent = new MyChatAgent(Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<MyChatAgent>.Instance);

        var (response, session) = await agent.GetResponseAsync("こんにちは", null);

        Assert.Equal("(エラーが発生したため応答できませんでした)", response);
        Assert.Null(session);
    }
}
