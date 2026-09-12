FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json Sentinel.slnx ./
COPY src ./src
COPY tools ./tools
ARG PROJECT
RUN dotnet publish $PROJECT -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ARG DLL
ENV ENTRYPOINT_DLL=$DLL
ENTRYPOINT ["sh", "-c", "dotnet $ENTRYPOINT_DLL"]
