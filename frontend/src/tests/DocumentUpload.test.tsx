import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DocumentUpload } from "../components/DocumentUpload";

describe("DocumentUpload Component", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  test("renders upload button", () => {
    render(<DocumentUpload />);
    const button = screen.getByRole("button", { name: /choose file/i });
    expect(button).toBeInTheDocument();
  });

  test("accepts file input", () => {
    render(<DocumentUpload />);
    const input = screen.getByLabelText(/choose a document to upload/i);
    expect(input).toHaveAttribute("type", "file");
  });

  test("shows success message on upload", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ message: "Document uploaded successfully", chunksCreated: 1 }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      })
    );

    const user = userEvent.setup();
    render(<DocumentUpload />);
    const input = screen.getByLabelText(/choose a document to upload/i);
    const file = new File(["content"], "test.txt", { type: "text/plain" });

    await user.upload(input, file);

    await waitFor(() => {
      expect(screen.getByText(/uploaded successfully/i)).toBeInTheDocument();
    });
  });

  test("shows error message on failed upload", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ message: "Upload failed" }), {
        status: 500,
        headers: { "Content-Type": "application/json" },
      })
    );

    const user = userEvent.setup();
    render(<DocumentUpload />);
    const input = screen.getByLabelText(/choose a document to upload/i);
    const file = new File(["content"], "test.txt", { type: "text/plain" });

    await user.upload(input, file);

    await waitFor(() => {
      expect(screen.getByText(/upload failed/i)).toBeInTheDocument();
    });
  });
});
