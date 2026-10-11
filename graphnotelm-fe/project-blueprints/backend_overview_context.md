# GraphNoteLM — Backend Overview

A .NET 9 ASP.NET Core REST API for managing knowledge graphs made up of notes (nodes), relationships between them, and tags. Users authenticate via JWT and each graph is private to its owner.

---

## How It Starts

`Program.cs` wires everything up in order:

1. Registers infrastructure services (repositories, DbContext, DynamoDB client)
2. Registers application services (auth, graph, node, tag, relationship)
3. Configures JWT Bearer authentication
4. Configures CORS — currently allows `localhost:5173` (the React dev server)
5. Enables Swagger in Development only
6. Runs the app on port `8080`

Environment-specific config is loaded from `appsettings.json` → `appsettings.{Environment}.json`. Secrets (DB connection string, JWT key, AWS credentials) should live in `appsettings.Development.json` locally and in AWS Secrets Manager in production.

---

## Architecture

The project follows a clean layered architecture:

```
HTTP Request
    │
    ▼
API Layer (Controllers)        — receives requests, calls services, returns responses
    │
    ▼
Service Layer (Core/Services)  — business logic, authorization checks, Result<T> returns
    │
    ▼
Repository Layer (Infrastructure/Repository) — data access, one interface per concern
    │
    ├── PostgreSQL (via EF Core)   — Users, NoteGraphMetadata
    └── DynamoDB (AWS SDK)         — NoteGraphDocument (the full graph data)
```

All service methods return a `Result<T>` wrapper — either a success with a value or a failure with an error message. Controllers unwrap this and map it to the appropriate HTTP status code.

---

## Authentication

**Endpoint:** `POST /UserAuth/register` · `POST /UserAuth/login`

- Passwords are hashed before storage (never stored plain)
- Login returns a JWT access token (default: 60 minute expiry)
- All other endpoints require `Authorization: Bearer <token>`
- The token carries the user's ID, which is used to scope all data access

---

## Database Split

| Data | Database | Why |
|------|----------|-----|
| Users | PostgreSQL (RDS) | Relational, needs transactions |
| NoteGraphMetadata (name, description, owner) | PostgreSQL (RDS) | Relational, queried by user |
| NoteGraphDocument (nodes, tags, relationships) | DynamoDB | Document store, flexible schema, AWS-native |

In **Development**, DynamoDB is replaced by an in-memory repository so you don't need AWS credentials to run locally.

---

## API Endpoints

All endpoints except `/UserAuth/*` require a valid JWT.

