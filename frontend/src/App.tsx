import { useEffect, useState } from "react";
import { ChatBox } from "./components/ChatBox";
import { DocumentUpload } from "./components/DocumentUpload";
import { getDocumentFiles, getHistory } from "./services/chatService";
import "./App.css";

interface Stats {
  knowledgeBases: number;
  chats: number;
  messages: number;
}

/**
 * Fetch a stat independently so one failing endpoint (e.g. the backend being
 * down) does not blank the whole dashboard.
 */
async function loadStat<T>(
  name: string,
  fetcher: () => Promise<T>,
  fallback: T
): Promise<T> {
  try {
    return await fetcher();
  } catch (error) {
    console.error(`Failed to fetch ${name}:`, error);
    return fallback;
  }
}

function App() {
  const [agentMode, setAgentMode] = useState(false);
  const [stats, setStats] = useState<Stats>({
    knowledgeBases: 0,
    chats: 0,
    messages: 0,
  });
  const [fileNames, setFileNames] = useState<string[]>([]);
  const [isLoadingStats, setIsLoadingStats] = useState(true);
  const [documentsAvailable, setDocumentsAvailable] = useState(true);

  const refreshStats = async () => {
    const [documents, history] = await Promise.all([
      loadStat("documents", getDocumentFiles, null),
      loadStat("chat history", getHistory, []),
    ]);

    setStats({
      knowledgeBases: documents?.fileCount ?? 0,
      chats: history.filter((message) => message.role === "user").length,
      messages: history.length,
    });
    setFileNames(documents?.fileNames ?? []);
    setDocumentsAvailable(documents !== null);
    setIsLoadingStats(false);
  };

  useEffect(() => {
    let cancelled = false;

    const loadInitialStats = async () => {
      const [documents, history] = await Promise.all([
        loadStat("documents", getDocumentFiles, null),
        loadStat("chat history", getHistory, []),
      ]);

      if (cancelled) return;

      setStats({
        knowledgeBases: documents?.fileCount ?? 0,
        chats: history.filter((message) => message.role === "user").length,
        messages: history.length,
      });
      setFileNames(documents?.fileNames ?? []);
      setDocumentsAvailable(documents !== null);
      setIsLoadingStats(false);
    };

    void loadInitialStats();

    return () => {
      cancelled = true;
    };
  }, []);

  const renderStat = (value: number) => (isLoadingStats ? "--" : value);

  return (
    <main className="workspace">
      {/* Hero */}
      <section className="overview" id="new-kb">
        <div className="overview-heading">
          <div>
            <p className="eyebrow">AI Knowledge Platform</p>
            <h2>Knowledge Assistant</h2>
            <p className="page-subtitle">
              Your personal AI-powered knowledge hub. Upload documents, create
              knowledge bases, and get instant answers through natural
              conversations.
            </p>
          </div>
          <a className="primary-action" href="#knowledge">
            <span aria-hidden="true">+</span>
            New Knowledge Base
          </a>

          <button
            type="button"
            onClick={() => setAgentMode((enabled) => !enabled)}
            className={`agent-toggle ${agentMode ? "agent-toggle-active" : ""}`}
            aria-pressed={agentMode}
          >
            {agentMode ? "🤖 Agent" : "💬 Chat"}
          </button>
        </div>
      </section>

      {/* Stats */}
      <div className="metrics-grid metrics-grid-2">
        <div className="metric-card">
          <div className="metric-icon metric-icon-blue" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" />
            </svg>
          </div>
          <div>
            <p className="metric-label">Knowledge Bases</p>
            <span className="metric-value">{renderStat(stats.knowledgeBases)}</span>
            <span className="metric-trend">documents indexed</span>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon metric-icon-violet" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <path d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
            </svg>
          </div>
          <div>
            <p className="metric-label">Chat Sessions</p>
            <span className="metric-value">{renderStat(stats.chats)}</span>
            <span className="metric-trend">{stats.messages} messages exchanged</span>
          </div>
        </div>
      </div>

      {/* Quick Actions */}
      <section className="overview">
        <div className="overview-heading">
          <div>
            <p className="eyebrow">Get started</p>
            <h2>Quick Actions</h2>
          </div>
        </div>
        <div className="quick-actions">
          <a className="quick-action" href="#knowledge">
            <div className="quick-action-icon quick-action-blue" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
              </svg>
            </div>
            <h3>Create Knowledge Base</h3>
            <p>Build a new AI-powered knowledge repository</p>
          </a>

          <a className="quick-action" href="#knowledge">
            <div className="quick-action-icon quick-action-indigo" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
            </div>
            <h3>Upload Documents</h3>
            <p>Add PDF, DOCX, MD or TXT files to your knowledge bases</p>
          </a>

          <a className="quick-action" href="#chat">
            <div className="quick-action-icon quick-action-purple" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" />
              </svg>
            </div>
            <h3>Start Chatting</h3>
            <p>Get instant answers from your knowledge with AI</p>
          </a>
        </div>
      </section>

      {/* Knowledge + Chat workspace */}
      <div className="workspace-grid">
        <section className="panel knowledge-panel" id="knowledge">
          <div className="panel-header knowledge-header">
            <div>
              <h2>Knowledge Base</h2>
              <p>Drop a document to index it for AI-powered retrieval.</p>
            </div>
            <span className="document-count">
              {isLoadingStats ? "…" : `${stats.knowledgeBases} document${stats.knowledgeBases === 1 ? "" : "s"}`}
            </span>
          </div>

          <DocumentUpload
            onUploadSuccess={refreshStats}
            onUploadError={(error) => console.error("Upload failed:", error)}
          />

          <div className="knowledge-footer">
            <p className="knowledge-footer-title">Indexed files</p>
            {fileNames.length > 0 ? (
              <ul className="knowledge-file-list">
                {fileNames.map((name) => (
                  <li key={name} className="knowledge-file-item">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
                      <path d="M14 2v6h6" />
                    </svg>
                    <span>{name}</span>
                  </li>
                ))}
              </ul>
            ) : documentsAvailable ? (
              <p className="knowledge-footer-note">
                No documents indexed yet. Upload your first file to start
                answering questions from your own knowledge.
              </p>
            ) : (
              <p className="knowledge-footer-note knowledge-footer-error">
                Couldn’t reach the document service. Check that the backend is
                running and reachable, then try again.
              </p>
            )}
          </div>
        </section>

        <div id="chat" className="chat-slot">
          <ChatBox agentMode={agentMode} />
        </div>
      </div>

      {/* How It Works */}
      <section className="panel how-it-works">
        <div className="panel-header">
          <div>
            <h2>How It Works</h2>
            <p>Three steps from raw documents to conversational answers.</p>
          </div>
        </div>
        <div className="steps">
          <div className="step">
            <div className="step-number" aria-hidden="true">1</div>
            <div>
              <h3>Create a Knowledge Base</h3>
              <p>
                Start by creating a new knowledge base to organize your
                information. Give it a name and description that helps you
                identify its purpose.
              </p>
              <a className="step-link" href="#knowledge">
                Create now
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M9 5l7 7-7 7" />
                </svg>
              </a>
            </div>
          </div>

          <div className="step">
            <div className="step-number step-number-indigo" aria-hidden="true">2</div>
            <div>
              <h3>Upload Your Documents</h3>
              <p>
                Upload PDF, DOCX, MD or TXT files to your knowledge base. Our
                system will process and index them for AI-powered retrieval.
              </p>
              <a className="step-link step-link-indigo" href="#knowledge">
                Upload documents
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M9 5l7 7-7 7" />
                </svg>
              </a>
            </div>
          </div>

          <div className="step">
            <div className="step-number step-number-purple" aria-hidden="true">3</div>
            <div>
              <h3>Chat With Your Knowledge</h3>
              <p>
                Start a conversation with your knowledge base. Ask questions
                in natural language and get accurate answers based on your
                documents.
              </p>
              <a className="step-link step-link-purple" href="#chat">
                Start chatting
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M9 5l7 7-7 7" />
                </svg>
              </a>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}

export default App;
