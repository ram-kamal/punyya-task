# PHF Work Management System

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](#)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](#)
[![Next.js 15](https://img.shields.io/badge/Next.js-15-black.svg)](#)
[![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16%2B-blue.svg)](#)
[![WCAG 2.1 AA](https://img.shields.io/badge/Accessibility-WCAG%202.1%20AA-success.svg)](#)

An enterprise-grade workforce telemetry, shift attendance, and deliverable-linked task management platform engineered for **Punnya Heart Foundation (PHF)**, built according to **`punnya-combined-spec.pptx`** and the Phase 1 Technical Specification.

---

## 1. Architectural Pillars (from `punnya-combined-spec.pptx`)

```
+-----------------------------------------------------------------------------------+
|                        PHF WORK MANAGEMENT SYSTEM                                |
+-----------------------------------------------------------------------------------+
|  1. Event-Driven Telemetry  |  Win32 SetWinEventHook (0x0003), CPU near 0%        |
|  2. Deliverable Gate        |  Task completion strictly requires proof of output  |
|  3. Tamper-Lock SQLite      |  HMAC-SHA256 offline buffer blocks client tampering |
|  4. Assistive Design System |  Full NVDA/Narrator UI Automation & WCAG 2.1 AA     |
|  5. Live Oversight Console  |  SignalR workstation streaming & 1-click CSV export |
+-----------------------------------------------------------------------------------+
```

1. **Native Windows Client (Near-0% CPU):**
   - Subscribes to `user32.dll` via `SetWinEventHook` listening exclusively to `EVENT_SYSTEM_FOREGROUND = 0x0003`.
   - Fires *only* when the user switches applications, eliminating polling loops and maintaining < 1% CPU utilization throughout an 8-hour shift.
   - Captures application process name, window title, duration, and user idle duration via `GetLastInputInfo` (> 300s threshold).
2. **Deliverable-Linked Completion (Accountability Gate):**
   - A task cannot be transitioned to `COMPLETED` without an attached deliverable (file or external URL) as proof of work.
   - The "Mark Completed" button remains disabled until valid output is attached.
   - Managers can immediately inspect and download deliverables directly from the Web Console.
3. **Tamper-Lock Offline Buffer:**
   - Client queues telemetry in a local SQLite database (`telemetry_offline.db`) during network drops.
   - Each row is cryptographically signed with an HMAC-SHA256 checksum, preventing employee-side modifications.
   - Automatically flushes to upstream `POST /api/v1/telemetry/batch` once connectivity returns.
4. **Built-in Accessibility & Assistive Design:**
   - Full screen-reader support: Every interactive control is mapped with `AutomationProperties.Name` and `AutomationProperties.HelpText` for Windows Narrator and NVDA.
   - 100% reachable by keyboard alone (logical `TabIndex` order).
   - High-contrast color palette compliant with WCAG 2.1 AA.
5. **Admin Console & One-Click Timesheet Export:**
   - Real-time fleet overview: Active Stations, current active app, state (`ACTIVE`, `IDLE`, `OFFLINE`).
   - Live streaming workstation event feed powered by SignalR.
   - Instant task dispatcher.
   - One-click CSV export for payroll without touching raw logs.

---

## 2. Project Structure

```
punnya-task-management-system/
├── backend/
│   ├── src/
│   │   ├── PHF.Core/               # Domain entities, DTOs, Enums, Service interfaces
│   │   ├── PHF.Infrastructure/     # EF Core, PostgreSQL/SQLite contexts, AuthService, TaskService,
│   │   │                           # ShiftService, TelemetryService, TimesheetService, Storage
│   │   ├── PHF.Realtime/           # SignalR WorkstationHub & SignalRRealtimeNotifier
│   │   └── PHF.Api/                # Controllers (Auth, Tasks, Telemetry, Shifts, Timesheet, Users), Swagger
│   └── tests/
│       └── PHF.Api.Tests/          # Unit & integration tests (Deliverable Gate, CSV Export, Auth)
├── client/
│   ├── src/
│   │   ├── PHF.Client.Telemetry/   # Win32 SetWinEventHook, IdleTracker, SessionTracker
│   │   ├── PHF.Client.Core/        # TamperProofDatabase (SQLite), ApiClient, RealtimeClient, SyncWorker
│   │   └── PHF.Client.Wpf/         # Accessible Desktop UI (Punnya Task Desk)
├── web/                            # Next.js 15 + Tailwind CSS Manager Web Portal
│   ├── src/
│   │   ├── app/                    # App Router, Live Oversight, Task Kanban, Payroll Timesheet
│   │   └── lib/                    # API & SignalR clients, types
├── database/
│   └── migrations/                 # 001_initial_schema.sql (PostgreSQL 16+ DDL & seed data)
├── docker/
│   ├── docker-compose.yml          # PostgreSQL 16, MinIO, Backend API, Web Portal
│   ├── Dockerfile.backend
│   └── Dockerfile.web
└── PHFWorkManagement.slnx          # Visual Studio / .NET Solution
```

---

## 3. Quickstart Guide

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) (or .NET 9/10 preview)
- [Node.js 20+ / 22+](https://nodejs.org)
- [Git](https://git-scm.com)

---

### Running the Backend API (.NET 8)

The backend is configured with **dual database support**:
- **SQLite (Zero-Config Development):** Enabled by default in `appsettings.json`. Database (`phf_local.db`) and seed data are created automatically on launch.
- **PostgreSQL 16+ (Production/Docker):** Set `"DatabaseProvider": "PostgreSQL"` in `appsettings.json`.

```bash
cd "backend/src/PHF.Api"
dotnet run
```
- **API URL:** `http://localhost:5000`
- **Swagger / OpenAPI:** `http://localhost:5000/swagger`
- **SignalR Workstation Hub:** `http://localhost:5000/hubs/workstation`

---

### Running the Manager Web Portal (Next.js 15)

```bash
cd web
npm run dev
```
- **Portal URL:** `http://localhost:3000`
- Features:
  - **Live Oversight:** Real-time station tracker (Active, Idle, Offline) + Streaming Event Feed.
  - **Task Distribution:** Kanban board with deliverable review links.
  - **Payroll Timesheets:** Consolidated table with 1-click CSV export button.
  - **Task Dispatcher:** Quick dispatch modal.

---

### Running the Windows Desktop Client (WPF)

```bash
cd "client/src/PHF.Client.Wpf"
dotnet run
```
- Features:
  - **Shift Management:** Clock In / Clock Out toggle with total hours logged today.
  - **Telemetry Indicator:** Real-time active foreground app, listening status (near-0% CPU).
  - **Task Feed:** Displays assigned tasks (`TASK-108`, `TASK-109`, `TASK-110`).
  - **Deliverable Gate:** Drag-and-drop file upload zone + URL input. "Mark Completed" remains disabled until proof of work is attached!
  - **Assistive Support:** Full screen-reader labels for NVDA and Windows Narrator.

---

### Running with Docker Compose (Full Enterprise Stack)

```bash
cd docker
docker-compose up --build
```
This launches:
1. **PostgreSQL 16** (`localhost:5432`) with automatic schema initialization (`001_initial_schema.sql`).
2. **MinIO Object Storage** (`localhost:9000` & Console `localhost:9001`).
3. **Backend API** (`localhost:5000`).
4. **Manager Web Portal** (`localhost:3000`).

---

## 4. Default Test Credentials

| Account Role | Email | Password | Station ID | Full Name |
|---|---|---|---|---|
| **ADMIN** | `admin@punnyaheart.org` | `Password@123` | `ST-ADMIN` | System Administrator |
| **MANAGER** | `manager@punnyaheart.org` | `Password@123` | `ST-MGR` | Operations Manager |
| **EMPLOYEE** | `st101@punnyaheart.org` | `Password@123` | `ST-101` | Aarav Sharma |
| **EMPLOYEE** | `st102@punnyaheart.org` | `Password@123` | `ST-102` | Priya Patel |
| **EMPLOYEE** | `st103@punnyaheart.org` | `Password@123` | `ST-103` | Rahul Verma |
| **EMPLOYEE** | `st104@punnyaheart.org` | `Password@123` | `ST-104` | Sneha Reddy |

---

## 5. Automated Tests & Verification

### Running .NET Unit & Integration Tests
```bash
dotnet test backend/tests/PHF.Api.Tests/PHF.Api.Tests.csproj
```
Tests include:
- **Deliverable Gate Validation:** Verifies that attempting to complete a task without an attached deliverable throws an `InvalidOperationException`, and completing with a deliverable succeeds.
- **Timesheet CSV Export:** Verifies CSV generation with UTF-8 BOM, headers, and employee activity aggregations.

### Building the Entire Solution
```bash
dotnet build PHFWorkManagement.slnx
```

### Building the Web Portal
```bash
cd web
npm run build
```

---

## 6. Key API Endpoints Reference

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/v1/auth/login` | Authenticate user & issue JWT token |
| `GET` | `/api/v1/auth/me` | Retrieve profile of authenticated user |
| `GET` | `/api/v1/tasks` | List tasks (optional filters: `status`, `assignedTo`, `priority`) |
| `POST` | `/api/v1/tasks` | Create/dispatch a new task (auto-generates `TASK-xxx` code) |
| `PATCH` | `/api/v1/tasks/{id}/status` | Update task status (**Deliverable Gate enforced**) |
| `POST` | `/api/v1/tasks/{id}/attachments` | Upload deliverable file attachment (multipart/form-data) |
| `GET` | `/api/v1/tasks/{id}/deliverable` | Direct download of task deliverable file |
| `POST` | `/api/v1/telemetry/batch` | High-throughput batch ingestion of activity logs and sessions |
| `POST` | `/api/v1/telemetry/heartbeat` | Live station heartbeat with active app and task context |
| `GET` | `/api/v1/telemetry/stations` | Current status of all workstations (`ACTIVE`, `IDLE`, `OFFLINE`) |
| `GET` | `/api/v1/telemetry/events` | Recent live event stream |
| `POST` | `/api/v1/shifts/clock-in` | Record employee clock in |
| `POST` | `/api/v1/shifts/clock-out` | Record employee clock out |
| `GET` | `/api/v1/shifts/current` | Active shift and total hours logged today |
| `GET` | `/api/v1/timesheet/summary` | Team efficiency, active stations count, and aggregated rows |
| `GET` | `/api/v1/timesheet/export/csv` | **One-click payroll CSV export** |

---

## 7. Compliance & Standards

- **Privacy & Security:** Keystroke logging is intentionally excluded. Activity logs capture only foreground application names and window titles.
- **WCAG 2.1 AA:** Accessible contrast ratios, full keyboard accessibility, and UI Automation labeling for screen readers.
- **Tamper Resistance:** SQLite offline logs are verified via SHA-256 HMAC signatures to prevent manual log alteration.
