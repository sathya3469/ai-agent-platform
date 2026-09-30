import { render, screen } from "@testing-library/react";
import { AgentThinking } from "../components/AgentThinking";
import type { AgentThought } from "../services/chatService";

const mockThoughts: AgentThought[] = [
  {
    step: 1,
    type: "THINK",
    content: "I need to search documents",
    timestamp: new Date().toISOString(),
  },
  {
    step: 2,
    type: "ACT",
    content: "Using tool: rag_search",
    timestamp: new Date().toISOString(),
  },
];

describe("AgentThinking Component", () => {
  test("renders thoughts", () => {
    render(<AgentThinking thoughts={mockThoughts} isProcessing={false} />);
    expect(screen.getByText("I need to search documents")).toBeInTheDocument();
    expect(screen.getByText("Using tool: rag_search")).toBeInTheDocument();
  });

  test("shows different styles for different types", () => {
    render(<AgentThinking thoughts={mockThoughts} isProcessing={false} />);
    const thinkStep = screen.getByText("I need to search documents").closest("div.agent-thought");
    const actStep = screen.getByText("Using tool: rag_search").closest("div.agent-thought");

    // The component styles each thought by its type; both classes are on the
    // thought element itself (not a child div).
    expect(thinkStep).toHaveClass("agent-thought", "bg-blue-100");
    expect(actStep).toHaveClass("agent-thought", "bg-purple-100");
  });

  test("shows loading indicator when processing", () => {
    render(<AgentThinking thoughts={[]} isProcessing={true} />);
    expect(screen.getByText(/agent thinking\.\.\./i)).toBeInTheDocument();
  });
});
