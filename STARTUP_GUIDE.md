# Phase 4 RAG Platform - Startup Guide

## 🚀 Quick Start (5 Steps)

### Step 1: Start Ollama (Terminal 1)
```bash
ollama pull nomic-embed-text
ollama serve
```
Expected: Model loads and server listens on `http://localhost:11434`

### Step 2: Start ChromaDB (Terminal 2)
```bash
pip install chromadb
chroma run --host localhost --port 8000
```
Expected: ChromaDB server starts on `http://localhost:8000`

### Step 3: Start Backend (Terminal 3)
```bash
cd E:\ai-agent-platform\backend\AiAgentPlatform.Api
dotnet run
```
Expected output:
```
[12:15:38 INF] Configuring LLM Provider: ollama
[12:15:38 INF] Using Ollama provider (default)
[12:15:39 INF] Now listening on: http://localhost:5243
```

### Step 4: Start Frontend (Terminal 4)
```bash
cd E:\ai-agent-platform\frontend
npm run dev
```
Expected output:
```
  VITE v8.2.2  ready in 234 ms
  ➜  Local:   http://localhost:5173/
```

### Step 5: Open Application
Go to: **http://localhost:5173**

---

## ✅ Verify Everything Works

### Option 1: Run Test Script
```bash
cd E:\ai-agent-platform
test-services.bat
```
This checks all 4 services and shows status.

### Option 2: Manual Verification

**Check Ollama:**
```bash
curl http://localhost:11434/api/tags
```
Should return: JSON with available models

**Check ChromaDB:**
```bash
curl http://localhost:8000/api/v1/heartbeat
```
Should return: `{"ok":true}`

**Check Backend:**
```bash
curl http://localhost:5243/swagger/v1/swagger.json
```
Should return: OpenAPI schema

**Check Frontend:**
Open browser to: `http://localhost:5173`
Should load the chat interface

---

## 🧪 Test RAG Pipeline

### Create Test Document

Save this as `test.txt`:
```
Artificial Intelligence (AI) is intelligence demonstrated by machines.
Machine Learning is a subset of AI that enables systems to learn from data.
Deep Learning uses neural networks to process complex patterns.
Natural Language Processing helps computers understand human language.
Computer Vision enables machines to interpret visual information from images.
```

### Test Steps

1. **Click "Upload Documents"** button in top-right
2. **Select test.txt** file
3. **Wait for success** - should show:
   - Document ID
   - Chunks created (3-5)
   - Upload timestamp
4. **Chat**: Type questions like:
   - "What is machine learning?"
   - "How does AI work?"
   - "Tell me about deep learning"
5. **Verify**: Response should reference your uploaded document

---

## 🔧 Troubleshooting

### Issue: "Backend not responding"
**Check:**
- Is terminal 3 still running `dotnet run`?
- Does it show "Now listening on: http://localhost:5243"?
- Try: `curl http://localhost:5243/swagger/v1/swagger.json`

**Fix:**
```bash
cd E:\ai-agent-platform\backend\AiAgentPlatform.Api
dotnet clean
dotnet build
dotnet run
```

### Issue: "Failed to generate embedding"
**Check:**
- Is Ollama running in terminal 1?
- Does it show the model server listening?
- Try: `curl http://localhost:11434/api/tags`

**Fix:**
```bash
ollama pull nomic-embed-text
ollama serve
```

### Issue: "Failed to query ChromaDB"
**Check:**
- Is ChromaDB running in terminal 2?
- Try: `curl http://localhost:8000/api/v1/heartbeat`

**Fix:**
```bash
pip install chromadb
chroma run --host localhost --port 8000
```

### Issue: "Document upload fails"
**Check:**
- Backend running and accessible
- ChromaDB running
- Ollama running with embedding model
- Try uploading from browser console: `fetch('http://localhost:5243/swagger/v1/swagger.json')`

### Issue: "Chat not streaming"
**Check:**
- Backend API endpoint is `/api/chat/stream` (not `/api/chat/chat`)
- Verify in browser DevTools → Network tab
- Check API_BASE_URL in chatService.tsx is `http://localhost:5243`

---

## 📊 Expected Performance

| Operation | Time |
|-----------|------|
| Upload & chunk document | < 1s |
| Generate embeddings | 100-500ms per chunk |
| ChromaDB retrieval | 50-200ms |
| Full RAG pipeline | 1-2 seconds |
| Chat response | 2-5 seconds (includes streaming) |

---

## 🔗 Important URLs

| Service | URL | Purpose |
|---------|-----|---------|
| Frontend | http://localhost:5173 | Web UI |
| Backend | http://localhost:5243 | API |
| API Docs | http://localhost:5243/swagger | Swagger documentation |
| Ollama | http://localhost:11434 | Embeddings |
| ChromaDB | http://localhost:8000 | Vector database |

---

## 📋 Architecture

```
┌─────────────────────────────────────────────────┐
│         Frontend (React, Vite)                   │
│         http://localhost:5173                    │
└──────────────────┬──────────────────────────────┘
                   │ HTTP
                   ▼
┌─────────────────────────────────────────────────┐
│    Backend API (.NET 8, OpenAPI)                │
│    http://localhost:5243                        │
│                                                  │
│  ├─ ChatService (with RAG)                      │
│  ├─ RagService                                  │
│  ├─ EmbeddingService ──────────┐                │
│  ├─ ChromaDbService ────────┐   │                │
│  └─ DocumentController      │   │                │
└──────────┬──────────────────┼───┼────────────────┘
           │                  │   │
           │                  │   ▼
           │                  │  ┌────────────────┐
           │                  │  │ Ollama         │
           │                  │  │ (Embeddings)   │
           │                  │  │ :11434         │
           │                  │  └────────────────┘
           │                  │
           │                  ▼
           │             ┌────────────────┐
           └─────────────┤ ChromaDB       │
                         │ (Vector DB)    │
                         │ :8000          │
                         └────────────────┘
```

---

## 🎯 Next Steps After Testing

If everything works:

1. **Upload your own documents**
   - PDF files
   - Text files
   - Multiple documents

2. **Test different queries**
   - General questions
   - Specific information from docs
   - Comparison questions

3. **Monitor performance**
   - Check response times
   - Verify accuracy of retrieved context
   - Test with large documents

4. **Explore Advanced Features** (Phase 5)
   - Document management
   - Multi-user support
   - Analytics
   - Citation tracking

---

## 💾 Saving Your Work

All changes are already committed to git:

```bash
cd E:\ai-agent-platform
git log --oneline | head -10
```

To push to GitHub:
```bash
git push origin main
```

---

## 🆘 Getting Help

**Check logs:**
- Backend: `E:\ai-agent-platform\backend\AiAgentPlatform.Api\logs\`
- Browser console: Press F12 → Console tab
- Network tab: F12 → Network → Check API calls

**Common issues:**
- Port conflicts: Change port in appsettings.json or services
- CORS errors: Already configured for localhost:5173
- Model not found: Run `ollama pull nomic-embed-text`

---

**Status**: ✅ All services configured and ready to test

Start with Step 1 above and follow through Step 5!
