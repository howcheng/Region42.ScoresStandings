# 🎯 Youth Soccer League Tracking Web Application (Region42)

Welcome to the **AYSO Region 42 Scores & Standings Tracking Web Application**. This system is built using modern **.NET 10** to manage and track seasons, divisions, teams, game schedules, scores, volunteer point contributions, and real-time division standings for a youth soccer league.

This repository serves as the single source of truth for the project. To assist both human developers and downstream AI assistants/agents in maintaining and extending this application, please refer to the comprehensive structural guides listed below.

---

## 🏗️ Technical Stack & Architecture

This application is built using a cleanly separated **4-Layer Onion Architecture** designed for high maintainability, testability, and decoupled data concerns.

### 1. Project Layering
*   **`Region42.ScoresStandings.Domain`** (`src/Region42.ScoresStandings.Domain`)
	*   *Role:* Core domain models, custom business entities, central enums, audit behaviors, and repository interfaces. Contains zero external dependencies.
*   **`Region42.ScoresStandings.Application`** (`src/Region42.ScoresStandings.Application`)
	*   *Role:* Domain services, business transaction boundaries, validation behavior, CSV parsers, standing calculations, and DTO mappings.
*   **`Region42.ScoresStandings.Infrastructure`** (`src/Region42.ScoresStandings.Infrastructure`)
	*   *Role:* JSON document persistence (local filesystem and Google Cloud Storage), repository implementations, and the in-memory competition data context.
*   **`Region42.ScoresStandings.Web`** (`src/Region42.ScoresStandings.Web`)
	*   *Role:* User interface built with **ASP.NET Core MVC** controllers/views. Houses OAuth pipeline configuration, middleware, and the application composition root.
*   **Test Projects** (`tests/`)
	*   Dedicated unit and integration tests covering the application, infrastructure, and web layers.

### 2. Tech Stack Detail
*   **Runtime:** .NET 10 (ASP.NET Core / C# 14)
*   **Data storage:** JSON files in Google Cloud Storage (`region42-storage` bucket); local filesystem mirror for development
*   **Authentication:** Google OAuth authentication
*   **Styling & UI:** Tailwind / Bootstrap, with responsive custom grids and tables optimized for mobile.

---

## 🐳 Hosting & CI/CD pipeline

*   **Version Control:** Hosted on GitHub (git branch `master`).
*   **CI/CD Workflows:** Automated builds, tests, and deployments via GitHub Actions (`.github/workflows/deploy.yml`).
*   **Containerization:** The `Region42.ScoresStandings.Web` project is Dockerized using a multi-stage `Dockerfile`.
*   **Production Hosting:** Google Cloud Run (Serverless Environment).
*   **Production Data:** Google Cloud Storage bucket `region42-storage` (JSON documents per season/division).

---

## 📚 Technical & Feature Documentation Index

Instead of scanning the codebase to reconstruct business logic, follow these specific sub-guides designed with full, rich details for both developers and AI agents:

### ⚙️ Environment, Storage & Secrets Setup
*   👉 **[Development Setup & Cloud Deployment Guide](docs/development-setup.md)**
	*   *Covers:* Local development with filesystem JSON storage, Google OAuth configuration, and Google Cloud Run deployment.
*   👉 **[JSON File Storage Guide](docs/json-storage.md)**
	*   *Covers:* Bucket layout, configuration, one-time PostgreSQL export, and concurrency behavior.

### ⚽ Feature & Business Domain Specifications
*   👉 **[CSV Import Feature Specs](docs/features/csv-import.md)**
	*   *Covers:* SportsConnect CSV structural schemas, rigid multi-stage pre-import validation (showing all issues at once), preview tables, and the replacement import logic with "ShortName" generation.
*   👉 **[Scores Entry Feature Specs](docs/features/scores-entry.md)**
	*   *Covers:* Score record models, cascading dropdown grids (Division → Round), double score validation models (both scores required), team scheduled-game conflict policies, and retroactive audits.
*   👉 **[Volunteer Points Feature Specs](docs/features/volunteer-entry.md)**
	*   *Covers:* Bulk point submission, zero-points support, team-by-round point allocations, and retroactive point-in-time standings calculations.
*   👉 **[Maintenance Feature Specs](docs/features/maintenance.md)**
	*   *Covers:* Intelligent automatic season generation (starting August 1), active state transitions, division preference cookieland (July 31 / Dec 31 lifecycle limits), and teams/divs CRUD rules with soft-delete safety bars.
*   👉 **[Standings Calculation & Playoff Specs](docs/features/standings-view.md)**
	*   *Covers:* Standard soccer points scoring matrices (3 pts Win, 1 pt Draw, 0 pts Loss), standard AYSO tie-breaker logic, standings adjustments for divisions with an odd number of teams, and play-offs slot designations.

---

*For detailed explanations of DB design structures, see the existing internal notes:*
*   **[Database Migrations Walkthrough](docs/DatabaseMigrations.md)** - Details on EF Core migrations.
*   **[PostgreSQL DateTime Handling](docs/PostgreSQLDateTimeHandling.md)** - Details on handling UTC dates inside Npgsql.
*   **[Domain Restricted Authentication](docs/DOMAIN-RESTRICTED-AUTH.md)** - Explains the Google OAuth dual-layer authentication setup.
