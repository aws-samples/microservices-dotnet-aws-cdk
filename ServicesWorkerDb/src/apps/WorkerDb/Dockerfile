
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR "/src/WorkerDb"
COPY . .
RUN dotnet restore "WorkerDb.csproj"
RUN dotnet build "WorkerDb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WorkerDb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerDb.dll"]