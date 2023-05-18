#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0.16-alpine3.17-amd64 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0.408-alpine3.17-amd64 AS build
WORKDIR "/src/SampleWebApp"
COPY . .
RUN dotnet restore "SampleWebApp.csproj"
RUN dotnet build "SampleWebApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SampleWebApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SampleWebApp.dll"]