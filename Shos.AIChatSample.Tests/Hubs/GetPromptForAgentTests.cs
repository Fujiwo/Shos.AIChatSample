using FCAIChat.Hubs;

namespace FCAIChat.Tests.Hubs;

/// <summary><see cref="ChatHub.GetPromptForAgent"/> (メンションの判定と除去) のテスト</summary>
public class GetPromptForAgentTests
{
    [Theory]
    [InlineData("@Agent 今日の天気は?"   , "今日の天気は?"  )]
    [InlineData("@agent 今日の天気は?"   , "今日の天気は?"  )] // 大文字小文字は区別しない
    [InlineData("@AGENT 今日の天気は?"   , "今日の天気は?"  )]
    [InlineData("今日の天気は? @Agent"   , "今日の天気は?"  )] // 末尾のメンション
    [InlineData("ねえ @Agent 教えて"     , "ねえ  教えて"   )] // 途中のメンション (前後の空白は残る)
    [InlineData("こんにちは@Agent"       , "こんにちは"     )] // 日本語に続けて書いた場合
    [InlineData("@Agentさん 元気?"       , "さん 元気?"     )] // 日本語を続けて書いた場合
    [InlineData("@Agent、教えて"         , "、教えて"       )]
    [InlineData("(@Agent) 教えて"        , "() 教えて"      )]
    [InlineData("@Agent @agent 教えて"   , "教えて"         )] // 複数のメンションはすべて取り除く
    [InlineData("@Agent"                 , ""               )] // メンションだけ
    [InlineData("  @Agent  "             , ""               )]
    [InlineData("@Agent\n1 行目\n2 行目" , "1 行目\n2 行目" )] // 改行を含む
    public void メンションがあればエージェント宛てになり_メンションが取り除かれる(string prompt, string expected)
    {
        var (isForAgent, promptForAgent) = ChatHub.GetPromptForAgent("Agent", prompt);

        Assert.True (isForAgent);
        Assert.Equal(expected, promptForAgent);
    }

    [Theory]
    [InlineData("こんにちは"          )] // メンションなし
    [InlineData(""                    )]
    [InlineData("Agent 教えて"        )] // @ がない
    [InlineData("@Agentic 教えて"     )] // 英単語の一部
    [InlineData("@Agent2 教えて"      )] // 直後が数字
    [InlineData("@Agent_x 教えて"     )] // 直後が _
    [InlineData("foo@Agent.com"       )] // メールアドレスの一部
    [InlineData("mail@agent"          )]
    [InlineData("@@Agent"             )] // 直前が @
    [InlineData(".@Agent"             )] // 直前が .
    [InlineData("@ Agent 教えて"      )] // @ の後に空白
    [InlineData("@Agen 教えて"        )]
    public void メンションがなければエージェント宛てにならない(string prompt)
    {
        var (isForAgent, promptForAgent) = ChatHub.GetPromptForAgent("Agent", prompt);

        Assert.False(isForAgent);
        Assert.Equal(string.Empty, promptForAgent);
    }

    [Fact]
    public void エージェント名に含まれる正規表現の特殊文字はエスケープされる()
    {
        Assert.Equal((true , "教えて"     ), ChatHub.GetPromptForAgent("A.I", "@A.I 教えて"));
        // "." が任意の 1 文字として扱われていないこと
        Assert.Equal((false, string.Empty), ChatHub.GetPromptForAgent("A.I", "@AxI 教えて"));
    }

    [Fact]
    public void エージェント名を変えるとその名前のメンションだけに反応する()
    {
        Assert.Equal((true , "教えて"     ), ChatHub.GetPromptForAgent("Bot", "@bot 教えて"  ));
        Assert.Equal((false, string.Empty), ChatHub.GetPromptForAgent("Bot", "@Agent 教えて"));
    }
}
