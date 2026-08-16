# Use Microsoft's official .NET runtime base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Use .NET SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project files first to restore dependencies
COPY ["src/Region42.ScoresStandings.Domain/Region42.ScoresStandings.Domain.csproj", "src/Region42.ScoresStandings.Domain/"]
COPY ["src/Region42.ScoresStandings.Application/Region42.ScoresStandings.Application.csproj", "src/Region42.ScoresStandings.Application/"]
COPY ["src/Region42.ScoresStandings.Web/Region42.ScoresStandings.Web.csproj", "src/Region42.ScoresStandings.Web/"]

# Restore packages
RUN dotnet restore "src/Region42.ScoresStandings.Web/Region42.ScoresStandings.Web.csproj"

# Copy the rest of the source code
COPY . .

# Build the Web project
WORKDIR "/src/src/Region42.ScoresStandings.Web"
RUN dotnet build "Region42.ScoresStandings.Web.csproj" -c Release -o /app/build

# Publish the Web project
FROM build AS publish
RUN dotnet publish "Region42.ScoresStandings.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage: build image with runtime and published application
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root user (built-in "app" user, standard for security)
USER app

ENTRYPOINT ["dotnet", "Region42.ScoresStandings.Web.dll"]
