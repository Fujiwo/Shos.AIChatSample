# Shos.AIChatSample リポジトリ全体レビュー

- レビュー日: 2026-10-20
- 対象: `main` ブランチ (f365209) + 作業ツリー
- 範囲: `Shos.AIChatSample/` 配下の C# / Razor / JavaScript / CSS / 設定ファイル / マイグレーション (`wwwroot/lib/` のサードパーティ製ライブラリは除く)
- 手法: 静的なコードリーディングのみ (ビルド・実行はしていない)

---

## 1. 総評

Razor Pages + SignalR + Microsoft Agent Framework + MCP を組み合わせたサンプルとして、構成は小さくまとまっていて読みやすいです。一方で、**サンプルの範囲を超えて公開・共有する前に直すべき問題**がいくつかあります。

1. **認証・認可がない**: Hub もページも匿名でアクセスでき、送信者名もクライアントが自由に指定できる。誰でもなりすましと全ログ削除ができ、Azure OpenAI の課金も発生させられる。
2. **会話スレッドの保存・復元が実際には機能していない**: 復元処理は呼び出し順の誤りで何もせず、保存先のキーも接続ごとに変わる。
3. **長いメッセージの表示が壊れる**: AI の応答全体をクエリ文字列に入れて部分ビューを取得しているため、長い応答ではリクエストが失敗する。
4. **リソース管理**: `async void Dispose()`、接続ごとに起動される `npx` MCP サーバー、際限なく増えるインメモリのスレッドストア。

重大度の目安:

| 重大度 | 意味 |
|---|---|
| 🔴 High | セキュリティ上の問題、または主要機能が正しく動かない |
| 🟠 Medium | 条件次第でバグになる、または運用上の問題 |
| 🟢 Low | 保守性・可読性・細かな改善 |

---

## 2. 指摘事項一覧

