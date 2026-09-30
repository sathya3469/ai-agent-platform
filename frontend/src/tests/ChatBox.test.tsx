import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatBox } from "../components/ChatBox";
import { streamMessage } from "../services/chatService";

// vi.mock factories are hoisted above every import, so anything they close over must be
// created through vi.hoisted too. This holder is set per test, before the component sends.
const { streamTokens } = vi.hoisted(() => ({ streamTokens: { current: [] as string[] } }));

// ChatBox talks to the backend on mount (getHistory) and on send (streamMessage). Neither
// is available in a unit test, so stub the service module.
vi.mock("../services/chatService", () => ({
  getHistory: vi.fn().mockResolvedValue([]),
  runAgent: vi.fn(),
  // An async generator the component consumes token-by-token; it reads the holder at call
  // time so each test can populate it before rendering.
  streamMessage: vi.fn(async function* () {
    for (const token of streamTokens.current) {
      yield token;
    }
  }),
}));

describe("ChatBox Component", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    streamTokens.current = [];
  });

  test("renders chat input", () => {
    render(<ChatBox />);
    const input = screen.getByPlaceholderText(/ask me anything/i);
    expect(input).toBeInTheDocument();
  });

  test("renders send button", () => {
    render(<ChatBox />);
    const button = screen.getByRole("button", { name: /send message/i });
    expect(button).toBeInTheDocument();
  });

  test("sends message on button click", async () => {
    streamTokens.current = ["Hello", " back"];
    const user = userEvent.setup();
    render(<ChatBox />);
    const input = screen.getByPlaceholderText(/ask me anything/i);
    const button = screen.getByRole("button", { name: /send message/i });

    await user.type(input, "Hello");
    await user.click(button);

    // The user's message is echoed immediately.
    await waitFor(() => {
      expect(screen.getByText("Hello")).toBeInTheDocument();
    });
    // And the streamed reply arrives once the async generator is consumed.
    await waitFor(() => {
      expect(streamMessage).toHaveBeenCalledWith("Hello");
    });
  });

  test("clears input after sending", async () => {
    streamTokens.current = ["Hi"];
    const user = userEvent.setup();
    render(<ChatBox />);
    const input = screen.getByPlaceholderText(/ask me anything/i);
    const button = screen.getByRole("button", { name: /send message/i });

    await user.type(input, "Test");
    await user.click(button);

    await waitFor(() => {
      expect(input).toHaveValue("");
    });
  });

  test("disables send button while loading", async () => {
    // Never resolve the stream, so the component stays in its loading state.
    streamTokens.current = [];
    const user = userEvent.setup();
    render(<ChatBox />);
    const input = screen.getByPlaceholderText(/ask me anything/i);
    const button = screen.getByRole("button", { name: /send message/i });

    await user.type(input, "Test");
    await user.click(button);

    await waitFor(() => {
      expect(button).toBeDisabled();
    });
  });
});
