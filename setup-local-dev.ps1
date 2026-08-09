# Setup Local Development Environment for Region42 Scores & Standings
# Run this script to quickly set up a local PostgreSQL database and configure user secrets

param(
	[Parameter(Mandatory=$false)]
	[ValidateSet("local", "cloudsql")]
	[string]$DatabaseMode = "local",

	[Parameter(Mandatory=$false)]
	[string]$LocalPassword = "LocalDevPassword123!",

	[Parameter(Mandatory=$false)]
	[string]$CloudSqlPassword,

	[Parameter(Mandatory=$false)]
	[string]$GoogleClientId,

	[Parameter(Mandatory=$false)]
	[string]$GoogleClientSecret
)

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Region42 Scores & Standings" -ForegroundColor Cyan
Write-Host "Local Development Setup" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

$ProjectPath = "Region42.ScoresStandings.Web"

# Function to check if Docker is running
function Test-DockerRunning {
	try {
		docker ps | Out-Null
		return $true
	}
	catch {
		return $false
	}
}

# Function to check if container exists
function Test-ContainerExists {
	param([string]$ContainerName)
	$exists = docker ps -a --filter "name=$ContainerName" --format "{{.Names}}" | Select-String -Pattern "^$ContainerName$"
	return $null -ne $exists
}

# Setup Local PostgreSQL
if ($DatabaseMode -eq "local") {
	Write-Host "[1/4] Setting up Local PostgreSQL Database..." -ForegroundColor Yellow

	if (-not (Test-DockerRunning)) {
		Write-Host "ERROR: Docker is not running. Please start Docker Desktop and try again." -ForegroundColor Red
		exit 1
	}

	$containerName = "region42-postgres"

	if (Test-ContainerExists $containerName) {
		Write-Host "Container '$containerName' already exists." -ForegroundColor Yellow
		$restart = Read-Host "Do you want to restart it? (y/N)"
		if ($restart -eq "y" -or $restart -eq "Y") {
			Write-Host "Restarting container..." -ForegroundColor Green
			docker restart $containerName
		}
	}
	else {
		Write-Host "Creating new PostgreSQL container..." -ForegroundColor Green
		docker run --name $containerName `
			-e POSTGRES_DB=region42 `
			-e POSTGRES_USER=postgres `
			-e POSTGRES_PASSWORD=$LocalPassword `
			-p 5432:5432 `
			-v region42_pgdata:/var/lib/postgresql/data `
			-d postgres:16

		Write-Host "Waiting for PostgreSQL to start..." -ForegroundColor Green
		Start-Sleep -Seconds 8
	}

	# Test connection
	$testResult = docker exec $containerName psql -U postgres -d region42 -c "SELECT 1;" 2>&1
	if ($LASTEXITCODE -eq 0) {
		Write-Host "✓ PostgreSQL is running and accessible" -ForegroundColor Green
	}
	else {
		Write-Host "⚠ Warning: Could not verify PostgreSQL connection" -ForegroundColor Yellow
	}

	$connectionString = "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=$LocalPassword"
}
elseif ($DatabaseMode -eq "cloudsql") {
	Write-Host "[1/4] Configuring Cloud SQL Connection..." -ForegroundColor Yellow

	if ([string]::IsNullOrEmpty($CloudSqlPassword)) {
		Write-Host "ERROR: -CloudSqlPassword is required when using -DatabaseMode cloudsql" -ForegroundColor Red
		Write-Host "Usage: .\setup-local-dev.ps1 -DatabaseMode cloudsql -CloudSqlPassword YOUR_PASSWORD" -ForegroundColor Yellow
		exit 1
	}

	Write-Host "Connection mode: Cloud SQL via Proxy (127.0.0.1:5432)" -ForegroundColor Cyan
	Write-Host "Make sure Cloud SQL Auth Proxy is running with:" -ForegroundColor Yellow
	Write-Host "  cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings" -ForegroundColor Cyan
	Write-Host ""

	$connectionString = "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=$CloudSqlPassword"
}

Write-Host ""

# Configure User Secrets
Write-Host "[2/4] Configuring User Secrets..." -ForegroundColor Yellow

Push-Location $ProjectPath

# Set connection string
Write-Host "Setting ConnectionString..." -ForegroundColor Green
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString

# Configure Google OAuth if provided
if (-not [string]::IsNullOrEmpty($GoogleClientId) -and -not [string]::IsNullOrEmpty($GoogleClientSecret)) {
	Write-Host "Setting Google OAuth credentials..." -ForegroundColor Green
	dotnet user-secrets set "Authentication:Google:ClientId" $GoogleClientId
	dotnet user-secrets set "Authentication:Google:ClientSecret" $GoogleClientSecret
	Write-Host "✓ OAuth configured" -ForegroundColor Green
}
else {
	Write-Host "⚠ OAuth credentials not provided. You'll need to set them manually:" -ForegroundColor Yellow
	Write-Host "  dotnet user-secrets set 'Authentication:Google:ClientId' 'YOUR_CLIENT_ID'" -ForegroundColor Cyan
	Write-Host "  dotnet user-secrets set 'Authentication:Google:ClientSecret' 'YOUR_CLIENT_SECRET'" -ForegroundColor Cyan
	Write-Host ""
	Write-Host "  Create credentials at: https://console.cloud.google.com/apis/credentials?project=ayso-region-42" -ForegroundColor Cyan
}

Pop-Location

Write-Host ""

# Display current configuration
Write-Host "[3/4] Current Configuration:" -ForegroundColor Yellow
Push-Location $ProjectPath
dotnet user-secrets list
Pop-Location

Write-Host ""

# Summary
Write-Host "[4/4] Setup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Configure Google OAuth (if not done above)" -ForegroundColor White
Write-Host "  2. Run migrations:" -ForegroundColor White
Write-Host "       cd $ProjectPath" -ForegroundColor Yellow
Write-Host "       dotnet ef migrations add InitialCreate" -ForegroundColor Yellow
Write-Host "       dotnet ef database update" -ForegroundColor Yellow
Write-Host "  3. Run the application:" -ForegroundColor White
Write-Host "       dotnet run" -ForegroundColor Yellow
Write-Host ""
Write-Host "Database Info:" -ForegroundColor Cyan
if ($DatabaseMode -eq "local") {
	Write-Host "  Type: Local PostgreSQL (Docker)" -ForegroundColor White
	Write-Host "  Container: region42-postgres" -ForegroundColor White
	Write-Host "  Host: localhost:5432" -ForegroundColor White
	Write-Host "  Database: region42" -ForegroundColor White
	Write-Host "  Username: postgres" -ForegroundColor White
	Write-Host ""
	Write-Host "Container Commands:" -ForegroundColor Cyan
	Write-Host "  Stop:    docker stop region42-postgres" -ForegroundColor Yellow
	Write-Host "  Start:   docker start region42-postgres" -ForegroundColor Yellow
	Write-Host "  Logs:    docker logs region42-postgres" -ForegroundColor Yellow
	Write-Host "  Remove:  docker rm -f region42-postgres" -ForegroundColor Yellow
}
else {
	Write-Host "  Type: Cloud SQL" -ForegroundColor White
	Write-Host "  Instance: ayso-region-42:us-west2:region-42-scores-standings" -ForegroundColor White
	Write-Host "  Connection: Via Cloud SQL Proxy (127.0.0.1:5432)" -ForegroundColor White
}
Write-Host ""