| # | 重大度 | 分類 | 対象 | 概要 |
|---|---|---|---|---|
| 1 | 🔴 | セキュリティ | `Hubs/ChatHub.cs`, `Pages/Index.cshtml` | Hub・ページに認可がなく、送信者名をクライアントが指定する |
| 2 | 🔴 | セキュリティ | `Hubs/ChatHub.cs:22-31` | 誰でも `Clear` で全メッセージを削除できる |
| 3 | 🔴 | バグ | `Hubs/ChatHub.cs:78-97`, `AIAgents/ChatAgent.cs:45-52` | スレッドの復元が常に失敗する (Agent が null の段階で Deserialize している) |
| 4 | 🔴 | バグ | `wwwroot/js/chat.js:47` | 本文をクエリ文字列で送るため、長い応答で URL 長の上限を超える |
| 5 | 🟠 | バグ | `AIAgents/ChatAgent.cs:54-59` | `async void Dispose()` で例外がプロセスを落とす可能性があり、破棄の完了も待てない |
| 6 | 🟠 | バグ | `wwwroot/js/chat.js:89-94` | `toLocalTimeStringAll()` が変換済みの時刻を再変換し、ロケールによっては `Invalid Date` になる |
| 7 | 🟠 | バグ | `wwwroot/js/chat.js:35-40` | `Clear` の完了を待たずに `location.reload()` しており、競合が起きる。他のクライアントにも通知されない |
| 8 | 🟠 | バグ | `wwwroot/js/chat.js:45-72` | 部分ビューの fetch が並行して走るため、メッセージの表示順が入れ替わることがある |
| 9 | 🟠 | バグ | `Hubs/ChatHub.cs:69-75` | メンション判定は大文字小文字を区別しないのに、除去 (`Replace`) は区別する。`@Agentic` などにも反応する |
| 10 | 🟠 | 性能/資源 | `AIAgents/ChatAgent.cs:199-241` | 接続ごとに `npx` の MCP サーバープロセスと `AzureOpenAIClient` を生成している |
| 11 | 🟠 | 性能/資源 | `Services/InMemoryThreadStore.cs`, `Hubs/ChatHub.cs:108-117` | スレッドストアが削除されずに増え続ける (メモリリーク) |
| 12 | 🟠 | 性能/コスト | `AIAgents/ChatAgent.cs:97-118` | 初回にシステムメッセージを `RunAsync` しており、無駄な LLM 呼び出しが 1 回発生する |
| 13 | 🟠 | 設計 | `Pages/Index.cshtml.cs:15-29` | コンストラクターで `.Result` による同期 DB アクセスをしている。件数の上限もない |
| 14 | 🟠 | 設計 | `Hubs/ChatHub.cs:33-53` | LLM 応答を Hub メソッド内で同期的に待つため、その接続の後続の呼び出しがブロックされる。エラー時の通知もない |
| 15 | 🟠 | 運用 | `AIAgents/ChatAgent.cs:134-140` 他 | 例外を握りつぶして `Debug.WriteLine` に出すだけで、`ILogger` を使っていない |
| 16 | 🟢 | 設計 | `Data/MessagesDbContext.cs` | UTC の扱いが呼び出し側任せ (ValueConverter か `DateTimeOffset` にすべき) |
| 17 | 🟢 | 設計 | `AIAgents/ChatAgent.cs` | 設定値を環境変数から直接読んでいる。`IConfiguration`、User Secrets、DI を使っていない |
| 18 | 🟢 | 保守性 | `AIAgents/ChatAgent.cs` | 未使用のコード (`RestoreThread` / `pendingThreadRestore` / `wasThreadRestored`) と誤ったコメントがある |
| 19 | 🟢 | 保守性 | `Shared/_Layout.cshtml`, `libman.json` | SignalR を CDN 9.0.6 から読み込む一方で、LibMan が `@latest` を復元している (どちらか一方にすべき) |
| 20 | 🟢 | 保守性 | `Migrations/**` | 名前空間が `FCAIChat2` になっている。マイグレーション名が説明的でない |
| 21 | 🟢 | 保守性 | `wwwroot/css/site.css` | Shift_JIS で保存されていて文字化けする。未使用のクラスもある |
| 22 | 🟢 | UX | `wwwroot/js/chat.js`, `Pages/Index.cshtml` | Enter キーで送信できない。送信後に入力欄がクリアされない。未ログイン時は何も起きない |
| 23 | 🟢 | UX/設計 | `Pages/Shared/_MessagePartial.cshtml` | 分岐ごとにマークアップが重複している。`<br>` を並べて余白を作っている |
| 24 | 🟢 | リポジトリ | ルート | 改行コードの差分で全ファイルが変更扱いになっている。`.gitattributes`、README、テストがない |

---

## 3. 詳細

### #1 🔴 Hub・ページに認可がなく、送信者名をクライアントが指定する

`ChatHub` にも `IndexModel` にも `[Authorize]` がありません。さらに `SendMessage(string user, string prompt)` の `user` は、`Index.cshtml` の hidden の `<input id="userInput">` からクライアントが送った値をそのまま使っています。

```csharp
// Hubs/ChatHub.cs:33
public async Task SendMessage(string user, string prompt)
```

- DevTools や独自の SignalR クライアントから `connection.invoke("SendMessage", "他人の名前", "...")` を呼べば、**任意のユーザーになりすませる**。`"Agent"` を名乗れば AI になりすますこともできる。
- 未ログインでも `/chatHub` に接続して `@Agent` を送れるため、**第三者が Azure OpenAI / Brave Search の課金を発生させられる**。

**推奨**

```csharp
[Authorize]
public class ChatHub : Hub
{
    public async Task SendMessage(string prompt)
    {
        var user = Context.User?.Identity?.Name
                   ?? throw new HubException("Unauthorized");
        ...
```

