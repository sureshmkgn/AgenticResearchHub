# Stage 1: Build & Publish with .NET 9 SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY AgenticResearchHub.sln .
COPY src/AgenticResearchHub.Core/AgenticResearchHub.Core.csproj src/AgenticResearchHub.Core/
COPY src/AgenticResearchHub.Infrastructure/AgenticResearchHub.Infrastructure.csproj src/AgenticResearchHub.Infrastructure/
COPY src/AgenticResearchHub.Agents/AgenticResearchHub.Agents.csproj src/AgenticResearchHub.Agents/
COPY src/AgenticResearchHub.Web/AgenticResearchHub.Web.csproj src/AgenticResearchHub.Web/
COPY tests/AgenticResearchHub.Tests/AgenticResearchHub.Tests.csproj tests/AgenticResearchHub.Tests/

# Restore dependencies
RUN dotnet restore AgenticResearchHub.sln

# Copy the entire source tree
COPY . .

# Run Tests during build
RUN dotnet test tests/AgenticResearchHub.Tests/AgenticResearchHub.Tests.csproj --configuration Release --no-restore --verbosity normal

# Publish the Web Application
WORKDIR /src/src/AgenticResearchHub.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime Image with ASP.NET Core 9
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Security: Non-root user
USER app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AgenticResearchHub.Web.dll"]
