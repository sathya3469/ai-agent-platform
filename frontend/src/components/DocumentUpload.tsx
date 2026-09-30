import React, { useRef, useState } from "react";

interface DocumentUploadProps {
  onUploadSuccess?: (message: string) => void;
  onUploadError?: (error: string) => void;
}

interface UploadResponse {
  message?: string;
  error?: string;
  documentId?: string;
  fileName?: string;
  chunksCreated?: number;
  progress?: number;
}

const ACCEPTED_EXTENSIONS = [
  ".txt", ".md", ".csv", ".html", ".xml", ".json", ".log", ".pdf", ".doc", ".docx",
] as const;

export const DocumentUpload: React.FC<DocumentUploadProps> = ({
  onUploadSuccess,
  onUploadError,
}) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadMessage, setUploadMessage] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [processingStage, setProcessingStage] = useState<string>("");
  const fileInputRef = useRef<HTMLInputElement>(null);
  const uploadIdRef = useRef(0);
  // Stops the progressive "still working" timers when the upload settles.
  const stopStageTimers = useRef<(() => void) | null>(null);

  const API_BASE_URL = import.meta.env.VITE_API_URL ?? "";

  const PROCESSING_STAGES: { at: number; message: (fileName: string) => string }[] = [
    { at: 0, message: (name) => `Uploading ${name}...` },
    { at: 5000, message: (name) => `Reading and chunking ${name}...` },
    { at: 20000, message: (name) => `Embedding ${name} (this can take a while)...` },
    { at: 60000, message: () => "Still indexing — generating embeddings for each chunk..." },
  ];

  const UPLOAD_TIMEOUT_MS = 10 * 60 * 1000;

  const resetFeedback = () => {
    setUploadError(null);
    setUploadMessage(null);
  };

  const handleFile = async (file: File) => {
    const extension = (file.name.split(".").pop() ?? "").toLowerCase();
    const extensionWithDot = extension ? `.${extension}` : "";

    if (!ACCEPTED_EXTENSIONS.includes(extensionWithDot as (typeof ACCEPTED_EXTENSIONS)[number])) {
      const message = `Unsupported file type "${extensionWithDot}". Only TXT, MD, CSV, HTML, XML, JSON, LOG, PDF, DOC or DOCX are supported.`;
      setUploadError(message);
      onUploadError?.(message);
      return;
    }

    // Cancel any in-flight upload so its stale callbacks can't clobber the new one.
    uploadIdRef.current += 1;
    const uploadId = uploadIdRef.current;

    resetFeedback();
    setSelectedFile(file);
    setIsUploading(true);
    setProcessingStage(`Uploading ${file.name}...`);

    // Progressive "still working" messaging: a large PDF can take minutes to
    // embed on a cold Ollama instance, and a stuck spinner looks like a dead
    // upload. Cleared on completion and on any timeout/error path below.
    const stageTimers = PROCESSING_STAGES.map(({ at, message }) =>
      window.setTimeout(() => {
        if (uploadId === uploadIdRef.current && !stopStageTimers.current) {
          setProcessingStage(message(file.name));
        }
      }, at)
    );
    stopStageTimers.current = () => stageTimers.forEach((id) => window.clearTimeout(id));

    // Absolute deadline. Ollama embedding of a large document is slow, so this
    // is generous — but without it a stalled backend hangs the UI indefinitely.
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), UPLOAD_TIMEOUT_MS);

    try {
      const formData = new FormData();
      formData.append("file", file);

      const response = await fetch(`${API_BASE_URL}/api/Document/upload`, {
        method: "POST",
        body: formData,
        signal: controller.signal,
      });

      let data: UploadResponse = {};
      try {
        data = (await response.json()) as UploadResponse;
      } catch {
        throw new Error(
          `The server returned an unexpected response (HTTP ${response.status}). Is the backend running${
            API_BASE_URL ? ` on ${API_BASE_URL}` : " and reachable"
          }?`
        );
      }

      if (uploadId !== uploadIdRef.current) return;

      if (!response.ok) {
        const errorMsg = data.message ?? data.error ?? `Upload failed (HTTP ${response.status})`;

        // Surface an actionable hint when the dependency chain is down.
        if (response.status === 503 || /chroma|embedding|8000|unavailable/i.test(errorMsg)) {
          throw new Error(
            "Document service unavailable. Please ensure ChromaDB is running on localhost:8000 and the backend can reach it."
          );
        }

        throw new Error(errorMsg);
      }

      const chunksCreated = typeof data.chunksCreated === "number" ? data.chunksCreated : 0;
      const successMessage = `${data.message ?? "Document uploaded successfully"} — ${chunksCreated} chunk${chunksCreated === 1 ? "" : "s"} indexed`;

      setUploadMessage(successMessage);
      setProcessingStage("");
      setSelectedFile(file);
      onUploadSuccess?.(successMessage);
    } catch (error) {
      if (uploadId !== uploadIdRef.current) return;

      // Give an actionable reason for a timeout rather than a bare "Upload
      // failed": the embedding step is by far the slowest part of the pipeline.
      const isTimeout =
        error instanceof DOMException && (error.name === "AbortError" || error.name === "TimeoutError");

      const errorMessage = isTimeout
        ? `Upload timed out after ${Math.round(UPLOAD_TIMEOUT_MS / 1000 / 60)} minutes. ` +
          "The backend may still be embedding the document (Ollama is slow on a cold model) — wait a moment, then try again or use a smaller file."
        : error instanceof Error
          ? error.message
          : "Upload failed";

      setUploadError(errorMessage);
      setProcessingStage("");
      onUploadError?.(errorMessage);
    } finally {
      window.clearTimeout(timeout);
      stopStageTimers.current?.();
      stopStageTimers.current = null;

      if (uploadId === uploadIdRef.current) {
        setIsUploading(false);
      }
    }
  };

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (file) {
      void handleFile(file);
    }
  };

  const handleDrop = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    const file = event.dataTransfer.files?.[0];
    if (file) {
      void handleFile(file);
    }
  };

  const handleDragOver = (event: React.DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (event: React.DragEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setIsDragging(false);
    }
  };

  return (
    <div className="document-upload">
      <div
        className={`drop-zone ${isDragging ? "is-dragging" : ""}`}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        <div className="drop-zone-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z" /><path d="M14 2v6h6M12 17v-6M9 14l3-3 3 3" /></svg>
        </div>
        <div className="drop-zone-content">
          <strong className="drop-zone-title">{selectedFile ? selectedFile.name : "Drop a document here"}</strong>
          <span className="drop-zone-subtitle">{selectedFile ? `${(selectedFile.size / 1024 / 1024).toFixed(2)} MB · indexing…` : "TXT, MD, CSV, HTML, XML, or JSON"}</span>
        </div>
        <button className="upload-button" type="button" onClick={() => fileInputRef.current?.click()} disabled={isUploading}>
          {isUploading ? "Uploading" : "Choose file"}
        </button>
        <input
          ref={fileInputRef}
          className="file-input"
          type="file"
          accept=".txt,.md,.csv,.html,.xml,.json,.log,.pdf,.doc,.docx"
          onChange={handleFileChange}
          disabled={isUploading}
          aria-label="Choose a document to upload"
        />
      </div>

      {selectedFile && (
        <button className="remove-file" type="button" onClick={() => setSelectedFile(null)}>
          Remove selected file
        </button>
      )}

      {isUploading && processingStage && (
        <div className="upload-processing" role="status">
          <span className="upload-spinner" aria-hidden="true" />
          <span>{processingStage}</span>
        </div>
      )}

      {uploadMessage && (
        <div className="upload-message" role="status">
          <span aria-hidden="true">✓</span>
          {uploadMessage}
        </div>
      )}

      {uploadError && (
        <div className="upload-error" role="alert">
          <span aria-hidden="true">!</span>
          {uploadError}
        </div>
      )}
    </div>
  );
};
