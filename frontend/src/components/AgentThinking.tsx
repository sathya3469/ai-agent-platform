import React from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { AgentThought } from "../services/chatService";

interface AgentThinkingProps {
  thoughts: AgentThought[];
  isProcessing: boolean;
}

const TYPE_LABELS: Record<AgentThought["type"], string> = {
  THINK: "Thinking",
  ACT: "Action",
  OBSERVE: "Tool result",
  FINAL_ANSWER: "Final answer",
};

const TYPE_STYLES: Record<AgentThought["type"], string> = {
  THINK: "bg-blue-100",
  ACT: "bg-purple-100",
  OBSERVE: "bg-amber-100",
  FINAL_ANSWER: "bg-green-100",
};

/**
 * Renders the agent's ReAct reasoning trace. ChatBox renders the same markup inline; this
 * component exists so the trace can be shown standalone (and unit tested).
 */
export const AgentThinking: React.FC<AgentThinkingProps> = ({
  thoughts,
  isProcessing,
}) => {
  return (
    <section className="agent-thinking" aria-label="Agent reasoning">
      <h3 className="agent-thinking-title">Agent thinking</h3>

      {thoughts.map((thought, index) => (
        <div
          key={`${thought.step}-${index}`}
          className={`agent-thought ${TYPE_STYLES[thought.type]}`}
        >
          <strong className="agent-thought-label">
            {TYPE_LABELS[thought.type]}
          </strong>
          <div className="agent-thought-content markdown">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>
              {thought.content}
            </ReactMarkdown>
          </div>
        </div>
      ))}

      {isProcessing && (
        <div className="agent-processing" role="status">
          <span className="agent-spinner" aria-hidden="true" />
          Agent thinking...
        </div>
      )}
    </section>
  );
};
