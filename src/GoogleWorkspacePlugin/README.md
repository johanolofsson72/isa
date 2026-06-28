# GoogleWorkspacePlugin

GoogleWorkspacePlugin provides integration with Google Drive, Calendar, and Chat for collaborative workflows in MCP/Agent environments.

## Features
- **Drive**: List, upload, and read files in the shared "Work" folder.
- **Calendar**: Create and list events in the shared calendar.
- **Chat**: Send messages to users via Google Chat.

## Configuration (appsettings.json)
```json
"GoogleWorkspace": {
  "ClientId": "...",
  "ClientSecret": "...",
  "ServiceAccountEmail": "...",
  "ImpersonatedUserEmail": "agent@example.com",
  "WorkDriveFolderName": "Work",
  "SharedCalendarId": "...",
  "TokenPath": "config/google-token.json",
  "CredentialsPath": "config/accountingagentdrive-c18d67539c7b.json"
}
```

## Supported Channels
- IO (stdin/stdout, JSON)
- HTTP (REST endpoints)

## Example JSON Requests (IO channel)
- List files:
```json
{ "method": "drive.list", "parameters": { "folder": "Work" } }
```
- Upload file:
```json
{ "method": "drive.upload", "parameters": { "fileName": "test.txt", "content": "...base64...", "folder": "Work" } }
```
- Read file:
```json
{ "method": "drive.read", "parameters": { "fileName": "test.txt", "folder": "Work" } }
```
- Create calendar event:
```json
{ "method": "calendar.create", "parameters": { "title": "Test", "start": "2024-07-01T10:00:00Z", "end": "2024-07-01T11:00:00Z", "desc": "Demo" } }
```
- List calendar events:
```json
{ "method": "calendar.list", "parameters": { "max": 5 } }
```
- Send chat message:
```json
{ "method": "chat.send", "parameters": { "to": "user@example.com", "message": "Hej!" } }
```

## HTTP Endpoints
- `/drive/list?folder=Work`
- `/drive/upload` (POST: fileName, content, folder)
- `/drive/read?fileName=...&folder=...`
- `/calendar/create` (POST: title, start, end, desc)
- `/calendar/list?max=5`
- `/chat/send` (POST: to, message)

## MCP/VSCode Compatibility
- Följer MCP-serverstandarden
- Kan installeras som verktyg i VSCode Copilot Chat
- Kan driftas i produktion på Linux-kluster

## Author
Example 