# 🔧 Maintenance Incident RAG in .NET

A fully local **Retrieval-Augmented Generation (RAG)** API built with **.NET 10** and **Ollama**, designed to assist maintenance teams by answering technical questions based on a historical incident knowledge base.

> **Use case:** A maintenance engineer asks *"What are the most frequent incidents on hydraulic pumps?"* — the system retrieves the most relevant past incidents from the database, then generates a precise, sourced answer using a local LLM.

---

## 📸 Demo

### Swagger UI
![Swagger UI](docs/screenshots/swagger.png)

### Ask UI (Razor Page)
![Ask UI](docs/screenshots/ask-ui.png)

---

## ✨ Features

- 🔍 **Semantic search** — vector similarity via `pgvector` (`<=>` cosine distance)
- 📝 **Full-text search** — PostgreSQL `ts_rank` on incident descriptions
- ⚡ **Hybrid search** — combines vector and full-text scores for higher recall
- 🔁 **Reranking** — custom scoring merges both signals before prompt construction
- 🤖 **RAG pipeline** — retrieval → prompt engineering → LLM generation
- 📌 **Sourced answers** — every response includes the incident IDs used as context
- 💬 **Multi-turn chat** — persistent chat sessions with full message history
- 🏠 **100% local** — no external API keys; everything runs via [Ollama](https://ollama.com)

---

## 🏗️ Architecture

```
User Question
     │
     ▼
 ASP.NET Core API
     │
     ├─► OllamaEmbeddingService  ──► float[] vector (all-minilm)
     │
     ├─► HybridSearch (PostgreSQL + pgvector)
     │       ├─ Vector search  (<=> cosine)
     │       └─ Full-text      (ts_rank)
     │
     ├─► Reranker  ──► top-N incidents
     │
     ├─► PromptBuilder  ──► structured prompt
     │
     └─► OllamaLlmService  ──► answer (mistral)
              │
              ▼
         AskResponse { answer, sources[] }
```

**Project layers:**

| Layer | Responsibility |
|---|---|
| `Domain` | Entities (`Incident`, `ChatSession`, `ChatMessage`, …), repository interfaces |
| `Application` | DTOs, service interfaces, `PromptBuilder` |
| `Infrastructure` | Dapper repositories, `OllamaEmbeddingService`, `OllamaLlmService`, `RagService`, `IncidentIndexingService` |
| `Api` | Minimal API endpoints, Razor Pages UI, DI wiring |

---

## 🛠️ Tech Stack

| Technology | Role |
|---|---|
| .NET 10 / ASP.NET Core | API & Razor Pages UI |
| PostgreSQL 16 | Relational store |
| pgvector | Vector similarity extension |
| Dapper | Lightweight SQL mapper |
| Ollama | Local LLM runtime |
| `all-minilm` | Embedding model (384 dimensions) |
| `mistral` | Text generation model |
| Swashbuckle | Swagger / OpenAPI |

---

## 🚀 Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 16](https://www.postgresql.org/download/) with the [pgvector](https://github.com/pgvector/pgvector) extension
- [Ollama](https://ollama.com/download)

### Steps

**1. Clone the repository**
```bash
git clone https://github.com/maroinecherif/MaintenanceRag.git
cd MaintenanceRag
```

**2. Start PostgreSQL** (default config: `localhost:5433`)

Apply the schema:
```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE incidents (
    id              UUID PRIMARY KEY,
    equipment_name  TEXT NOT NULL,
    incident_date   DATE NOT NULL,
    description     TEXT NOT NULL,
    cause           TEXT,
    solution        TEXT,
    search_text     TEXT NOT NULL
);

CREATE TABLE incident_embeddings (
    incident_id  UUID PRIMARY KEY REFERENCES incidents(id) ON DELETE CASCADE,
    embedding    vector(384)
);

CREATE TABLE conversations (
    id          UUID PRIMARY KEY,
    question    TEXT NOT NULL,
    answer      TEXT NOT NULL,
    equipment   TEXT,
    sources     UUID[] NOT NULL DEFAULT '{}',
    created_at  TIMESTAMPTZ NOT NULL
);

CREATE TABLE chat_sessions (
    id          UUID PRIMARY KEY,
    title       TEXT NOT NULL,
    equipment   TEXT,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL
);

CREATE TABLE chat_messages (
    id          UUID PRIMARY KEY,
    session_id  UUID NOT NULL REFERENCES chat_sessions(id) ON DELETE CASCADE,
    role        TEXT NOT NULL,
    content     TEXT NOT NULL,
    sources     UUID[] NOT NULL DEFAULT '{}',
    created_at  TIMESTAMPTZ NOT NULL
);
```

**3. Start Ollama and pull the required models**
```bash
ollama serve
ollama pull all-minilm
ollama pull mistral
```

**4. Configure `appsettings.json`** (or environment variables)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=MaintenanceRag;Username=postgres;Password=postgres"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "EmbeddingModel": "all-minilm",
    "GenerationModel": "mistral"
  }
}
```

**5. Run the API**
```bash
cd src/MaintenanceRag.Api
dotnet run
```

API available at: `https://localhost:7242` — Swagger UI at `/swagger`