### Graphs — `/NoteGraph`

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/NoteGraph/list` | Get all graphs owned by the current user |
| `GET` | `/NoteGraph/id/{noteGraphId}` | Get a single graph with all its data |
| `POST` | `/NoteGraph/create` | Create a new empty graph |
| `PATCH` | `/NoteGraph/edit/{noteGraphId}/metadata` | Edit graph name/description |
| `DELETE` | `/NoteGraph/delete/{noteGraphId}` | Soft-delete a graph |
| `POST` | `/NoteGraph/create/import` | Create a graph from an exported JSON document |
| `GET` | `/NoteGraph/id/{noteGraphId}/export` | Export a graph as a JSON document |

### Nodes — `/NoteGraph/id/{noteGraphId}/node`

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `.../node/create` | Add a new node to the graph |
| `PATCH` | `.../node/edit/{noteNodeId}` | Edit a node's title |
| `PATCH` | `.../node/{nodeId}/content` | Save the note content of a node |
| `PATCH` | `.../node/{nodeId}/metadata` | Edit node metadata (confidence rate, LLM metadata) |
| `DELETE` | `.../node/delete/{noteNodeId}` | Remove a node from the graph |

### Relationships — `/NoteGraph/id/{noteGraphId}/relationships`

Relationships are defined at the graph level first (e.g., "causes", "depends on"), then assigned between nodes.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `.../relationships` | List all relationship types defined in the graph |
| `POST` | `.../relationships/create` | Create a new relationship type |
| `PATCH` | `.../relationships/edit/{relationId}` | Edit a relationship type |
| `DELETE` | `.../relationships/delete/{relationId}` | Delete a relationship type |
| `POST` | `.../node/{nodeId}/relationships` | Assign a relationship between two nodes |
| `DELETE` | `.../node/{nodeId}/relationships/{targetNodeId}/{relationshipId}` | Remove a relationship between two nodes |

### Tags — `/NoteGraph/id/{noteGraphId}/tags`

Tags work the same way — defined at the graph level, then applied to nodes.

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `.../tags` | List all tags defined in the graph |
| `POST` | `.../tags/create` | Create a new tag |
| `PATCH` | `.../tags/edit/{tagId}` | Edit a tag |
| `DELETE` | `.../tags/delete/{tagId}` | Delete a tag |
| `POST` | `.../node/{nodeId}/tags` | Apply a tag to a node |
| `DELETE` | `.../node/{nodeId}/tags/{tagId}` | Remove a tag from a node |

### Write-Through — `/NoteGraphWriteThrough`

| Method | Route | Description |
|--------|-------|-------------|
| `PUT` | `/{noteGraphId}` | Replace the entire graph document in one operation |

This endpoint accepts the full `NoteGraphDocument` and overwrites it wholesale. Used for bulk saves rather than incremental updates.

---

## Data Models

### NoteGraphDocument
The full graph stored in DynamoDB. Contains everything below under one document keyed by graph ID.

```
NoteGraphDocument
├── Id (Guid)
├── UserId (Guid)
├── Nodes: Dictionary<Guid, NoteNode>
│       └── NoteNode
│           ├── Id, Title, Note (string content)
│           ├── Metadata: { UserConfidenceRate (float), LLMMetadata (string) }
│           ├── Relationships: [{ TargetNodeId, RelationshipId }]
│           └── Tags: [Guid]
├── Relationships: Dictionary<Guid, RelationshipDefinition>
│       └── { Name, Color, Inverse }
└── Tags: Dictionary<Guid, TagDefinition>
        └── { Name, Color }
```

### NoteGraphMetadata (PostgreSQL)
Lightweight record used for listing graphs without loading the full document:
- `Id`, `UserId`, `Name`, `Description`, `IsPublic`, `IsDeleted`, `CreatedAt`, `UpdatedAt`

### User (PostgreSQL)
- `Id`, `Username`, `Email`, `PasswordHash`, `CreatedAt`, `LastLoginAt`

---

## Services

| Service | Responsibility |
|---------|---------------|
| `AuthService` | Register users, validate credentials, return JWT tokens |
| `JwtTokenService` | Create signed JWT access tokens |
| `NoteGraphService` | CRUD for graphs, import/export |
| `NoteNodeService` | CRUD for nodes, content and metadata edits |
| `NoteGraphRelationshipService` | Manage relationship types and node-to-node assignments |
| `NoteGraphTagService` | Manage tag types and node-to-tag assignments |
| `NoteGraphAccessService` | Authorization — verifies the current user owns the graph before any operation |
| `NoteGraphWriteThroughService` | Bulk replace a graph document |

`NoteGraphAccessService` is called at the start of every graph operation. It loads the graph metadata from PostgreSQL and confirms the requesting user's ID matches `UserId`. If not, the service returns a failure result and the controller returns `403`.

---

## Unimplemented Services (Planned)

Two services are defined with interfaces but have no implementation yet:

- **`IGraphAnalysisService`** — graph traversal algorithms: least confident path, knowledge frontier, best path within a confidence budget
- **`IGraphProcessorService`** — batch adjustments to node confidence rates and LLM metadata across a graph

These suggest planned AI/ML features for analyzing and scoring the knowledge graph.

---

## Running Locally

```powershell
# Requires: .NET 9 SDK, PostgreSQL running on localhost:5432

# Copy and fill in secrets
cp appsettings.Example.json appsettings.Development.json

dotnet run
# Swagger UI available at https://localhost:{port}/swagger
```

Or with Docker (includes DynamoDB Local):

```powershell
docker-compose up
# App on localhost:8080
# DynamoDB Local on localhost:8000
```

EF Core migrations run automatically on startup via `AppDbContext`. No manual migration step needed for local development.
