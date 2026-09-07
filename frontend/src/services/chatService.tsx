import type { Message } from "../types/chat";


type HistoryMessage = Omit<Message, "timestamp"> & {
  timestamp: string;
};

const API_BASE_URL =
  (import.meta.env as Record<string, string | undefined>).VITE_API_URL ||
  "https://localhost:7005";

export async function* streamMessage(
  message: string
): AsyncGenerator<string, void, unknown> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/Chat/chat`, {
      method: "POST",
      headers: {
        Accept: "application/json",
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
    let buffer = "";

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // Parse JSON tokens from the buffer
      const lines = buffer.split("\n");
      buffer = lines[lines.length - 1]; // Keep incomplete line in buffer

      for (let i = 0; i < lines.length - 1; i++) {
        const line = lines[i].trim();
        if (line) {
          try {
            const token = JSON.parse(line);
            yield token;
          } catch {
            // Ignore parse errors, continue
          }
        }
      }
    }

    // Process remaining buffer
    if (buffer.trim()) {
      try {
        const parsed: unknown = JSON.parse(buffer);
        if (Array.isArray(parsed)) {
          for (const token of parsed) {
            if (typeof token === "string") yield token;
          }
        } else if (typeof parsed === "string") {
          yield parsed;
        }
      } catch {
        // Ignore parse errors
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
    const response = await fetch(`${API_BASE_URL}/api/Chat/history`, {
      method: "DELETE",
    });

    if (!response.ok) {
      throw new Error(`Failed to clear history: ${response.status}`);
    }
  } catch (error) {
    console.error("Error clearing history:", error);
  }
}