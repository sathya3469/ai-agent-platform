import  { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { Message } from "../types/chat";
import {
  streamMessage,
  getHistory,
  runAgent,
  type AgentThought,
} from "../services/chatService";
import { MessageList } from "./MessageList";
import { InputBox } from "./InputBoxFixed";

interface ChatBoxProps {
  agentMode?: boolean;
}

export function ChatBox({ agentMode = false }: ChatBoxProps) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [agentThoughts, setAgentThoughts] = useState<AgentThought[]>([]);
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
       if (agentMode) {
      // Use agent endpoint (same API_BASE_URL + headers pattern as streamMessage)
      const data = await runAgent(messageText);
      setAgentThoughts(data.thoughts);
      // Display agent final answer as an assistant message
      setMessages((prev) => [
        ...prev,
        {
          role: "assistant",
          content: data.finalAnswer,
          timestamp: new Date(),
        },
      ]);
    }
    else{
      let assistantContent = "";
      let receivedAnyToken = false;
      const assistantMessage: Message = {
        role: "assistant",
        content: "",
        timestamp: new Date(),
      };

      for await (const token of streamMessage(messageText)) {
        receivedAnyToken = true;
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

      if (!receivedAnyToken && assistantContent.trim() === "") {
        throw new Error("The assistant did not return a response. Check that Ollama is running and the model is loaded.");
      }
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
    <div className="chat-panel">
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

      {agentMode && agentThoughts.length > 0 && (
        <section className="agent-thinking" aria-label="Agent reasoning">
          <h3 className="agent-thinking-title">Agent thinking</h3>
          {agentThoughts.map((thought, index) => {
            const label =
              thought.type === "THINK"
                ? "Thinking"
                : thought.type === "ACT"
                  ? "Action"
                  : thought.type === "OBSERVE"
                    ? "Tool result"
                    : "Final answer";

            return (
              <div key={`${thought.step}-${index}`} className={`agent-thought agent-thought-${thought.type.toLowerCase()}`}>
                <strong className="agent-thought-label">{label}</strong>
                <div className="agent-thought-content markdown">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>{thought.content}</ReactMarkdown>
                </div>
              </div>
            );
          })}
        </section>
      )}

      <div className="messages-area" aria-busy={isLoading}>
        <MessageList messages={messages} isLoading={isLoading} />
      </div>

      <div className="composer-area">
        <InputBox onSend={handleSendMessage} disabled={isLoading} />
      </div>

      <p className="chat-mode">
        {agentMode
          ? "Agent Mode: Shows reasoning steps"
          : "Chat Mode: Direct responses"}
      </p>
    </div>
  );
}