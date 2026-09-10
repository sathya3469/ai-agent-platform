import { useState } from "react";
import { ChatBox } from "./components/ChatBox";
import { DocumentUpload } from "./components/DocumentUpload";
import "./App.css";

const navItems = [
  {
    label: "Overview",
    icon: (
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <rect x="3" y="3" width="7" height="9" rx="1.5" />
        <rect x="14" y="3" width="7" height="5" rx="1.5" />
        <rect x="14" y="12" width="7" height="9" rx="1.5" />
        <rect x="3" y="16" width="7" height="5" rx="1.5" />
      </svg>
    ),
  },
  {
    label: "Conversation",
    icon: (
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M21 11.5a8.5 8.5 0 0 1-8.5 8.5c-1.3 0-2.6-.3-3.7-.8L3 21l1.8-5.3A8.5 8.5 0 1 1 21 11.5Z" />
        <path d="M8.5 11.5h.01M12 11.5h.01M15.5 11.5h.01" />
      </svg>
    ),
  },
  {
    label: "Documents",
    icon: (
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" />
        <path d="M14 2v6h6M8 13h8M8 17h5" />
      </svg>
    ),
  },
  {
    label: "Knowledge",
    icon: (
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
        <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2Z" />
      </svg>
    ),
  },
  {
    label: "Activity",
    icon: (
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M3 3v18h18" />
        <path d="m7 16 4-5 3 3 5-7" />
      </svg>
    ),
  },
  {
    label: "Settings",
    icon: (
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
        <path d="M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z" />
        <path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.83 2.83-.06-.06A1.7 1.7 0 0 0 15 19.4a1.7 1.7 0 0 0-1 .4 1.7 1.7 0 0 0-.4 1v.07H9.5V20a1.7 1.7 0 0 0-1-.4 1.7 1.7 0 0 0-1.88.34l-.06.06-2.83-2.83.06-.06A1.7 1.7 0 0 0 4.6 15a1.7 1.7 0 0 0-.4-1 1.7 1.7 0 0 0-1-.4h-.07V9.5H4a1.7 1.7 0 0 0 .4-1 1.7 1.7 0 0 0-.34-1.88l-.06-.06 2.83-2.83.06.06A1.7 1.7 0 0 0 9 4.6a1.7 1.7 0 0 0 1-.4 1.7 1.7 0 0 0 .4-1V3.13h4v.07a1.7 1.7 0 0 0 1 .4 1.7 1.7 0 0 0 1.88-.34l.06-.06 2.83 2.83-.06.06A1.7 1.7 0 0 0 19.4 9c.13.37.27.7.4 1a1.7 1.7 0 0 0 1 .4h.07v4H20a1.7 1.7 0 0 0-.4 1Z" />
      </svg>
    ),
  },
];

