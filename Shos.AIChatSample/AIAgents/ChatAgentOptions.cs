namespace FCAIChat.AIAgents;

/// <summary>AI エージェントの接続設定</summary>
/// <remarks>
/// 構成セクション "ChatAgent" (appsettings.json / User Secrets / 環境変数 ChatAgent__Endpoint など) から読み込む。
/// 未設定の項目は従来どおり環境変数 AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_API_KEY / BRAVE_API_KEY から補う (Program.cs を参照)。
/// API キーなどの秘密情報は appsettings.json には書かず、User Secrets や環境変数で設定すること。
/// </remarks>
public class ChatAgentOptions
{
    /// <summary>構成セクション名</summary>
    public const string SectionName           = "ChatAgent";
    /// <summary>既定で使用する Azure OpenAI のデプロイ名</summary>
    public const string DefaultDeploymentName = "gpt-5-mini";

    /// <summary>Azure OpenAI のエンドポイント (例: https://your-resource-name.openai.azure.com/)</summary>
    public string Endpoint       { get; set; } = string.Empty;
    /// <summary>Azure OpenAI の API キー</summary>
    public string ApiKey         { get; set; } = string.Empty;
    /// <summary>Azure OpenAI のデプロイ名</summary>
    /// <remarks>空文字が設定された場合は既定のデプロイ名を使う (C# 14 の field キーワードで自動実装のバッキングフィールドを参照)</remarks>
    public string DeploymentName { get; set => field = string.IsNullOrWhiteSpace(value) ? DefaultDeploymentName : value; } = DefaultDeploymentName;
    /// <summary>Brave Search の API キー (Web 検索ツール用)</summary>
    public string BraveApiKey    { get; set; } = string.Empty;
}
