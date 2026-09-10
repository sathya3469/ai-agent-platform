import type { Message } from "../types/chat";


type HistoryMessage = Omit<Message, "timestamp"> & {
  timestamp: string;
};

const API_BASE_URL =
  (import.meta.env as Record<string, string | undefined>).VITE_API_URL ||
  "http://localhost:5243";

export async function* streamMessage(
  message: string
): AsyncGenerator<string, void, unknown> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/chat/stream`, {
      method: "POST",
      headers: {
        Accept: "text/plain",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ message, sessionId: "string" }),
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status}`);
    }

    if (!response.body) {
      throw new Error("Response body is empty");
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const text = decoder.decode(value, { stream: true });
      if (text) {
        yield text;
      }
    }
  } catch (error) {
    throw new Error(
      error instanceof Error ? error.message : "Failed to send message",
      { cause: error }
    );
  }
}

export async function getHistory(): Promise<Message[]> {
  const response = await fetch(`${API_BASE_URL}/api/Chat/history`, {
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch history: ${response.status}`);
  }

  const data = (await response.json()) as HistoryMessage[];

  return data.map((msg) => ({
    ...msg,
    timestamp: new Date(msg.timestamp),
  }));
}

export async function clearHistory(): Promise<void> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/chat/history`, {
      method: "DELETE",
    });

    if (!response.ok) {
      throw new Error(`Failed to clear history: ${response.status}`);
    }
  } catch (error) {
    console.error("Error clearing history:", error);
  }
}