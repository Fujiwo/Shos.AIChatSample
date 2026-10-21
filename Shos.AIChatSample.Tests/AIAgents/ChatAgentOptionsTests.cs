using FCAIChat.AIAgents;
using Microsoft.Extensions.Configuration;

namespace FCAIChat.Tests.AIAgents;

/// <summary><see cref="ChatAgentOptions"/> のテスト</summary>
public class ChatAgentOptionsTests
{
    [Fact]
    public void 既定値()
    {
        var options = new ChatAgentOptions();

        Assert.Equal(string.Empty                         , options.Endpoint      );
        Assert.Equal(string.Empty                         , options.ApiKey        );
        Assert.Equal(ChatAgentOptions.DefaultDeploymentName, options.DeploymentName);
        Assert.Equal("gpt-5-mini"                         , options.DeploymentName);
        Assert.Equal(string.Empty                         , options.BraveApiKey   );
    }

    [Theory]
    [InlineData(""    )]
    [InlineData(" "   )]
    [InlineData("\t\n")]
    public void DeploymentNameに空白を設定すると既定のデプロイ名になる(string deploymentName)
    {
        var options = new ChatAgentOptions { DeploymentName = deploymentName };

        Assert.Equal(ChatAgentOptions.DefaultDeploymentName, options.DeploymentName);
    }

    [Fact]
    public void DeploymentNameに設定した値はそのまま使われる()
    {
        var options = new ChatAgentOptions { DeploymentName = "gpt-4o" };

        Assert.Equal("gpt-4o", options.DeploymentName);
    }

    [Fact]
    public void 構成セクションから読み込める()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                [$"{ChatAgentOptions.SectionName}:Endpoint"      ] = "https://example.openai.azure.com/",
                [$"{ChatAgentOptions.SectionName}:ApiKey"        ] = "api-key",
                [$"{ChatAgentOptions.SectionName}:DeploymentName"] = "gpt-4o",
                [$"{ChatAgentOptions.SectionName}:BraveApiKey"   ] = "brave-key"
            })
            .Build();

        var options = configuration.GetSection(ChatAgentOptions.SectionName).Get<ChatAgentOptions>();

        Assert.NotNull(options);
        Assert.Equal("https://example.openai.azure.com/", options.Endpoint      );
        Assert.Equal("api-key"                          , options.ApiKey        );
        Assert.Equal("gpt-4o"                           , options.DeploymentName);
        Assert.Equal("brave-key"                        , options.BraveApiKey   );
    }

    [Fact]
    public void 構成でDeploymentNameが空なら既定のデプロイ名になる()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                [$"{ChatAgentOptions.SectionName}:DeploymentName"] = ""
            })
            .Build();

        var options = new ChatAgentOptions();
        configuration.GetSection(ChatAgentOptions.SectionName).Bind(options);

        Assert.Equal(ChatAgentOptions.DefaultDeploymentName, options.DeploymentName);
    }
}
