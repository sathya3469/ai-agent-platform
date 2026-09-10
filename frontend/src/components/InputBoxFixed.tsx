import React, { useState } from "react";

interface InputBoxProps {
  onSend: (message: string) => void;
  disabled: boolean;
}

export const InputBox: React.FC<InputBoxProps> = ({ onSend, disabled }) => {
  const [input, setInput] = useState("");

  const handleSend = () => {
    if (input.trim()) {
      onSend(input);
      setInput("");
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const isButtonDisabled = disabled || !input.trim();

  return (
    <div className="input-box">
      <div className="input-meta">
        <div className="flex">
          <span className="flex items-center gap-1">
            <svg className="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
              <path d="M5.5 13a3.5 3.5 0 01-.369-6.98 4 4 0 117.753-1.3A4.5 4.5 0 1113.5 13H11V9.413l1.293 1.293a1 1 0 001.414-1.414l-3-3a1 1 0 001.414 0l-3 3a1 1 0 001.414 1.414L9 9.414V13H5.5z" />
            </svg>
            Shift+Enter for new line
          </span>
        </div>
        <span className={`${input.length > 1000 ? 'text-red-500' : 'text-slate-400'} font-medium`}>
          {input.length} / 2000
        </span>
      </div>

      <div className="input-area">
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value.slice(0, 2000))}
          onKeyPress={handleKeyPress}
          placeholder="Ask me anything... I'll search your uploaded documents and provide context-aware responses."
          disabled={disabled}
          rows={1}
        />

        <button
          className={`send-button ${isButtonDisabled ? '' : ''}`}
          onClick={handleSend}
          disabled={isButtonDisabled}
          title={isButtonDisabled ? 'Type a message to send' : 'Send message (Enter)'}
        >
          <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 20 20">
            <path d="M10.894 2.553a1 1 0 00-1.788 0l-7 14a1 1 0 001.169 1.409l5.951-2.976 5.951 2.976a1 1 0 001.169-1.409l-7-14z" />
          </svg>
        </button>
      </div>

      {!input.trim() && (
        <div className="tips">
          <svg className="w-4 h-4 flex-shrink-0 mt-0.5" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 5v8a2 2 0 01-2 2h-5l-5 4v-4H4a2 2 0 012-2V5a2 2 0 012 2h12a2 2 0 012 2zm-11-1a1 1 0 11-2 0 1 1 0 012 0zM8 9a1 1 0 100-2 1 1 0 000 2zm1 4a1 1 0 11-2 0 1 1 0 012 0z" clipRule="evenodd" />
          </svg>
          <span>💡 <span className="font-medium">Tip:</span> Upload documents to get more relevant and context-aware responses!</span>
        </div>
      )}
    </div>
  );
};