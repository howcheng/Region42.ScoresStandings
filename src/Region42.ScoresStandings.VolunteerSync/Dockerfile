FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Region42.ScoresStandings.VolunteerSync/Region42.ScoresStandings.VolunteerSync.csproj src/Region42.ScoresStandings.VolunteerSync/
RUN dotnet restore src/Region42.ScoresStandings.VolunteerSync/Region42.ScoresStandings.VolunteerSync.csproj

COPY src/Region42.ScoresStandings.VolunteerSync/ src/Region42.ScoresStandings.VolunteerSync/
RUN dotnet publish src/Region42.ScoresStandings.VolunteerSync/Region42.ScoresStandings.VolunteerSync.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
ENV DOTNET_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Region42.ScoresStandings.VolunteerSync.dll"]
