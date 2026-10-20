# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core Razor Pages app (.NET 10) — a SignalR group chat in which an AI agent (Microsoft Agent Framework + Azure OpenAI, with MCP tools) replies when mentioned as `@Agent`. Solution file is `Shos.AIChatSample.slnx` with a single project in `Shos.AIChatSample/`. The C# root namespace is `FCAIChat` (not the project name). There are no test projects.

## Commands

Run from the repo root:

```bash
dotnet build Shos.AIChatSample.slnx
dotnet run --project Shos.AIChatSample                  # http://localhost:5138
dotnet run --project Shos.AIChatSample --launch-profile https   # https://localhost:7274
```

EF Core — there are **two DbContexts on the same connection string**, so always pass `--context`:

```bash
dotnet ef database update --project Shos.AIChatSample --context ApplicationDbContext
dotnet ef database update --project Shos.AIChatSample --context MessagesDbContext
dotnet ef migrations add <Name> --project Shos.AIChatSample --context MessagesDbContext --output-dir Migrations/MessagesDb
```

Identity migrations live in `Migrations/`, message migrations in `Migrations/MessagesDb/`.

## Required environment

- SQL Server LocalDB (`(localdb)\mssqllocaldb`, see `appsettings.json`) — effectively Windows-only as configured.
- Env vars read at runtime in `AIAgents/ChatAgent.cs`: `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `BRAVE_API_KEY`. Deployment name is hard-coded as `gpt-5-mini`.
- Node/`npx` on PATH: the Brave Search MCP server (`@modelcontextprotocol/server-brave-search`) is launched over stdio on the agent's first use.
- SignalR client JS is restored via LibMan (`libman.json`) into `wwwroot/lib/microsoft/signalr/`.

## Architecture

Message flow:

1. `wwwroot/js/chat.js` invokes hub method `SendMessage(user, prompt)` on `/chatHub`.
2. `Hubs/ChatHub.cs` saves the message to `MessagesDbContext` and broadcasts `ReceiveMessage(user, content, createdAtIso)` to **all** clients.
3. If the prompt contains `@{agent.Name}` (case-insensitive; `"@Agent"`), the mention is stripped and the text is sent to the agent; the reply is saved and broadcast the same way under the agent's name.
4. On `ReceiveMessage`, the client fetches `GET /Messages/RenderPartial?Message.UserName=…&Message.Content=…&Message.CreatedAt=…` (`Pages/Messages/RenderPartial`), which renders `Pages/Shared/_MessagePartial.cshtml` server-side so live messages match the initial `Index` render. A pure-JS fallback (`addMessageRow`) is used if the fetch fails.
5. Timestamps are stored/transported as UTC ISO-8601; `.utc-time` spans are converted to local time client-side. Code takes care to `DateTime.SpecifyKind(..., Utc)` when reading from the DB/query string — preserve this.

Agent layer (`AIAgents/ChatAgent.cs`):

- Abstract `ChatAgent` owns an `AIAgent` (`ChatClientAgent`) and an `AgentSession` (conversation history). Lazy init on first `GetResponseAsync`: fetch MCP tools → create agent → create/restore session → send `SystemPrompt` as a system message (new sessions only).
- `MyChatAgent` is the concrete implementation: supplies the Azure OpenAI `IChatClient` (with `UseFunctionInvocation()` for tool calls) and the MCP clients. To change model, prompts, or tools, edit/subclass here.
- Agents are kept per SignalR `ConnectionId` in a static `ConcurrentDictionary` in `ChatHub`; disposed on disconnect/`Clear`.
- Sessions are serialized after each reply into `IThreadStore` (registered as singleton `InMemoryThreadStore`, keyed by ConnectionId) and restored when an agent is recreated. Swap the `IThreadStore` registration in `Program.cs` for persistent storage.

`Clear` hub method removes the caller's agent/session but deletes **all** rows in `Messages` (shared chat log).

Auth: ASP.NET Core Identity (default UI, `ApplicationDbContext`). `_MessagePartial` puts a message on the right side when `UserName` equals the logged-in user's name.

## Conventions

Code comments are largely in Japanese; follow existing style (aligned property/argument columns, local functions, expression-bodied members).
