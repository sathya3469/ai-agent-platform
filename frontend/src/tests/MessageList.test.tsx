import { render, screen } from "@testing-library/react";
import { MessageList } from "../components/MessageList";
import type { Message } from "../types/chat";

// The component needs the full Message shape (timestamp is read by MessageItem), so build
// them here rather than using a partial object that would fail at render time.
const mockMessages: Message[] = [
  { role: "user", content: "Hello", timestamp: new Date("2026-01-01T00:00:00Z") },
  { role: "assistant", content: "Hi there!", timestamp: new Date("2026-01-01T00:00:01Z") },
];

describe("MessageList Component", () => {
  test("renders messages", () => {
    render(<MessageList messages={mockMessages} isLoading={false} />);
    expect(screen.getByText("Hello")).toBeInTheDocument();
    expect(screen.getByText("Hi there!")).toBeInTheDocument();
  });

  test("renders empty state when no messages", () => {
    render(<MessageList messages={[]} isLoading={false} />);
    expect(screen.getByText(/no messages|start a conversation/i)).toBeInTheDocument();
  });

  test("renders user and assistant messages differently", () => {
    render(<MessageList messages={mockMessages} isLoading={false} />);
    // MessageItem wraps each message in a row whose class distinguishes the speaker
    // (message-row-user / message-row-assistant).
    const userMessage = screen.getByText("Hello").closest(".message-row");
    const assistantMessage = screen.getByText("Hi there!").closest(".message-row");

    expect(userMessage).toHaveClass("message-row-user");
    expect(assistantMessage).toHaveClass("message-row-assistant");
  });
});
