import React, { useRef, useState } from "react";

interface DocumentUploadProps {
  onUploadSuccess?: (message: string) => void;
  onUploadError?: (error: string) => void;
}

export const DocumentUpload: React.FC<DocumentUploadProps> = ({
  onUploadSuccess,
  onUploadError,
}) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadMessage, setUploadMessage] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const apiUrl = import.meta.env.VITE_API_URL || "http://localhost:5243";

  const handleFile = async (file: File) => {
    setSelectedFile(file);
    setUploadError(null);
    setUploadMessage(null);
    setIsUploading(true);

    try {
      const formData = new FormData();
      formData.append("file", file);

      const response = await fetch(`${apiUrl}/api/Document/upload`, {
        method: "POST",
        body: formData,
      });

      const data = (await response.json()) as { message?: string; chunksCreated?: number };

      if (!response.ok) {
        throw new Error(data.message || "Upload failed");
      }

      const chunksCreated = typeof data.chunksCreated === "number" ? data.chunksCreated : 0;
      const successMessage = `${data.message || "Document uploaded"} (${chunksCreated} chunks)`;
      setUploadMessage(successMessage);
      setSelectedFile(null);
      onUploadSuccess?.(successMessage);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : "Upload failed";
      setUploadError(errorMessage);
      onUploadError?.(errorMessage);
    } finally {
      setIsUploading(false);
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
          <span className="drop-zone-subtitle">{selectedFile ? `${(selectedFile.size / 1024 / 1024).toFixed(2)} MB · ready to index` : "PDF, TXT, HTML, DOC, or DOCX"}</span>
        </div>
        <button className="upload-button" type="button" onClick={() => fileInputRef.current?.click()} disabled={isUploading}>
          {isUploading ? "Uploading" : "Choose file"}
        </button>
        <input
          ref={fileInputRef}
          className="file-input"
          type="file"
          accept=".pdf,.txt,.html,.doc,.docx,application/pdf,text/plain,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
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
