# MCP Server Exposure

## Priority: P3 (Medium)

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
- [ ] MCP server starts and lists all tools from `api/agents/tools.py`.
- [ ] External MCP client can call tools and receive responses.
- [ ] Same tool functions serve both internal agents and MCP.
- [ ] Works standalone (Docker) or embedded in the main FastAPI app.
- [ ] README documents how to connect MCP clients.

## Dependencies
- 001-real-data-connectors (more useful with real data behind tools)

## Notes
The `openapi/tools.yaml` already describes the tool interface. MCP adds
a protocol layer for AI-native discovery. Consider `mcp-python-sdk`.
