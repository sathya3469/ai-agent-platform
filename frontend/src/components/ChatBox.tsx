import React, { useEffect, useState } from "react";
import type { Message } from "../types/chat";
import { streamMessage, getHistory } from "../services/chatService";
import { MessageList } from "./MessageList";
import { InputBox } from "./InputBoxFixed";

interface ChatBoxProps {
  className?: string;
}

export const ChatBox: React.FC<ChatBoxProps> = ({ className = "" }) => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadHistory = async () => {
      try {
        const history = await getHistory();
        setMessages(history);
      } catch (err) {
        console.error("Failed to load history:", err);
      }
    };

    loadHistory();
  }, []);

  const handleSendMessage = async (messageText: string) => {
    setError(null);
    setIsLoading(true);

    const userMessage: Message = {
      role: "user",
      content: messageText,
      timestamp: new Date(),
    };
    setMessages((prev) => [...prev, userMessage]);

    try {
      let assistantContent = "";
      const assistantMessage: Message = {
        role: "assistant",
        content: "",
        timestamp: new Date(),
      };

      for await (const token of streamMessage(messageText)) {
        assistantContent += token;
        assistantMessage.content = assistantContent;
        setMessages((prev) => {
          const updated = [...prev];
          if (updated[updated.length - 1].role === "assistant") {
            updated[updated.length - 1] = assistantMessage;
          } else {
            updated.push(assistantMessage);
          }
          return updated;
        });
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      console.error("Error sending message:", err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className={`chat-panel ${className}`}>
      <div className="chat-header">
        <div className="chat-header-content">
          <div className="chat-title-row">
            <div className="chat-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 11.5a8.5 8.5 0 0 1-8.5 8.5c-1.3 0-2.6-.3-3.7-.8L3 21l1.8-5.3A8.5 8.5 0 1 1 21 11.5Z" />
                <path d="M8.5 11.5h.01M12 11.5h.01M15.5 11.5h.01" />
              </svg>
            </div>
            <div className="chat-header-text">
              <h2>Agent conversation</h2>
              <p>Ask a question or upload context for a more relevant answer.</p>
            </div>
          </div>
          <div className="status-pill">
            <span className="status-dot" aria-hidden="true" />
            Online
          </div>
        </div>
        <div className="message-count" aria-label={`${messages.length} messages`}>
          <strong>{messages.length}</strong>
          <span>messages</span>
        </div>
      </div>

      {error && (
        <div className="chat-error" role="alert">
          <div className="chat-error-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="9" /><path d="M12 8v5M12 16.5h.01" /></svg>
          </div>
          <div>
            <strong>Conversation unavailable</strong>
            <span>{error}</span>
          </div>
          <button className="error-dismiss" type="button" onClick={() => setError(null)} aria-label="Dismiss error">×</button>
        </div>
      )}

      <div className="messages-area" aria-busy={isLoading}>
        <MessageList messages={messages} isLoading={isLoading} />
      </div>

      <div className="composer-area">
        <InputBox onSend={handleSendMessage} disabled={isLoading} />
      </div>
    </div>
  );
};