- `Index` ページにも `[Authorize]` を付ける (または `AddRazorPages(o => o.Conventions.AuthorizePage("/Index"))`)。
- `/Messages/RenderPartial` にも同様に認可を付ける (#4 の対応で不要になるなら削除する)。
- 必要に応じて、メッセージ長の上限とレート制限 (`AddRateLimiter`) を入れる。

### #2 🔴 誰でも `Clear` で全メッセージを削除できる

`Clear` は呼び出し元のエージェントを破棄したあと、**共有チャットログである `Messages` の全行を削除**します。#1 のとおり認可がないため、匿名のユーザーでも全員のログを消せます。

また、`dbSet.RemoveRange(dbSet)` は全行をメモリに読み込んでから 1 行ずつ DELETE します。

**推奨**

- 管理者ロールに限定する (`[Authorize(Roles = "Admin")]`)、または「自分の表示だけクリアする」仕様に変える。
- 削除は `await dbContext.Messages.ExecuteDeleteAsync();` で一括実行する。
- 削除後に `Clients.All.SendAsync("Cleared")` で他のクライアントへ通知する (#7 も参照)。

### #3 🔴 スレッドの復元が常に失敗する

```csharp
// Hubs/ChatHub.cs:83-88
var agent = new MyChatAgent();
var serializedSession = await threadStore.GetThreadAsync(connectionId);
if (serializedSession.HasValue) {
    await agent.DeserializeSessionAsync(serializedSession);
```

```csharp
// AIAgents/ChatAgent.cs:45-52
public async Task<bool> DeserializeSessionAsync(JsonElement? jsonElement)
{
    if (Agent is not null && jsonElement.HasValue) {   // ← Agent は Start() まで null
```

`Agent` は `Start()` (= 初回の `GetResponseAsync`) で初めて生成されます。そのため、生成直後に `DeserializeSessionAsync` を呼んでも**必ず `false` が返り、何も起きません**。戻り値も確認されていません。

さらに根本的な問題として、**保存キーが `Context.ConnectionId` である**ことが挙げられます。SignalR の ConnectionId はページの再読み込みや再接続のたびに新しくなるので、保存したスレッドを次の接続で取り出せることは実質的にありません。加えて、`OnDisconnectedAsync` ではエージェントを破棄するのにストアの中身は残すため、#11 のリークにつながります。

**推奨**

- キーを**ユーザー ID** (`Context.UserIdentifier`) にするか、グループチャットであればチャットルーム ID にする。
- 復元は `Start()` の中で `Agent` を生成したあとに行う。既にある `RestoreThread()` + `pendingThreadRestore` の仕組みがちょうどこの目的に合うので、`ChatHub` 側はコメントアウトされている `agent.RestoreThread(...)` に戻し、`DeserializeSessionAsync` を削除して一本化する。
- 復元したときにシステムメッセージを再送しないための `wasThreadRestored` も、この経路で初めて意味を持つ。

### #4 🔴 本文をクエリ文字列で送るため、長い応答で URL 長の上限を超える

```js
// wwwroot/js/chat.js:47
const url = `/Messages/RenderPartial?Message.UserName=...&Message.Content=${encodeURIComponent(message)}&...`;
```

LLM の応答 (特に Web 検索の結果を含むもの) は数 KB を超えることがよくあります。日本語は `encodeURIComponent` で 1 文字あたり 9 バイトに膨らむため、およそ 900 文字で Kestrel の `MaxRequestLineSize` (既定 8 KB) を超えます。超えた場合は 414/400 になり、フォールバックの `addMessageRow` で**レイアウトの異なる素のテキスト**として表示されます。

ほかにも次の問題があります。

- 全クライアントが、受信したメッセージ 1 件ごとにサーバーへ HTTP リクエストを送る (N クライアント × M メッセージ)。
- 会話の本文がアクセスログやプロキシのログに URL として残る (プライバシー上の懸念)。

**推奨 (どれか 1 つ)**

- a) クライアント側でテンプレートからレンダリングする。`textContent` を使えば XSS は起きない。
- b) サーバー側でメッセージの ID を送り、クライアントは `/Messages/RenderPartial?id=123` で DB から取得する。
- c) 最低限の対応として、POST + アンチフォージェリトークンに変える。

### #5 🟠 `async void Dispose()`

```csharp
// AIAgents/ChatAgent.cs:54
public async void Dispose()
{
    foreach (var mcpClient in mcpClients)
        await mcpClient.DisposeAsync();
}
```