function App() {
  const [activeNav, setActiveNav] = useState("Overview");

  const startConversation = () => {
    document.querySelector<HTMLElement>(".chat-panel")?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
    setActiveNav("Conversation");
  };

  const findDocuments = () => {
    document.getElementById("documents")?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
    setActiveNav("Documents");
  };

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark" aria-hidden="true">AI</div>
          <div className="brand-copy">
            <strong>Agent Console</strong>
            <span>Intelligent workspace</span>
          </div>
        </div>

        <button className="new-chat-button" type="button" onClick={startConversation}>
          <span className="new-chat-plus" aria-hidden="true">+</span>
          New conversation
        </button>

        <div className="sidebar-scroll">
          <div className="sidebar-section">
            <p className="sidebar-label">Workspace</p>
            <nav className="sidebar-nav" aria-label="Primary navigation">
              {navItems.map((item) => (
                <button
                  className={`nav-item ${activeNav === item.label ? "active" : ""}`}
                  key={item.label}
                  type="button"
                  onClick={() => setActiveNav(item.label)}
                  aria-current={activeNav === item.label ? "page" : undefined}
                >
                  <span className="nav-icon">{item.icon}</span>
                  {item.label}
                </button>
              ))}
            </nav>
          </div>

          <div className="sidebar-section">
            <p className="sidebar-label">Quick access</p>
            <button className="nav-item" type="button" onClick={findDocuments}>
              <span className="nav-icon">
                <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 3v12" />
                  <path d="m7 10 5 5 5-5" />
                  <path d="M5 21h14" />
                </svg>
              </span>
              Upload source
            </button>
            <button className="nav-item" type="button" onClick={startConversation}>
              <span className="nav-icon">
                <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 3a9 9 0 1 0 9 9" />
                  <path d="M12 7v5l3 3" />
                </svg>
              </span>
              Recent activity
            </button>
          </div>
        </div>

        <div className="sidebar-footer">
          <div className="env-badge"><span aria-hidden="true" /> Development</div>
          <div className="user-row">
            <div className="user-avatar" aria-hidden="true">AK</div>
            <div>
              <strong>Alex Kim</strong>
              <span>Workspace owner</span>
            </div>
          </div>
        </div>
      </aside>

      <div className="main-shell">
        <header className="topbar">
          <div className="topbar-left">
            <div className="breadcrumb" aria-label="Breadcrumb">
              <span>Workspace</span>
              <span className="breadcrumb-separator" aria-hidden="true">/</span>
              <strong>Agent Console</strong>
            </div>
            <div className="topbar-title">
              <span className="topbar-kicker">Your AI workspace</span>
              <h1>Good morning, Alex.</h1>
            </div>
          </div>

          <div className="topbar-right">
            <button className="search-box" type="button" onClick={findDocuments}>
              <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
                <circle cx="11" cy="11" r="7" />
                <path d="m20 20-4-4" />
              </svg>
              <span>Search workspace</span>
              <kbd>⌘ K</kbd>
            </button>
            <button className="topbar-icon" type="button" aria-label="Notifications">
              <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" />
                <path d="M10 21h4" />
              </svg>
              <span className="notification-dot" aria-hidden="true" />
            </button>
            <div className="user-avatar user-avatar-small" aria-hidden="true">AK</div>
          </div>
        </header>

        <main className="workspace">
          <section className="overview" aria-labelledby="page-title">
            <div className="overview-heading">
              <div>
                <p className="eyebrow">Workspace overview</p>
                <h2 id="page-title">Make every answer context-aware.</h2>
                <p className="page-subtitle">Upload your sources, ask focused questions, and keep the conversation moving in one place.</p>
              </div>
              <button className="primary-action" type="button" onClick={startConversation}>
                <span aria-hidden="true">+</span>
                Start a conversation
              </button>
            </div>

            <div className="metrics-grid">
              <article className="metric-card">
                <div className="metric-icon metric-icon-blue" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" /><path d="M14 2v6h6M8 13h8M8 17h5" /></svg>
                </div>
                <div>
                  <p className="metric-label">Documents indexed</p>
                  <strong className="metric-value">12</strong>
                  <span className="metric-trend">+2 this week</span>
                </div>
              </article>

              <article className="metric-card">
                <div className="metric-icon metric-icon-violet" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><path d="M21 11.5a8.5 8.5 0 0 1-8.5 8.5c-1.3 0-2.6-.3-3.7-.8L3 21l1.8-5.3A8.5 8.5 0 1 1 21 11.5Z" /><path d="M8.5 11.5h.01M12 11.5h.01M15.5 11.5h.01" /></svg>
                </div>
                <div>
                  <p className="metric-label">Active conversations</p>
                  <strong className="metric-value">04</strong>
                  <span className="metric-trend">Ready to continue</span>
                </div>
              </article>

              <article className="metric-card">
                <div className="metric-icon metric-icon-teal" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><path d="M12 3v12" /><path d="m7 10 5 5 5-5" /><path d="M5 21h14" /></svg>
                </div>
                <div>
                  <p className="metric-label">Average response</p>
                  <strong className="metric-value">1.8s</strong>
                  <span className="metric-trend">Streaming live</span>
                </div>
              </article>

              <article className="metric-card">
                <div className="metric-icon metric-icon-amber" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><path d="M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Z" /><path d="M12 6v6l4 2" /></svg>
                </div>
                <div>
                  <p className="metric-label">Service health</p>
                  <strong className="metric-value">99.9%</strong>
                  <span className="metric-trend">All systems nominal</span>
                </div>
              </article>
            </div>
          </section>

          <section className="workspace-grid">
            <ChatBox />

            <aside id="documents" className="knowledge-panel panel" aria-labelledby="knowledge-title">
              <div className="panel-header knowledge-header">
                <div>
                  <p className="eyebrow">Knowledge base</p>
                  <h2 id="knowledge-title">Document sources</h2>
                  <p>Give your assistant the context it needs.</p>
                </div>
                <span className="document-count">4 files</span>
              </div>

              <DocumentUpload />

              <div className="knowledge-footer">
                <div className="knowledge-footer-title">Workspace readiness</div>
                <div className="knowledge-progress" aria-label="Knowledge base readiness">
                  <div className="knowledge-progress-track"><div className="knowledge-progress-fill" /></div>
                  <span className="knowledge-progress-label">Sources connected</span>
                </div>
                <p className="knowledge-footer-note">Upload a document to expand the assistant's context.</p>
              </div>
            </aside>
          </section>
        </main>
      </div>
    </div>
  );
}

export default App;
