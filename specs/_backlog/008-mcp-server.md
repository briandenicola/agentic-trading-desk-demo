# MCP Server Exposure

## Priority: P3 (Medium)

## Status: Selected for NEXT iteration — order 5 of 6 (per 2026-06-09 direction). Rescoped from the original Python/FastAPI framing to the current C#/.NET 10 stack.

## Description
Expose the mock data tools as an MCP (Model Context Protocol) server so
external AI agents can discover and call them.

## Scope
- MCP server implementation wrapping the existing tool functions.
- Tool discovery endpoint listing available tools + schemas.
- SSE or stdio transport (both supported by MCP spec).
- Docker-based deployment option for the MCP server standalone.
- Documentation for connecting Copilot, Claude, or other MCP clients.

## Acceptance Criteria
- [ ] MCP server starts and lists all tools from the orchestration-api tool wrappers (`src\orchestration-api\Agents\Tools\`) / `openapi\tools.yaml`.
- [ ] External MCP client can call tools and receive responses.
- [ ] Same tool functions serve both internal agents and MCP.
- [ ] Works standalone (Docker) or embedded in the orchestration-api (ASP.NET) app.
- [ ] README documents how to connect MCP clients.

## Dependencies
- 001-real-data-connectors (more useful with real data behind tools)

## Notes
The `openapi/tools.yaml` already describes the tool interface. MCP adds
a protocol layer for AI-native discovery. Use the C# MCP SDK (`ModelContextProtocol` NuGet).
