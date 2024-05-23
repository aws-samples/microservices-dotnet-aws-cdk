# Base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR "/src/WorkerIntegration"
COPY . .
RUN dotnet restore "WorkerIntegration.csproj"
RUN dotnet build "WorkerIntegration.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "WorkerIntegration.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerIntegration.dll"]