---

## 📡 API Usage

### Index incidents (generate embeddings)

```http
POST /incidents/reindex
```

Iterates over all incidents in the database, generates vector embeddings via Ollama, and stores them in `incident_embeddings`. Run this once after seeding data, or whenever incidents are updated.

---

### Ask a question (RAG)

```http
POST /ask
Content-Type: application/json

{
  "question": "Quels incidents fréquents sur les pompes hydrauliques ?",
  "equipment": "Pompe hydraulique"
}
```

**Response:**
```json
{
  "answer": "Les incidents les plus fréquents sur les pompes hydrauliques concernent les fuites de joints, les surchauffes dues à un manque d'huile et les vibrations anormales liées à des paliers usés...",
  "sources": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "9b2e1c47-8a3d-4f1e-b205-6d4e7c8f9012"
  ]
}
```

---

### Multi-turn chat sessions

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/chat/sessions` | Create a new chat session |
| `GET` | `/chat/sessions` | List 20 most recent sessions |
| `GET` | `/chat/sessions/{id}` | Get session with full message history |
| `POST` | `/chat/sessions/{id}/ask` | Ask a question in an existing session |

---

### Conversation history

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/conversations` | Save a question/answer pair |
| `GET` | `/conversations` | Retrieve the 10 most recent entries |

---

## 💡 Key Concepts

| Concept | Description |
|---|---|
| **RAG** | Augments LLM generation with retrieved documents to ground answers in real data |
| **Embeddings** | Dense vector representations of text that capture semantic meaning |
| **Vector search** | Finds documents closest in meaning using cosine distance (`<=>`) |
| **Hybrid search** | Combines vector similarity and keyword relevance for better recall |
| **Reranking** | Re-scores candidates by merging multiple signals before passing to the LLM |
| **Prompt engineering** | Structures retrieved context + question into an effective LLM prompt |

---

## 🔮 Future Improvements

- [ ] Advanced ChatGPT-style UI with session management
- [ ] Authentication & user-scoped history
- [ ] Advanced filtering (by equipment, date range, severity)
- [ ] Streaming LLM responses (Server-Sent Events)
- [ ] Cloud deployment (Azure Container Apps + Azure Database for PostgreSQL)
- [ ] Evaluation pipeline (answer quality metrics)

---

## 📝 Conclusion

This project demonstrates how to build a **production-grade RAG system from scratch** using only open-source, locally-running components. Key takeaways include integrating `pgvector` with raw SQL via Dapper, building a hybrid search pipeline, applying custom reranking, and orchestrating a full LLM workflow — all without relying on any external paid API.

It serves as a practical foundation for any domain-specific knowledge assistant: maintenance records, legal documents, customer support tickets, and more.

---

## 📄 License

MIT