- `async void` の中で発生した例外は呼び出し元で捕捉できず、`SynchronizationContext` のない ASP.NET Core ではプロセスがクラッシュする可能性がある。
- 呼び出し元は破棄の完了を待てない。そのため、`npx` の子プロセスが終了する前に次の処理へ進んでしまう。

**推奨**: `IAsyncDisposable` を実装し、`ChatHub` 側では `await agent.DisposeAsync()` を呼ぶ。

```csharp
public abstract class ChatAgent : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        foreach (var mcpClient in mcpClients)
            await mcpClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }
```

### #6 🟠 `toLocalTimeStringAll()` が変換済みの時刻を再変換する

```js
// wwwroot/js/chat.js:89
function toLocalTimeStringAll() {
    document.querySelectorAll('.utc-time').forEach(element => {
        element.classList.remove('hidden');
        element.textContent = toLocalTimeString(element.textContent)
    });
}
```

新しいメッセージを受信するたびに、**すでにローカル時刻へ変換済みの要素も含めて**、すべての要素をもう一度 `new Date(...)` に通しています。

- `ja-JP` の `2026/10/20 12:34:56` は Chrome ではたまたまパースできるので、見かけ上は問題が出ない。
- `en-GB` の `20/10/2026, 12:34:56` などは **`Invalid Date`** になる。Safari や Firefox では `ja-JP` の形式でも失敗することがある。

**推奨**: 未変換の要素だけを対象にする。元の値は属性に保持しておく。

```html
<time class="utc-time" datetime="@Model.CreatedAt.ToString("o")"></time>
```

```js
document.querySelectorAll('.utc-time:not([data-converted])').forEach(el => {
    el.textContent = new Date(el.getAttribute('datetime')).toLocaleString();
    el.dataset.converted = '1';
});
```

`setToDiv` の `span.innerHTML = ...` も `textContent` に変えてください。現状は日付文字列しか入らないので実害はありませんが、`innerHTML` を使う必要がありません。

### #7 🟠 `Clear` 後の reload に競合があり、他のクライアントにも通知されない

```js
connection.invoke("Clear").catch(...);
location.reload();   // invoke の完了を待っていない
```

- ページの再読み込みで SignalR の接続が切れるため、`Clear` の処理より先に `Index` の GET が走って古いメッセージが表示されたり、切断の仕方によっては呼び出し自体が完了しなかったりする。
- 他のクライアントには何も通知されないので、古いログが表示されたままになる。

**推奨**: `await connection.invoke("Clear")` のあとで画面をクリアする。サーバー側からは `Clients.All.SendAsync("Cleared")` で全員に通知する。

### #8 🟠 メッセージの表示順が入れ替わることがある

`ReceiveMessage` のハンドラーが `addMessagePartial` (非同期の fetch) を await せずに呼んでいます。そのため、短い間隔で届いたメッセージは **fetch が完了した順に**追加されます。複数のユーザーが同時に発言した場合などに、表示順が入れ替わります。

**推奨**: #4 でクライアント側のレンダリングに変える (同期的に追加される) か、Promise チェーンで順番に処理する。

### #9 🟠 メンション判定と除去の不整合

```csharp
return prompt.Contains(chatAgentReference, StringComparison.OrdinalIgnoreCase)
       ? (true, prompt.Replace(chatAgentReference, ""))   // ← 大文字小文字を区別する
```

- `@agent こんにちは` はメンションとして判定されますが、`@agent` が取り除かれないまま LLM に渡されます。
- `Contains` は部分一致なので、`@Agentic` や `foo@Agent.com` などにも反応します。

**推奨**: `prompt.Replace(chatAgentReference, "", StringComparison.OrdinalIgnoreCase)` に変える。判定は正規表現 `(?<!\w)@Agent\b` などで単語境界を考慮する。

また、LLM には**発言者の名前が渡されていません**。グループチャットなので、`"{user}: {prompt}"` のように話者を含めないと、エージェントは誰が話しているのか区別できません。

### #10 🟠 接続ごとに `npx` の MCP サーバーと `AzureOpenAIClient` を生成している

`MyChatAgent` は接続ごとに作られ、初めて `@Agent` が呼ばれたときに次のものを生成します。

