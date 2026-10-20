namespace FCAIChat.AIAgents;

/// <summary>AI エージェントの接続設定</summary>
/// <remarks>
/// 構成セクション "ChatAgent" (appsettings.json / User Secrets / 環境変数 ChatAgent__Endpoint など) から読み込む。
/// 未設定の項目は従来どおり環境変数 AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_API_KEY / BRAVE_API_KEY から補う。
/// </remarks>
public class ChatAgentOptions
{
    public const string SectionName           = "ChatAgent";
    public const string DefaultDeploymentName = "gpt-5-mini";

    public string Endpoint       { get; set; } = string.Empty;
    public string ApiKey         { get; set; } = string.Empty;
    // 空文字が設定された場合は既定のデプロイ名を使う (C# 14 の field キーワード)
    public string DeploymentName { get; set => field = string.IsNullOrWhiteSpace(value) ? DefaultDeploymentName : value; } = DefaultDeploymentName;
    public string BraveApiKey    { get; set; } = string.Empty;
}
