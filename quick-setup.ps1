# Quick Setup - Local PostgreSQL for Development
# This script will set up everything you need for local development

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Region42 Scores & Standings" -ForegroundColor Cyan
Write-Host "Local PostgreSQL Setup" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$ContainerName = "region42-postgres"
$DbName = "region42"
$DbUser = "postgres"
$DbPassword = "LocalDevPassword123!"
$Port = 5432

# Step 1: Check Docker
Write-Host "[1/5] Checking Docker..." -ForegroundColor Yellow
try {
	docker ps | Out-Null
	Write-Host "✓ Docker is running" -ForegroundColor Green
}
catch {
	Write-Host "✗ Docker is not running" -ForegroundColor Red
	Write-Host ""
	Write-Host "Please start Docker Desktop and run this script again." -ForegroundColor Yellow
	Write-Host "Press any key to exit..."
	$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
	exit 1
}
Write-Host ""

# Step 2: Check if container exists
Write-Host "[2/5] Checking for existing PostgreSQL container..." -ForegroundColor Yellow
$existingContainer = docker ps -a --filter "name=$ContainerName" --format "{{.Names}}" 2>$null

if ($existingContainer -eq $ContainerName) {
	$status = docker ps --filter "name=$ContainerName" --format "{{.Status}}" 2>$null
	if ($status) {
		Write-Host "✓ Container '$ContainerName' is already running" -ForegroundColor Green
	}
	else {
		Write-Host "Container '$ContainerName' exists but is stopped. Starting..." -ForegroundColor Yellow
		docker start $ContainerName
		Write-Host "✓ Container started" -ForegroundColor Green
	}
}
else {
	Write-Host "Creating new PostgreSQL container..." -ForegroundColor Yellow
	docker run --name $ContainerName `
		-e POSTGRES_DB=$DbName `
		-e POSTGRES_USER=$DbUser `
		-e POSTGRES_PASSWORD=$DbPassword `
		-p ${Port}:5432 `
		-v region42_pgdata:/var/lib/postgresql/data `
		-d postgres:16

	if ($LASTEXITCODE -eq 0) {
		Write-Host "✓ Container created successfully" -ForegroundColor Green
		Write-Host "Waiting 8 seconds for PostgreSQL to initialize..." -ForegroundColor Yellow
		Start-Sleep -Seconds 8
	}
	else {
		Write-Host "✗ Failed to create container" -ForegroundColor Red
		exit 1
	}
}
Write-Host ""

# Step 3: Test database connection
Write-Host "[3/5] Testing database connection..." -ForegroundColor Yellow
$testResult = docker exec $ContainerName psql -U $DbUser -d $DbName -c "SELECT version();" 2>&1
if ($LASTEXITCODE -eq 0) {
	Write-Host "✓ Database is accessible" -ForegroundColor Green
	$version = ($testResult | Select-String "PostgreSQL").ToString().Trim()
	Write-Host "  $version" -ForegroundColor Cyan
}
else {
	Write-Host "⚠ Warning: Could not verify connection (container may still be starting)" -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Configure User Secrets
Write-Host "[4/5] Configuring User Secrets..." -ForegroundColor Yellow
$connectionString = "Host=localhost;Port=$Port;Database=$DbName;Username=$DbUser;Password=$DbPassword"

Push-Location Region42.ScoresStandings.Web

# Check if user secrets is initialized
$userSecretsId = dotnet user-secrets list 2>&1 | Select-String "UserSecretsId"
if (-not $userSecretsId) {
	Write-Host "Initializing user secrets..." -ForegroundColor Yellow
	dotnet user-secrets init | Out-Null
}

# Set connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString | Out-Null
Write-Host "✓ Connection string configured" -ForegroundColor Green

Pop-Location
Write-Host ""

# Step 5: Display configuration
Write-Host "[5/5] Setup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Database Information" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  Container:  $ContainerName" -ForegroundColor White
Write-Host "  Host:       localhost:$Port" -ForegroundColor White
Write-Host "  Database:   $DbName" -ForegroundColor White
Write-Host "  Username:   $DbUser" -ForegroundColor White
Write-Host "  Password:   $DbPassword" -ForegroundColor White
Write-Host ""

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "User Secrets Status" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Push-Location Region42.ScoresStandings.Web
Write-Host ""
dotnet user-secrets list
Write-Host ""
Pop-Location

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Next Steps" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Configure Google OAuth (required):" -ForegroundColor White
Write-Host "   • Go to: https://console.cloud.google.com/apis/credentials?project=ayso-region-42" -ForegroundColor Yellow
Write-Host "   • Create OAuth 2.0 Client ID (Web Application)" -ForegroundColor Yellow
Write-Host "   • Add redirect URIs:" -ForegroundColor Yellow
Write-Host "     - https://localhost:5001/signin-google" -ForegroundColor Cyan
Write-Host "     - http://localhost:5000/signin-google" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Then run:" -ForegroundColor White
Write-Host "   cd Region42.ScoresStandings.Web" -ForegroundColor Yellow
Write-Host "   dotnet user-secrets set `"Authentication:Google:ClientId`" `"YOUR_CLIENT_ID`"" -ForegroundColor Yellow
Write-Host "   dotnet user-secrets set `"Authentication:Google:ClientSecret`" `"YOUR_SECRET`"" -ForegroundColor Yellow
Write-Host ""
Write-Host "2. Create database migrations:" -ForegroundColor White
Write-Host "   cd Region42.ScoresStandings.Web" -ForegroundColor Yellow
Write-Host "   dotnet ef migrations add InitialCreate" -ForegroundColor Yellow
Write-Host "   dotnet ef database update" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Run the application:" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor Yellow
Write-Host ""

Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "Useful Docker Commands" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "  View logs:       docker logs $ContainerName" -ForegroundColor Yellow
Write-Host "  Stop container:  docker stop $ContainerName" -ForegroundColor Yellow
Write-Host "  Start container: docker start $ContainerName" -ForegroundColor Yellow
Write-Host "  Remove + data:   docker rm -f $ContainerName; docker volume rm region42_pgdata" -ForegroundColor Yellow
Write-Host "  Connect to DB:   docker exec -it $ContainerName psql -U $DbUser -d $DbName" -ForegroundColor Yellow
Write-Host ""

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
