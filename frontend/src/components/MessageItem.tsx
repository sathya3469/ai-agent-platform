import React from "react";
import ReactMarkdown from "react-markdown";
import type { Message } from "../types/chat";

interface MessageItemProps {
  message: Message;
}

export const MessageItem: React.FC<MessageItemProps> = ({ message }) => {
  const isUser = message.role === "user";

  return (
    <div className={`message-row ${isUser ? "message-row-user" : "message-row-assistant"}`}>
      <div className={`message-bubble ${isUser ? "message-bubble-user" : "message-bubble-assistant"}`}>
        {isUser ? (
          <p>{message.content}</p>
        ) : (
          <div className="prose prose-sm max-w-none">
            <ReactMarkdown>{message.content}</ReactMarkdown>
          </div>
        )}
        <time dateTime={message.timestamp.toISOString()}>{message.timestamp.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}</time>
      </div>
    </div>
  );
};
