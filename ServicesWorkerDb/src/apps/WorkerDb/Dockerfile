
FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal AS base
WORKDIR /app
RUN apt-get update -y && apt-get upgrade -y libc-bin libc6 libgcrypt20 perl-base
RUN addgroup --system --gid 1000 app_group \
    && adduser --system --uid 1000 --ingroup app_group --shell /bin/sh app_user

FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS build
WORKDIR "/src/WorkerDb"
COPY . .
RUN dotnet restore "WorkerDb.csproj"
RUN dotnet build "WorkerDb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WorkerDb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
RUN chown 1000:app_group /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerDb.dll"]
# Tell docker that all future commands should run as the appuser user, must use the user number
USER 1000