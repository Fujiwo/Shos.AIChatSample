using FCAIChat.Services;
using System.Text.Json;

namespace FCAIChat.Tests.Services;

/// <summary><see cref="InMemoryThreadStore"/> のテスト</summary>
public class InMemoryThreadStoreTests
{
    readonly InMemoryThreadStore threadStore = new();

    static JsonElement CreateThread(string text) => JsonSerializer.SerializeToElement(new { text });

    [Fact]
    public async Task 保存していなければnullを返す()
        => Assert.Null(await threadStore.GetThreadAsync("user-1"));

    [Fact]
    public async Task 保存した会話を取得できる()
    {
        await threadStore.SaveThreadAsync("user-1", CreateThread("こんにちは"));

        var thread = await threadStore.GetThreadAsync("user-1");

        Assert.NotNull(thread);
        Assert.Equal("こんにちは", thread.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task 元のJsonDocumentを破棄しても取得できる()
    {
        // JsonElement は元の JsonDocument に依存するので、文字列で保持していることを確認する
        using (var document = JsonDocument.Parse("""{"text":"こんにちは"}"""))
            await threadStore.SaveThreadAsync("user-1", document.RootElement);

        var thread = await threadStore.GetThreadAsync("user-1");

        Assert.Equal("こんにちは", thread!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task 同じユーザーで保存すると上書きする()
    {
        await threadStore.SaveThreadAsync("user-1", CreateThread("1 回目"));
        await threadStore.SaveThreadAsync("user-1", CreateThread("2 回目"));

        var thread = await threadStore.GetThreadAsync("user-1");

        Assert.Equal("2 回目", thread!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task ユーザーごとに別々に保存する()
    {
        await threadStore.SaveThreadAsync("user-1", CreateThread("alice"));
        await threadStore.SaveThreadAsync("user-2", CreateThread("bob"  ));

        Assert.Equal("alice", (await threadStore.GetThreadAsync("user-1"))!.Value.GetProperty("text").GetString());
        Assert.Equal("bob"  , (await threadStore.GetThreadAsync("user-2"))!.Value.GetProperty("text").GetString());
    }

    [Fact]
    public async Task 削除するとnullを返す_他のユーザーの会話は残る()
    {
        await threadStore.SaveThreadAsync("user-1", CreateThread("alice"));
        await threadStore.SaveThreadAsync("user-2", CreateThread("bob"  ));

        await threadStore.DeleteThreadAsync("user-1");

        Assert.Null   (await threadStore.GetThreadAsync("user-1"));
        Assert.NotNull(await threadStore.GetThreadAsync("user-2"));
    }

    [Fact]
    public async Task 保存していない会話を削除しても失敗しない()
    {
        await threadStore.DeleteThreadAsync("user-1");

        Assert.Null(await threadStore.GetThreadAsync("user-1"));
    }

    [Fact]
    public async Task 同時に保存しても失敗しない()
    {
        await Task.WhenAll(Enumerable.Range(0, 100).Select(index => Task.Run(() => threadStore.SaveThreadAsync($"user-{index % 10}", CreateThread($"{index}")))));

        for (var index = 0; index < 10; index++)
            Assert.NotNull(await threadStore.GetThreadAsync($"user-{index}"));
    }
}
