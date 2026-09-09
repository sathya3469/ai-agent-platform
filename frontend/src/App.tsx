import { useState } from "react";
import { ChatBox } from "./components/ChatBox";
import { DocumentUpload } from "./components/DocumentUpload";
import "./App.css";

function App() {
  const [showDocumentUpload, setShowDocumentUpload] = useState(false);

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 to-slate-800">
      <div className="max-w-6xl mx-auto">
        {/* Header */}
        <div className="border-b border-slate-700 bg-slate-800/50 backdrop-blur-sm sticky top-0 z-10">
          <div className="flex items-center justify-between p-4">
            <div>
              <h1 className="text-2xl font-bold text-white">AI Agent Platform</h1>
              <p className="text-sm text-slate-400">With RAG & Document Retrieval</p>
            </div>
            <button
              onClick={() => setShowDocumentUpload(!showDocumentUpload)}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition flex items-center gap-2"
            >
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              {showDocumentUpload ? "Hide" : "Upload Documents"}
            </button>
          </div>
        </div>

        {/* Main Content */}
        <div className="flex gap-6 p-6">
          {/* Chat Section */}
          <div className="flex-1">
            <ChatBox />
          </div>

          {/* Document Upload Sidebar */}
          {showDocumentUpload && (
            <div className="w-80">
              <DocumentUpload
                onUploadSuccess={(response) => {
                  console.log("Document uploaded:", response);
                }}
                onUploadError={(error) => {
                  console.error("Upload error:", error);
                }}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default App;