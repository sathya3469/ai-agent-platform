import type { Message } from "../types/chat";


type HistoryMessage = Omit<Message, "timestamp"> & {
  timestamp: string;
};

// Both supported hosts proxy `/api` to the backend (the Vite dev server in
// vite.config.ts, Nginx in the container), so a relative base works everywhere
// with no configuration. Set VITE_API_URL only when the frontend is served from
// a host that has no such proxy in front of it.
const API_BASE_URL =
  (import.meta.env as Record<string, string | undefined>).VITE_API_URL ?? "";

// Matches the backend's per-read stall window. A cold Ollama model load can take
// several minutes, so this is deliberately generous rather than a snappy UX timeout.
const STREAM_TIMEOUT_MS = 6 * 60 * 1000;

// The agent runs a full ReAct loop (multiple model calls + tool execution), so it
// needs a longer bound than a single streamed answer.
const AGENT_TIMEOUT_MS = 10 * 60 * 1000;

interface DocumentFileInventoryResponse {
  fileCount: number;
  fileNames: string[];
}

export async function getDocumentFiles(): Promise<DocumentFileInventoryResponse> {
  const response = await fetch(`${API_BASE_URL}/api/Document/files`, {
    headers: {
      Accept: "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch documents: ${response.status}`);
  }

  return (await response.json()) as DocumentFileInventoryResponse;
}

export async function* streamMessage(
  message: string
): AsyncGenerator<string, void, unknown> {
  // The backend now surfaces failures as readable text rather than a bare 500, but
  // fetch has no default timeout — without an abort, a stalled backend hangs the UI
  // forever. Generous window: a cold model load can take minutes.
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), STREAM_TIMEOUT_MS);

  try {
    const response = await fetch(`${API_BASE_URL}/api/chat/stream`, {
      method: "POST",
      headers: {
        Accept: "text/plain",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ message, sessionId: "string" }),
      signal: controller.signal,
    });

    if (!response.ok) {
      // The controller returns 503 with a plain-text reason when the model is
      // unreachable, so prefer that over a bare status code.
      let reason = `API error: ${response.status}`;
      try {
        const body = await response.text();
        if (body) {
          reason = body;
        }
      } catch {
        /* keep the status-only fallback */
      }
      throw new Error(reason);
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
    if (error instanceof DOMException && error.name === "AbortError") {
      throw new Error(
        "The model is taking too long to respond. It may still be loading — please try again in a moment.",
        { cause: error }
      );
    }

    throw new Error(
      error instanceof Error ? error.message : "Failed to send message",
      { cause: error }
    );
  } finally {
    clearTimeout(timeout);
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

// ---------- Agent mode types + API ----------

export interface AgentThought {
  step: number;
  type: "THINK" | "ACT" | "OBSERVE" | "FINAL_ANSWER";
  content: string;
  timestamp: string;
}

export interface ToolCallRecord {
  tool: string;
  parameters: Record<string, unknown>;
  result?: unknown;
  error?: string;
  executedAt: string;
}

export interface AgentResponse {
  thoughts: AgentThought[];
  toolCalls: ToolCallRecord[];
  finalAnswer: string;
  totalSteps: number;
  executionTimeMs: number;
}

/**
 * Run a query through the orchestrator agent (non-streaming).
 * Returns the full agent trace: reasoning thoughts, tool calls, final answer.
 */
export async function runAgent(query: string): Promise<AgentResponse> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), AGENT_TIMEOUT_MS);

  try {
    const response = await fetch(`${API_BASE_URL}/api/agent/run`, {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ query, maxSteps: 10 }),
      signal: controller.signal,
    });

    if (!response.ok) {
      let reason = `Agent API error: ${response.status}`;
      try {
        const body = await response.text();
        if (body) {
          reason = body;
        }
      } catch {
        /* keep the status-only fallback */
      }
      throw new Error(reason);
    }

    return (await response.json()) as AgentResponse;
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw new Error(
        "The agent is taking too long to finish. It may still be loading the model — please try again.",
        { cause: error }
      );
    }

    throw error;
  } finally {
    clearTimeout(timeout);
  }
}

/** Fetch the most recent agent reasoning trace from the server. */
export async function getAgentThoughts(): Promise<AgentThought[]> {
  const response = await fetch(`${API_BASE_URL}/api/agent/thoughts`, {
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch agent thoughts: ${response.status}`);
  }

  return (await response.json()) as AgentThought[];
}