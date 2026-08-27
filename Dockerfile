FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RotationDating.Web.csproj", "./"]
RUN dotnet restore "RotationDating.Web.csproj"
COPY . .
RUN dotnet publish "RotationDating.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_HOSTBUILDER_RELOADCONFIGONCHANGE=false
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENV DOTNET_gcServer=0
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "RotationDating.Web.dll"]
