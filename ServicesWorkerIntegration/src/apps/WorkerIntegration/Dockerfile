#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0.16-alpine3.17-amd64 AS base
WORKDIR /app
RUN adduser -u 5678 --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

FROM mcr.microsoft.com/dotnet/sdk:6.0.408-alpine3.17-amd64 AS build
WORKDIR "/src/WorkerIntegration"
COPY . .
RUN dotnet restore "WorkerIntegration.csproj"
RUN dotnet build "WorkerIntegration.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WorkerIntegration.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerIntegration.dll"]