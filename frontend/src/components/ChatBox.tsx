import React, { useState, useEffect } from "react";
import type { Message } from "../types/chat";
import { streamMessage, getHistory } from "../services/chatService";
import { MessageList } from "./MessageList";
import { InputBox } from "./InputBox";

export const ChatBox: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Load chat history on mount
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

    // Add user message
    const userMessage: Message = {
      role: "user",
      content: messageText,
      timestamp: new Date(),
    };
    setMessages((prev) => [...prev, userMessage]);

    try {
      // Stream response from API
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
      const errorMessage =
        err instanceof Error ? err.message : "An error occurred";
      setError(errorMessage);
      console.error("Error sending message:", err);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-blue-600 text-white p-4 shadow-md">
        <h1 className="text-2xl font-bold">AI Chat Assistant</h1>
        <p className="text-blue-100 text-sm">Powered by your .NET API</p>
      </div>

      {/* Error Banner */}
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3">
          <p className="font-semibold">Error</p>
          <p className="text-sm">{error}</p>
        </div>
      )}

      {/* Messages Area */}
      <MessageList messages={messages} isLoading={isLoading} />

      {/* Input Area */}
      <InputBox onSend={handleSendMessage} disabled={isLoading} />
    </div>
  );
};