- `npx -y @modelcontextprotocol/server-brave-search` の **Node プロセス 1 つ** (初回は npm から取得するため数秒〜数十秒かかる)
- `AzureOpenAIClient` / `IChatClient`

同時に接続している人数の分だけ Node プロセスが立ち上がり、#5 の理由で確実に終了する保証もありません。

**推奨**

- `IChatClient` と MCP のツール一覧を **DI のシングルトン**にし、エージェントごとに変わるのは `AgentSession` だけにする。
- `AIAgent` 自体もステートレスなので共有できる (状態は `AgentSession` が持つ)。
- MCP クライアントの破棄は `IHostedService` か、アプリ終了時の DI コンテナに任せる。
- あわせて、`npx -y` で最新版を自動インストールするのはサプライチェーン上のリスクがあるため、バージョンを固定する (`@modelcontextprotocol/server-brave-search@x.y.z`)。

### #11 🟠 スレッドストアが削除されずに増え続ける

`OnDisconnectedAsync` は「再接続に備えて」スレッドを残しますが、#3 のとおり ConnectionId は再利用されないため、**誰も読まないデータが増え続けます**。コメントにある cleanup の仕組みも実装されていません。

**推奨**: キーをユーザー ID に変える (#3)。インメモリのまま使うなら `IMemoryCache` で有効期限 (スライディング期限) を付ける。

### #12 🟠 初回に無駄な LLM 呼び出しが 1 回発生する

```csharp
ChatMessage systemMessage = new(ChatRole.System, SystemPrompt);
await Agent.RunAsync(systemMessage, Session);
```

システムメッセージだけで `RunAsync` しているため、**応答を捨てるための LLM 呼び出しが 1 回**余分に発生します (レイテンシーと課金が増える)。しかも `Instructions` と `SystemPrompt` はどちらも `"あなたはAIエージェントです"` で、内容が重複しています。

**推奨**: `ChatClientAgent` の `instructions` に一本化し、`SystemPrompt` / `SendSystemMessageAsync` は削除する。指示文には「グループチャットである」「発言は `名前: 本文` の形式で届く」「日本語で簡潔に答える」などの文脈を書くと、応答の品質が上がります。

### #13 🟠 `IndexModel` のコンストラクターで同期 DB アクセスをしている

```csharp
Messages = dbContext.Messages.OrderBy(...).ToListAsync().Result ...
```

- コンストラクターでの I/O や `.Result` による sync-over-async は、スレッドプールの枯渇を招く典型的なアンチパターンです。
- 全件を読み込むので、ログが増えるほどページの表示が重くなります。
- `.Select(new Message {...})` でコピーしているのは `SpecifyKind` のためだけです (#16 で不要になります)。
- `_logger` が使われていません。

**推奨**

```csharp
public async Task OnGetAsync()
    => Messages = await _dbContext.Messages
                                  .AsNoTracking()
                                  .OrderByDescending(m => m.CreatedAt).Take(200)
                                  .OrderBy(m => m.CreatedAt)
                                  .ToListAsync();
```

### #14 🟠 LLM 応答を Hub メソッド内で同期的に待っている

`SendMessage` は LLM と MCP ツールの呼び出し (数秒〜数十秒) が終わるまで返りません。SignalR の既定値 `MaximumParallelInvocationsPerClient = 1` により、**その間、同じクライアントからの次の `SendMessage` や `Clear` は待たされます**。

また、`Start()` の中で環境変数の未設定や `npx` の不在による例外が発生すると、ユーザーのメッセージは配信済みなのに、AI 側からは何の応答も表示されません (クライアントのコンソールに汎用的なエラーが出るだけです)。

**推奨**

- 「Agent が入力中…」のようなインジケーターを `Clients.All` に送る。
- エージェントの処理はバックグラウンドのキュー (`Channel<T>` + `BackgroundService`) に渡し、完了したら `IHubContext<ChatHub>` で配信する。
- 失敗した場合は、その旨をメッセージとして配信する。

### #15 🟠 例外を握りつぶし、`Debug.WriteLine` にしか出していない

`RunAgentAsync` は例外を捕捉して `"……"` を返します。`Debug.WriteLine` は Release ビルドやサーバー環境では出力されないため、**本番で障害が起きても原因を追えません**。

**推奨**: `ILogger<T>` を DI で注入し、`logger.LogError(ex, "...")` で記録する。ユーザーには「エラーが発生しました」のような分かるメッセージを返す。

### #16 🟢 UTC の扱いが呼び出し側任せになっている

`CreatedAt` は `datetime2` に Kind なしで保存されるため、読み出すたびに `SpecifyKind(..., Utc)` を呼ぶ必要があります (CLAUDE.md の Gotchas にも記載あり)。呼び忘れるとそのままバグになります。

**推奨**: `OnModelCreating` で ValueConverter を設定し、読み出し時に必ず UTC になるようにする。

```csharp
modelBuilder.Entity<Message>()
            .Property(m => m.CreatedAt)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
```

または `DateTimeOffset` 型 (`datetimeoffset`) に移行する。これで `IndexModel` のコピー処理と `RenderPartialModel` の補正が不要になります。

`UserName` / `Content` は `nvarchar(max)` なので、`UserName` には `HasMaxLength(256)` を付けることも検討してください。

### #17 🟢 設定値を環境変数から直接読んでいる

csproj には `UserSecretsId` がありますが、使われていません。`Environment.GetEnvironmentVariable` を直接呼んでいるため、`appsettings.json`、User Secrets、Azure App Configuration / Key Vault などに差し替えられません。デプロイ名 `gpt-5-mini` もハードコードされています。

**推奨**: `IOptions<AzureOpenAIOptions>` にバインドする。`IConfiguration` は環境変数も読み込むので、`AZURE_OPENAI_ENDPOINT` などの既存の名前はそのまま使えます。可能であれば、API キーの代わりに `DefaultAzureCredential` (Entra ID 認証) を使うことも検討してください。

### #18 🟢 未使用のコードと誤ったコメント

- `RestoreThread` / `pendingThreadRestore` / `wasThreadRestored` は、どこからも呼ばれていません (#3 の修正で使うなら残す)。
- `GetMcpToolsAsync` の既定実装にある `await Task.Delay(0);` は不要です。`Task.FromResult(...)` を返す形に変えてください。
- `GetEndPoint()` と `GetKey()` のコメントにある**例が逆になっています** (エンドポイントの例が 64 桁の 16 進数、キーの例が URL)。また、どちらも「ここではハードコードする」と書かれていますが、実際にはハードコードしていません。
- `ChatHub.cs:6` に、コメントアウトされた `using` が残っています。
- `MessagesDbContext.cs` に、コメントアウトされた `OnConfiguring` (接続文字列入り) が残っています。
- `responce` は `response` のスペルミスです。
- `ChatHub` の中にある `EntityExtensions` は、#2 で `ExecuteDeleteAsync` に置き換えれば不要になります。

### #19 🟢 SignalR クライアントの読み込み元が二重になっている

- `_Layout.cshtml` は cdnjs の `microsoft-signalr/9.0.6` を SRI 付きで読み込んでいます。
- `libman.json` は `@microsoft/signalr@latest` を `wwwroot/lib/microsoft/signalr/` に復元しますが、**どこからも参照されていません**。また、`@latest` のためビルドの再現性がありません。
- CLAUDE.md には「LibMan で復元する」と書かれていて、実際の動作と食い違っています。

**推奨**: どちらか一方に統一する。ローカルにするなら、バージョンを固定したうえで `<script src="~/lib/microsoft/signalr/dist/browser/signalr.min.js">` を参照する。

あわせて、空の `<script type="importmap"></script>` は削除してください。`<html lang="en">` は、UI が日本語なら `ja` が適切です。

### #20 🟢 マイグレーションの名前空間と名前

- マイグレーションの名前空間が `FCAIChat2.Migrations*` で、ルート名前空間の `FCAIChat` と一致していません (プロジェクト名を変えた名残と思われます)。動作に影響はありませんが、混乱のもとになります。
- `InitialCreate2` / `InitialCreate3` は内容を表していません (それぞれ `AddMessages` / `AddMessageCreatedAt` のような名前が適切です)。
- スナップショットの `ProductVersion` が `9.0.10` のままですが、これは次回のマイグレーション作成時に更新されるので対応は不要です。

### #21 🟢 `site.css` が Shift_JIS で保存されている

`wwwroot/css/site.css` が Shift_JIS (CP932) で保存されているため、UTF-8 を前提とするツール (Git の diff、Web 上のビューア、Linux 環境など) では日本語のコメントが文字化けします。ほかのファイルは UTF-8 (BOM 付き) です。

**推奨**: UTF-8 に変換する。あわせて、未使用の `.talk2` / `.talk3` を削除する。

### #22 🟢 UX の細かな点

- Enter キーで送信できない (`keydown` で `Enter` を拾えば対応できる)。
- 送信後に `messageInput` がクリアされない。
- 未ログインの状態だと `userInput` が空のため、送信ボタンを押しても**何も起きず、何も表示されない** (#1 で認可を付ければ解消する)。
- 新しいメッセージが届いても自動でスクロールしない。
- `console.log` で送受信したメッセージの本文を出力している (本番では削除する)。

### #23 🟢 `_MessagePartial.cshtml` の重複

`talk_right` と `talk_left` の分岐で、中身のマークアップがまったく同じです。

```cshtml
@{ var side = Model.UserName == User?.Identity?.Name ? "talk_right" : "talk_left"; }
<div class="@side">
    <p>
        <span>@Model.UserName</span>
        <span class="content">@Model.Content</span>
        <time class="utc-time" datetime="@Model.CreatedAt.ToString("o")"></time>
    </p>
</div>
```

- 改行の表示は `Html.Raw(Html.Encode(...).Replace("&#xA;", "<br>"))` ではなく、CSS の `white-space: pre-wrap` で行う方が安全で簡潔です (現状のコードも、エンコードしてから置換しているので XSS は起きません)。
- 末尾の `<br><br><br><br>` で余白を作るのではなく、CSS の `margin` を使ってください。

### #24 🟢 リポジトリ全体

- **作業ツリーの「変更あり」はほぼすべて改行コードの差分です** (`git diff --ignore-cr-at-eol` で実際に差分があるのは `CLAUDE.md` だけ)。Windows と WSL を行き来しているためと思われます。`.gitattributes` (例: `* text=auto`、`*.cs text eol=crlf`) を追加し、`git add --renormalize .` で正規化することをおすすめします。
- README がありません。セットアップの手順 (LocalDB、環境変数、Node、マイグレーション) は CLAUDE.md にしか書かれていません。
- テストプロジェクトがありません。最低限、`GetPromptForAgent` (#9) のような純粋なロジックを static メソッドに切り出し、単体テストを書ける形にすることをおすすめします。
- `wwwroot/lib/` のサードパーティ製ライブラリがコミットされています。LibMan で管理するなら、`.gitignore` で除外して復元する運用にする方法もあります。

---

## 4. 対応の優先順位 (提案)

1. **#1, #2** 認可を付け、送信者をサーバー側で決める (公開する前に必須)
2. **#4, #6, #8** 表示まわりのバグ (部分ビューの取得方式を見直せば、まとめて解消できる)
3. **#5, #10, #11, #12** リソースとコスト (DI のシングルトン化と `IAsyncDisposable`)
4. **#3** スレッド永続化の設計を見直す (キーをユーザー ID にし、`Start()` 内で復元する)
5. **#13 – #17** 非同期化、ログ、設定、UTC の ValueConverter
6. 残りの Low 項目 (#18 – #24)

## 5. 良い点

- `ChatAgent` を抽象クラスにして `MyChatAgent` で具体化しており、モデルや MCP ツールを差し替えやすい構造になっている。
- UTC の ISO-8601 で送受信し、クライアント側でローカル時刻に変換するという方針が一貫している。
- `_MessagePartial` で `Html.Encode` をしてから改行を置換しており、XSS に配慮している。
- CDN のスクリプトに SRI (`integrity`) を付けている。
- DbContext を Identity 用とメッセージ用に分け、マイグレーションのフォルダーも分けている。

---

## 6. 対応状況 (2026-10-20 追記)

`dotnet build` が警告 0 件・エラー 0 件で通ることを確認済み。実行時の動作確認 (LocalDB、Azure OpenAI、ブラウザ) はしていない。

| # | 状況 | 対応内容 |
|---|---|---|
| 1 | ✅ | `ChatHub`・`Index`・`Messages/RenderPartial` に `[Authorize]` を付けた。送信者名は `Context.User` から取得し、`SendMessage(prompt)` の引数から `user` を削除。最大文字数 (4000) のチェックを追加 |
| 2 | 🔶 一部 | 認証済みユーザーに限定し、`ExecuteDeleteAsync` で一括削除、`Cleared` を全クライアントへ通知するようにした。**管理者ロールへの限定は未対応** (ロールの仕組みがないため) |
| 3 | ✅ | スレッドの保存キーを `Context.UserIdentifier` に変更。復元は `ChatAgent.GetResponseAsync` の中で、Agent を生成したあとに行う |
| 4 | ✅ | `ReceiveMessage` で ID を配信し、`/Messages/RenderPartial?id=` で DB から読み込む方式に変更 |
| 5 | ✅ | `ChatAgent` を `IAsyncDisposable` に変更 (DI コンテナがアプリ終了時に破棄する) |
| 6 | ✅ | `<time datetime>` と `data-converted` で、未変換の要素だけを変換するようにした |
| 7 | ✅ | `Clear` の完了を待ち、サーバーからの `Cleared` 通知で全クライアントの画面をクリアする |
| 8 | ✅ | Promise のキューで受信順に表示する |
| 9 | ✅ | 正規表現でメンションを判定・除去するようにした (大文字小文字を区別しない、英単語の一部には反応しない)。LLM には `名前: 本文` の形式で渡す |
| 10 | ✅ | `MyChatAgent` を DI のシングルトンにし、AIAgent・IChatClient・MCP クライアントをアプリ全体で 1 つにした。MCP サーバーのバージョンを `0.6.2` に固定 |
| 11 | ✅ | キーをユーザー ID にしたので、エントリ数はユーザー数が上限になる |
| 12 | ✅ | システムメッセージの `RunAsync` を削除し、`Instructions` に一本化 (グループチャットの文脈を記述) |
| 13 | ✅ | `OnGetAsync` で非同期に読み込む。`AsNoTracking`、最新 200 件に制限 |
| 14 | ⏳ 未対応 | Hub の中で LLM の応答を待つ構造はそのまま。失敗時は「エラーが発生したため応答できませんでした」というメッセージを配信する |
| 15 | ✅ | `ILogger` を使うようにした |
| 16 | ✅ | `MessagesDbContext.OnModelCreating` で、UTC にする ValueConverter を設定 (スキーマの変更はなし) |
| 17 | ✅ | `ChatAgentOptions` (構成セクション `ChatAgent`) を追加。未設定の項目は従来の環境変数名から補う |
| 18 | ✅ | 未使用のコード、コメントアウトされたコード、誤ったコメントを削除 |
| 19 | ✅ | `libman.json` を削除し、CDN (SRI 付き) に一本化。空の importmap を削除し、`lang="ja"` に変更 |
| 20 | — | マイグレーションはレビューのあとに作り直されている (`InitialCreateApplication` / `InitialCreateMessages`) ため対応不要 |
| 21 | ✅ | UTF-8 に変換し、未使用の `.talk2` / `.talk3` / `.hidden` を削除 |
| 22 | ✅ | Enter キーでの送信 (IME の変換中は除く)、送信後の入力欄クリア、自動スクロール、自動再接続に対応。本文の `console.log` を削除 |
| 23 | ✅ | 分岐を 1 つにまとめた。改行は `white-space: pre-wrap` で表示し、`<br>` による余白をやめて CSS の margin に変更 |
| 24 | ⏳ 未対応 | `.gitattributes`、README、テストプロジェクトは未作成 (リポジトリの運用方針が関わるため) |
