#!/usr/bin/env python3
"""
Embedding service for ICD-10-CM RAG search.

Provides a simple HTTP API to generate embeddings for user queries at runtime.
The ICD10AM.Api calls this service to encode search queries.

Usage:
    python embedding_service.py
    # Runs on http://localhost:8000

API:
    POST /embed
    {"text": "chest pain with difficulty breathing"}

    Response:
    {"Embedding": [0.123, -0.456, ...]}
"""

from fastapi import FastAPI
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
import uvicorn

# Model configuration - must match generate_embeddings.py
EMBEDDING_MODEL = "abhinand/MedEmbed-small-v0.1"

app = FastAPI(
    title="MedEmbed Embedding Service",
    description="Generates medical embeddings for ICD-10 RAG search",
    version="1.0.0"
)

print(f"Loading MedEmbed model: {EMBEDDING_MODEL}")
model = SentenceTransformer(EMBEDDING_MODEL)
print(f"Model loaded! Dimensions: {model.get_sentence_embedding_dimension()}")


class EmbedRequest(BaseModel):
    """Request to generate embedding for text."""
    text: str


class EmbedResponse(BaseModel):
    """Response containing the embedding vector."""
    Embedding: list[float]


@app.post("/embed", response_model=EmbedResponse)
def embed(request: EmbedRequest) -> EmbedResponse:
    """Generate embedding for the given text."""
    embedding = model.encode(request.text).tolist()
    return EmbedResponse(Embedding=embedding)


@app.get("/health")
def health():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "model": EMBEDDING_MODEL,
        "dimensions": model.get_sentence_embedding_dimension()
    }


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")
