# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

WORKDIR /source

# Copy project files
COPY ["AiAgentPlatform.Api/AiAgentPlatform.Api.csproj", "AiAgentPlatform.Api/"]

# Restore dependencies
RUN dotnet restore "AiAgentPlatform.Api/AiAgentPlatform.Api.csproj"

# Copy all code
COPY . .

# Build
RUN dotnet build "AiAgentPlatform.Api/AiAgentPlatform.Api.csproj" -c Release -o /app/build

# Publish
RUN dotnet publish "AiAgentPlatform.Api/AiAgentPlatform.Api.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy from builder
COPY --from=builder /app/publish .

# Expose ports
EXPOSE 5000
EXPOSE 7005

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/api/Chat/history || exit 1

# Entry point
ENTRYPOINT ["dotnet", "AiAgentPlatform.Api.dll"]