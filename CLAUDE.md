# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core Razor Pages app (.NET 10) — a SignalR group chat in which an AI agent (Microsoft Agent Framework + Azure OpenAI, with MCP tools) replies when mentioned as `@Agent`. The C# root namespace is `FCAIChat` (not the project name); EF migrations are generated into `Shos.AIChatSample.Migrations*`. There are no test projects.

## Commands

EF Core — there are **two DbContexts on the same connection string**, so always pass `--context`:

```bash
dotnet ef database update --project Shos.AIChatSample --context ApplicationDbContext
dotnet ef database update --project Shos.AIChatSample --context MessagesDbContext
dotnet ef migrations add <Name> --project Shos.AIChatSample --context MessagesDbContext --output-dir Migrations/MessagesDb
```

Identity migrations live in `Migrations/`, message migrations in `Migrations/MessagesDb/`.

## Required environment

- SQL Server LocalDB (`(localdb)\mssqllocaldb`, see `appsettings.json`) — effectively Windows-only as configured.
- AI settings bind to `ChatAgentOptions` (config section `ChatAgent`: `Endpoint`, `ApiKey`, `DeploymentName` = `gpt-5-mini` by default, `BraveApiKey`); empty values fall back to env vars `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `BRAVE_API_KEY` (see `Program.cs`). Missing values only fail on the first `@Agent` call.
- Node/`npx` on PATH: the Brave Search MCP server (`@modelcontextprotocol/server-brave-search`, version pinned in `ChatAgent.cs`) is launched over stdio on the agent's first use, once per app (the agent is a DI singleton).
- SignalR client JS is loaded from cdnjs in `_Layout.cshtml` (SRI pinned).

## Gotchas

- Timestamps are stored/transported as UTC ISO-8601. `MessagesDbContext` has a value converter that marks `CreatedAt` as `DateTimeKind.Utc` on read; `<time class="utc-time" datetime="...">` elements are converted to local time client-side (once, tracked by `data-converted`).
- The hub, `Index` and `Messages/RenderPartial` require login. The sender name comes from `Context.User`, never from the client.
- New messages are broadcast as `ReceiveMessage(id, user, content, createdAt)`; clients fetch `/Messages/RenderPartial?id=` in a queue to keep order.
- Agent conversations (`AgentSession`) are stored per user (`Context.UserIdentifier`) in `IThreadStore` (in-memory; lost on restart).
- `Clear` hub method removes the caller's agent session but deletes **all** rows in `Messages` (shared chat log) and broadcasts `Cleared` to all clients.

## Conventions

Code comments are largely in Japanese; follow existing style (aligned property/argument columns, local functions, expression-bodied members).
