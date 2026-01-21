# Publishing Tracker

A full-stack application for managing book publications and sales tracking. Built with .NET Web API backend and dual frontend options (React and Angular).

**Tech Stack:**
- Backend: .NET 8 Web API
- Frontend (Primary): React with Vite
- Frontend (Alternative): Angular
- Database: SQL Server / Azure SQL
- Cloud: Azure

---
Tech Stack:

Backend: .NET 8 Web API
Frontend (Primary): React with Vite
Frontend (Alternative): Angular
Database: SQL Server / Azure SQL
Cloud: Azure
Project Structure
Publishing-Tracker/
├── apps/
│   ├── api/
│   │   └── PublishingTracker.Api/           # .NET Web API
│   │       ├── PublishingTracker.Api.csproj
│   │       ├── Program.cs
│   │       └── ...
│   ├── ui-react/                             # React UI (Vite)
│   │   ├── package.json
│   │   ├── src/
│   │   ├── vite.config.ts
│   │   └── ...
│   └── ui-angular/
│       └── publishing-tracker-ui-angular/    # Angular UI
│           ├── package.json
│           ├── src/
│           ├── angular.json
│           └── ...
├── .github/
│   └── workflows/                            # CI/CD pipelines
├── devops/                                   # Azure/Infrastructure
├── docs/                                     # Documentation
└── README.md
Prerequisites
All Projects
Git
Visual Studio Code or Visual Studio 2022
Backend (.NET API)
.NET 8 SDK
Frontend (React)
Node.js 20.x LTS
npm (comes with Node.js)
Frontend (Angular)
Node.js 20.x LTS
npm (comes with Node.js)
Angular CLI (installed via npm)
Setup Instructions
1. Clone the Repository
cd [C:\Github\PublishingTracker]
git clone https://github.com/Ian-J-Harkin/Publishing-Tracker.git
cd Publishing-Tracker
2. Backend Setup (.NET Web API)
Directory: [C:\Github\PublishingTracker\Publishing-Tracker\apps\api\PublishingTracker.Api]

Install & Build
cd apps/api/PublishingTracker.Api

# Restore dependencies
dotnet restore

# Build the project
dotnet build --configuration Release

# Run tests (if available)
dotnet test
Run the API
# From: apps/api/PublishingTracker.Api/PublishingTracker.Api/
dotnet run

# API will be available at: https://localhost:5001
Visual Studio Users
Open apps/api/PublishingTracker.Api/PublishingTracker.Api.sln in Visual Studio 2022
Build the solution (Ctrl+Shift+B)
Run (F5)
3. React Frontend Setup
Directory: [C:\Github\PublishingTracker\Publishing-Tracker\apps\ui-react]

Install Dependencies
cd apps/ui-react
npm install
Run Development Server
npm run dev

# UI will be available at: http://localhost:5173
Build for Production
npm run build

# Output in: apps/ui-react/dist/
Run Tests
npm test
4. Angular Frontend Setup
Directory: [C:\Github\PublishingTracker\Publishing-Tracker\apps\ui-angular\publishing-tracker-ui-angular]

Install Dependencies
cd apps/ui-angular/publishing-tracker-ui-angular
npm install
Run Development Server
npm start
# or
ng serve

# UI will be available at: http://localhost:4200
Build for Production
npm run build
# or
ng build --configuration production

# Output in: apps/ui-angular/publishing-tracker-ui-angular/dist/
Run Tests
npm test
# or
ng test
Running All Services at Once
Automated (Batch File - Windows)
From the root directory:

.\run_manual_tests.bat
This will launch:

Backend API in a new terminal
React UI in a new terminal
Manual (Run Each Separately)
Terminal 1 - Backend API:

cd apps/api/PublishingTracker.Api/PublishingTracker.Api
dotnet run
Terminal 2 - React UI:

cd apps/ui-react
npm run dev
Terminal 3 - Angular UI (Optional):

cd apps/ui-angular/publishing-tracker-ui-angular
npm start
CI/CD Configuration
GitHub Actions workflows are configured for:

.github/workflows/dotnet.yml - .NET API build & test
.github/workflows/cicd.yml - Frontend build & Azure deployment
Development Workflow
Create a feature branch:

git checkout -b feature/your-feature-name
Make your changes in the appropriate app directory

Commit with descriptive messages:

git add -A
git commit -m "feat: description of changes"
Push and create a pull request:

git push origin feature/your-feature-name
Troubleshooting
Backend Issues
Port already in use: Change port in Program.cs or kill the process using port 5001
Database connection: Verify connection string in appsettings.json
Dependencies: Run dotnet restore --force
React Issues
Dependencies not installing: Delete node_modules and package-lock.json, then npm install
Port 5173 in use: Kill the process or specify a different port: npm run dev -- --port 3000
Angular Issues
Dependencies not installing: Delete node_modules and package-lock.json, then npm install
Port 4200 in use: Specify a different port: ng serve --port 4300
Useful Commands Reference
Task	Command
Restore .NET deps	dotnet restore
Build .NET project	dotnet build
Run API	dotnet run
Run React	npm run dev
Build React	npm run build
Run Angular	ng serve
Build Angular	ng build
Run all services	.\run_manual_tests.bat
Documentation
Additional documentation available in docs/:

API Documentation
Architecture
Database Schema
Development Guide
Azure Deployment Guide
