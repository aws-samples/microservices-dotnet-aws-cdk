# Base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN addgroup --system --gid 1000 app_group \
    && adduser --system --uid 1000 --ingroup app_group --shell /bin/sh app_user

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR "/src/WorkerDb"
COPY . .
RUN dotnet restore "WorkerDb.csproj"
RUN dotnet build "WorkerDb.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "WorkerDb.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
RUN chown 1000:app_group /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerDb.dll"]
# Tell docker that all future commands should run as the appuser user, must use the user number
USER 1000