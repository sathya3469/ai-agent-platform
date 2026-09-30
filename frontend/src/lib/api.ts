export class ApiError extends Error {
  status: number;
  
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export const api = {
  async get<T>(path: string): Promise<T> {
    const response = await fetch(`${baseUrl}${path}`);

    if (!response.ok) {
      throw new ApiError(response.status, "Request failed");
    }

    return response.json() as Promise<T>;
  },
};