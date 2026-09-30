import React from "react";

interface ToolCall {
  tool: string;
  parameters: Record<string, unknown>;
  result?: unknown;
  error?: string;
  executedAt: string;
}

interface ToolCallProps {
  call: ToolCall;
}

export const ToolCallComponent: React.FC<ToolCallProps> = ({ call }) => {
  const parametersText: string =
    JSON.stringify(call.parameters, null, 2) ?? "{}";

  return (
    <div className="bg-purple-50 border border-purple-300 rounded p-3 text-sm">
      <div className="font-semibold text-purple-900">
        🔧 Tool: {call.tool}
      </div>

      <div className="mt-2">
        <div className="text-xs font-mono bg-white p-2 rounded">
          {parametersText}
        </div>
      </div>

      {call.error && (
        <div className="mt-2 bg-red-100 border border-red-300 p-2 rounded text-red-800">
          ✗ Error: {call.error}
        </div>
      )}

      {call.result !== undefined && (
        <div className="mt-2 bg-green-100 border border-green-300 p-2 rounded text-green-800">
          ✓ Result: {String(call.result).substring(0, 100)}...
        </div>
      )}
    </div>
  );
};