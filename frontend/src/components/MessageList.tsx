import React, { useEffect, useRef } from "react";
import type { Message } from "../types/chat";
import { MessageItem } from "./MessageItem";

interface MessageListProps {
  messages: Message[];
  isLoading: boolean;
}

export const MessageList: React.FC<MessageListProps> = ({
  messages,
  isLoading,
}) => {
  const endOfMessagesRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endOfMessagesRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages, isLoading]);

  return (
    <div className="message-list">
      {messages.length === 0 ? (
        <div className="empty-chat">
          <div className="empty-chat-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"><path d="M21 11.5a8.5 8.5 0 0 1-8.5 8.5c-1.3 0-2.6-.3-3.7-.8L3 21l1.8-5.3A8.5 8.5 0 1 1 21 11.5Z" /><path d="M8.5 11.5h.01M12 11.5h.01M15.5 11.5h.01" /></svg>
          </div>
          <h3>Start a conversation</h3>
          <p>Ask a question, or upload a document to give the assistant more context.</p>
        </div>
      ) : (
        <>
          {messages.map((message, index) => (
            <MessageItem key={index} message={message} />
          ))}
          {isLoading && (
            <div className="typing-indicator" aria-label="Assistant is typing">
              <span />
              <span />
              <span />
            </div>
          )}
          <div ref={endOfMessagesRef} />
        </>
      )}
    </div>
  );
};
