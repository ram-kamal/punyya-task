# Punnya Heart Foundation — Web Admin & Operations Console Manual

> **Application:** Punnya Heart Work Management — Web Administration Console  
> **Target Roles:** System Administrators, Operations Managers, Team Leads  
> **Portal URL:** `https://punnya-api.onrender.com` (Production API & Admin Web Console)  
> **Organization:** Punnya Heart Foundation  

---

## Table of Contents
1. [Overview & Role-Based Access Control (RBAC)](#1-overview--role-based-access-control-rbac)
2. [Logging In to the Operations Console](#2-logging-in-to-the-operations-console)
3. [Global Header & Navigation Tabs](#3-global-header--navigation-tabs)
4. [Tab 1: Live Fleet Overview & Telemetry Feed](#4-tab-1-live-fleet-overview--telemetry-feed)
5. [Tab 2: Task Dispatcher & Deliverable Review](#5-tab-2-task-dispatcher--deliverable-review)
6. [Tab 3: Timesheets & Long-Term Efficiency Analytics](#6-tab-3-timesheets--long-term-efficiency-analytics)
7. [Tab 4: Employee Leave Management Center](#7-tab-4-employee-leave-management-center)
8. [Tab 5: Staff Directory, Roles & Access Control](#8-tab-5-staff-directory-roles--access-control)
9. [Exporting Timesheet Data for Payroll](#9-exporting-timesheet-data-for-payroll)
10. [Cloud Deployment & Environment Settings](#10-cloud-deployment--environment-settings)
11. [Troubleshooting & Security Best Practices](#11-troubleshooting--security-best-practices)

---

## 1. Overview & Role-Based Access Control (RBAC)

The **Punnya Heart Web Management Console** is a centralized administrative command center built with **Next.js 15**, **ASP.NET Core 8 Web API**, and **SignalR**. It delivers instantaneous visibility into workstation productivity, task fulfillment, employee attendance, and leave workflows.

### User Roles & Permissions:
- **`ADMIN` (System Administrator):** Full access to all modules, including staff enrollment, password resets, role assignment, leave review, timesheet exports, and task deletion.
- **`MANAGER` (Operations Lead):** Can view live fleet telemetry, create and assign tasks, approve/reject leaves, inspect deliverables, and export timesheets.
- **`EMPLOYEE` (Standard Staff):** Restricted from accessing the management console. Restricted to their own desktop workstation client.

---

## 2. Logging In to the Operations Console

Navigate to the web portal in any modern desktop web browser (Chrome, Edge, Firefox, Safari):

```
+-------------------------------------------------------------+
|  ♥ PUNNYA HEART FOUNDATION                                  |
|    Enterprise Operations & Workstation Oversight Console    |
|                                                             |
|  Username or Email:                                         |
|  [ dhvaj                                                  ] |
|                                                             |
|  Password:                                                  |
|  [ ****************                                       ] |
|                                                             |
|  [          Sign In to Admin Console                      ] |
+-------------------------------------------------------------+
```

### Form Controls:
1. **Username or Email:** Enter your administrative account (e.g., `dhvaj` or `dhyanidhvaj@gmail.com`).
2. **Password:** Enter your administrative password.
3. **Sign In to Admin Console:** Authenticates with the backend via `POST /api/v1/auth/login`. On successful login, an encrypted JWT token and user profile are securely stored in your browser session.

> [!NOTE]
> The authentication endpoint is protected by an automated rate limiter (maximum 10 attempts per minute) to guard against brute-force credential attacks.

---

## 3. Global Header & Navigation Tabs

Once authenticated, the permanent top navigation bar gives you instant switching between all management modules:

```
+---------------------------------------------------------------------------------------------------------------+
| ♥ Punnya Heart Foundation   [● LIVE SIGNALR CONNECTED]                                                        |
| [ LIVE FLEET ]  [ TASKS ]  [ TIMESHEETS ]  [ LEAVES ]  [ STAFF DIRECTORY ]       [👤 dhvaj (ADMIN)]  [Sign Out]|
+---------------------------------------------------------------------------------------------------------------+
```

### Header Indicators & Controls:
- **Logo & Title:** Links back to the default Live Fleet overview.
- **SignalR Connection Badge:**
  - **`● LIVE SIGNALR CONNECTED` (Emerald):** Connected to real-time WebSockets; new shift events, task completions, and leave applications update your screen automatically without manual refreshing.
  - **`○ RECONNECTING...` (Amber):** Temporary network interruption; automatically attempts reconnection every 2–5 seconds.
- **User Profile Pill:** Shows your display name and current RBAC role (`ADMIN` or `MANAGER`).
- **Sign Out Button:** Clears the stored JWT token and redirects back to the sign-in screen.

---

## 4. Tab 1: Live Fleet Overview & Telemetry Feed

Clicking the **LIVE FLEET** tab opens the operational heartbeat of the organization.

```
+---------------------------------------------------------------------------------------------------------------+
| TOTAL WORKSTATIONS     ACTIVE SHIFTS NOW         IDLE STATIONS            AVERAGE TEAM EFFICIENCY             |
|        12                     8                         1                          89.4%                      |
+---------------------------------------------------------------------------------------------------------------+
| ACTIVE WORKSTATIONS GRID                                     | LIVE EVENT STREAM                              |
| ┌──────────────────────────────────────────────────────────┐ | ┌────────────────────────────────────────────┐ |
| │ [● ACTIVE] WFH-PC-DESK01       Worker: Rajesh Sharma     │ | │ 10:42:15 AM - WFH-PC01: Rajesh Sharma      │ |
| │ Focused App: WINWORD.EXE                                 │ | │ Focus: WINWORD.EXE (Patient Report.docx)   │ |
| │ Window: Patient Medical Outreach Report 2026.docx        │ | │ ------------------------------------------ │ |
| │ Active Task: [PHF-101] Compile Outreach Survey           │ | │ 10:41:02 AM - DESK-02: Sneha Patel         │ |
| │ Today: Active 4h 12m | Idle 18m | Last Seen: Just now    │ | │ CLOCK_IN: Employee clocked in at DESK-02   │ |
| └──────────────────────────────────────────────────────────┘ | └────────────────────────────────────────────┘ |
+---------------------------------------------------------------------------------------------------------------+
```

### Top Metric KPI Cards:
- **Total Workstations:** Total number of registered stations across remote and in-office staff.
- **Active Shifts Now:** Number of employees currently clocked in and producing telemetry.
- **Idle Stations:** Workstations where no mouse/keyboard input has occurred for > 5 minutes.
- **Average Team Efficiency:** Real-time formula based on actual active time vs total shift hours today:  
  $$\text{Team Efficiency} = \left(\frac{\text{Total Active Seconds}}{\text{Total Logged Seconds}}\right) \times 100$$  
  *(Displays a clean `—` dash if no shifts have been started today).*

### Workstation Cards (Left Grid):
- **Station ID & Status Badge:** Green `ACTIVE`, Amber `IDLE`, or Gray `OFFLINE`.
- **Worker Name:** Name of the employee logged into that device.
- **Focused App & Window Title:** Live application process (e.g., `EXCEL.EXE`, `chrome.exe`) and document title currently in the employee's foreground.
- **Active Task:** Current ticket linked to this employee's telemetry.
- **Time Meters:** Live counters of active minutes and idle minutes accumulated today.

### Live Event Stream (Right Sidebar):
- Real-time chronologically sorted feed of events: `CLOCK_IN`, `CLOCK_OUT`, `LOCK`, `UNLOCK`, `TASK_ASSIGNED`, and window switches.

---

## 5. Tab 2: Task Dispatcher & Deliverable Review

Clicking the **TASKS** tab opens the task dispatcher and proof-of-work review interface.

```
+---------------------------------------------------------------------------------------------------------------+
| TASKS MANAGEMENT                  [ Search tasks...               ]  [ Priority: ALL ▼ ]  [ + New Task ]      |
+---------------------------------------------------------------------------------------------------------------+
| CODE     PRIORITY   TITLE & ASSIGNEE                      DUE DATE      DELIVERABLE PROOF      STATUS         |
| ------------------------------------------------------------------------------------------------------------- |
| PHF-101  [HIGH]     Compile Outreach Survey Data          2026-09-12    [📄 Survey_Final.xlsx] [COMPLETED ▼]  |
|                     Assigned to: Rajesh Sharma                          Size: 2.4 MB           [ 🗑 Delete ]  |
|                                                                                                               |
| PHF-102  [MEDIUM]   Cardiology Patient Follow-up Calls    2026-09-14    No Deliverable Yet     [IN PROGRESS ▼]|
|                     Assigned to: Sneha Patel                                                   [ 🗑 Delete ]  |
+---------------------------------------------------------------------------------------------------------------+
```

### Creating & Dispatching a New Task:
1. Click the **`+ New Task`** button in the top toolbar.
2. Fill out the **Create New Task** modal:
   - **Task Title:** Concise name of the assignment.
   - **Task Code:** Identifier (e.g., `PHF-105`).
   - **Description:** Clear instructions for the worker.
   - **Assignee:** Select the employee from the registered staff dropdown.
   - **Priority:** Choose `LOW`, `MEDIUM`, or `HIGH`.
   - **Due Date:** Select target completion deadline.
3. Click **Dispatch Task**. The task appears immediately in the employee's desktop client feed.

### Reviewing Proof-of-Work Deliverables:
- When an employee marks a task completed, they are required to attach proof.
- In the **Deliverable Proof** column:
  - If a file was uploaded: A clickable link (e.g., `[📄 Survey_Final.xlsx]`) allows the manager to **download and inspect the output with 1 click**.
  - If a web link was submitted: A clickable external URL opens the deliverable (Google Drive, Figma, GitHub, etc.) in a new browser tab.

### Updating Status or Deleting:
- Use the **Status Dropdown** to manually transition a task between `PENDING`, `IN_PROGRESS`, and `COMPLETED`.
- Click the **🗑 Delete** button (with confirmation prompt) to cancel or remove obsolete tasks.

---

## 6. Tab 3: Timesheets & Long-Term Efficiency Analytics

Clicking the **TIMESHEETS** tab gives managers full historical and analytical control over workforce attendance and productivity.

```
+---------------------------------------------------------------------------------------------------------------+
| PERIOD: [ TODAY ]  [ YESTERDAY ]  [ THIS WEEK ]  [ THIS MONTH ]  [ LAST MONTH ]  [ THIS YEAR ] [ Custom Range ]|
| From: [ 2026-09-01 ]  To: [ 2026-09-30 ]  [ Apply Range ]                                                     |
+---------------------------------------------------------------------------------------------------------------+
| AVERAGE EFFICIENCY       TOTAL HOURS WORKED       ACTIVE WORKERS       TASKS COMPLETED                        |
|       91.2%                    184.5 hrs                8                    34                               |
+---------------------------------------------------------------------------------------------------------------+
| View Mode: (●) Consolidated Summary   ( ) Itemized Daily Breakdown            [ 📥 Export CSV for Payroll ]   |
| Search: [ Filter by employee, station, or date...                                                           ] |
+---------------------------------------------------------------------------------------------------------------+
| DATE / PERIOD    EMPLOYEE         STATION   CLOCK IN   CLOCK OUT   TOTAL HOURS  ACTIVE     IDLE     EFFICIENCY|
| ------------------------------------------------------------------------------------------------------------- |
| Sep 01 - Sep 10  Rajesh Sharma    WFH-PC01  09:00 AM   05:30 PM    42.5 hrs     38h 15m    4h 15m   [ 90.0% ] |
| Sep 01 - Sep 10  Sneha Patel      DESK-02   09:15 AM   05:00 PM    38.0 hrs     35h 20m    2h 40m   [ 93.0% ] |
+---------------------------------------------------------------------------------------------------------------+
```

### Period Selection Controls:
- **Quick Period Buttons:**
  - **`TODAY`:** Current day's shift hours and active timers.
  - **`YESTERDAY`:** Complete 24-hour records from yesterday.
  - **`THIS WEEK`:** Monday through Sunday of the current week.
  - **`THIS MONTH`:** 1st of the current month to today.
  - **`LAST MONTH`:** Complete previous calendar month.
  - **`THIS YEAR`:** Long-term annual tracking from January 1st to December 31st.
- **Custom Range:** Select custom `From` and `To` dates and click **Apply Range** to audit any fiscal quarter or project window.

### View Modes:
1. **Consolidated Summary:** Groups by employee across the selected period, showing total days worked, total hours, cumulative active/idle split, and average efficiency percentage.
2. **Itemized Daily Breakdown:** Expands every day individually, showing exact clock-in/out timestamps, tasks finished on that day, and day-by-day efficiency ratings.

### Efficiency Color Coding:
- **Green (`≥ 80.0%`):** High productivity, excellent active task focus.
- **Amber (`60.0% – 79.9%`):** Standard performance; moderate idle periods.
- **Red (`< 60.0%`):** High idle duration; requires supervisor check-in.

---

## 7. Tab 4: Employee Leave Management Center

Clicking the **LEAVES** tab displays all employee vacation, medical, and emergency leave requests.

```
+---------------------------------------------------------------------------------------------------------------+
| LEAVE REQUESTS                    Filter: [ ALL ]  [ PENDING (2) ]  [ APPROVED ]  [ REJECTED ]                |
+---------------------------------------------------------------------------------------------------------------+
| EMPLOYEE         TYPE      DATES                    DAYS    REASON                   STATUS       ACTIONS     |
| ------------------------------------------------------------------------------------------------------------- |
| Rajesh Sharma    CASUAL    2026-09-20 to 2026-09-22 3 days  Attending family wedding [PENDING]    [✓ Approve] |
|                                                                                                   [✕ Reject]  |
| Sneha Patel      SICK      2026-09-08 to 2026-09-09 2 days  Doctor advised bed rest  [APPROVED]   Reviewed by |
|                                                                                      Note: OK     dhvaj       |
+---------------------------------------------------------------------------------------------------------------+
```

### Reviewing a Leave Request:
1. Locate the request under the **`PENDING`** filter.
2. Inspect the employee's requested dates and reason.
3. Click either:
   - **`[✓ Approve]`:** Opens confirmation modal. Optionally add remarks (e.g., *"Approved. Ensure handover is completed."*). Click **Confirm Approval**.
   - **`[✕ Reject]`:** Opens rejection modal. Provide clear feedback explaining why the request cannot be granted. Click **Confirm Rejection**.
4. The employee's desktop client immediately receives a real-time toast alert with your decision and feedback.

---

## 8. Tab 5: Staff Directory, Roles & Access Control

Clicking the **STAFF DIRECTORY** tab opens the user management console.

```
+---------------------------------------------------------------------------------------------------------------+
| STAFF MANAGEMENT        Search: [ Filter staff...        ]   Role: [ ALL ▼ ]          [ + Add New Staff ]     |
+---------------------------------------------------------------------------------------------------------------+
| FULL NAME       USERNAME     EMAIL                   ROLE         WORK MODE        STATUS       ACTIONS   |
| ------------------------------------------------------------------------------------------------------------- |
| dhvaj           @dhvaj       dhyanidhvaj@gmail.com   [ADMIN]      🏢 IN-OFFICE     [ACTIVE]     [Edit]    |
|                                                                                                 [Reset PW]|
| Rajesh Sharma   @rajesh      rajesh@punnyaheart.org  [EMPLOYEE]   🏠 REMOTE (WFH)  [ACTIVE]     [Suspend] |
|                                                                                                 [Reset PW]|
+---------------------------------------------------------------------------------------------------------------+
```

### Adding a New Staff Member:
1. Click **`+ Add New Staff`**.
2. Complete the enrollment form:
   - **Full Name:** Official employee name.
   - **Username:** Alphanumeric handle (e.g. `rajesh`).
   - **Email:** Official email address.
   - **Role:** Select `EMPLOYEE`, `MANAGER`, or `ADMIN`.
   - **Work Mode:** Select `🏠 Remote (Work From Home)` or `🏢 In-Office (Fixed Desk)`.
   - **Initial Temporary Password:**
     - Type a password, OR
     - Click **`🎲 Generate Secure`** to instantly generate a 12-character high-entropy secure temporary password.
3. Click **Enroll Staff Member**. The employee can immediately log in from their desktop app.

### 1-Click Password Reset:
If an employee forgets their password:
1. Click **`[Reset PW]`** next to their profile.
2. Type a new temporary password or click **`🎲 Generate Secure`**.
3. Click **Update Password**. The password hash in PostgreSQL/Supabase is updated instantly using BCrypt.

### Suspending or Reactivating Staff:
- Click **`[Suspend]`** on an employee card to immediately block them from clocking in or logging into the desktop client.
- Click **`[Reactivate]`** to restore access at any time.

---

## 9. Exporting Timesheet Data for Payroll

To download timesheet records for HR and payroll processing:
1. Navigate to the **TIMESHEETS** tab.
2. Select the desired period (e.g., `THIS MONTH` or a custom date range).
3. Click the **`📥 Export CSV for Payroll`** button.
4. Your browser will download an ISO-standard CSV file named:  
   `Timesheet_Export_<Period>_<Date>.csv`

### Sample CSV Structure:
```csv
Date,StationId,EmployeeName,Email,ClockIn,ClockOut,TotalHours,ActiveHours,IdleHours,EfficiencyPercentage,TasksCompleted,DaysWorked
2026-09-10,WFH-PC01,Rajesh Sharma,rajesh@punnyaheart.org,09:00 AM,05:30 PM,8.50,7.75,0.75,91.2%,3,1
2026-09-10,DESK-02,Sneha Patel,sneha@punnyaheart.org,09:15 AM,05:00 PM,7.75,7.10,0.65,91.6%,2,1
```

---

## 10. Cloud Deployment & Environment Settings

The backend and web frontend are deployed via Render:

### Key Environment Variables (Production):

| Variable Name | Purpose | Example / Default |
| :--- | :--- | :--- |
| `DATABASE_URL` | Supabase PostgreSQL Connection Pooler | `postgres://postgres:Task_Punyya@2026@db.ghsdpgkmxrllxuwfhwnc.supabase.co:5432/postgres` |
| `DatabaseProvider` | Database engine selector | `PostgreSQL` |
| `JWT_SECRET` | 256-bit symmetric HMAC-SHA key | Configured in Render Environment Settings |
| `DEFAULT_ADMIN_EMAIL` | Fallback admin email if no admin exists | `dhyanidhvaj@gmail.com` |
| `DEFAULT_ADMIN_USERNAME` | Fallback admin username | `dhvaj` |
| `DEFAULT_ADMIN_PASSWORD` | Fallback admin initial password | `Admin0246` |
| `NEXT_PUBLIC_API_URL` | Web client backend endpoint | `https://punnya-api.onrender.com` |

---

## 11. Troubleshooting & Security Best Practices

1. **Manager Cannot Access Staff Directory:**
   - Ensure the user's role is set to `ADMIN` or `MANAGER`. Standard `EMPLOYEE` accounts receive HTTP 403 Forbidden.
2. **SignalR Connection Shows Disconnected:**
   - Verify that your corporate network allows WebSocket connections over port 443 (`wss://punnya-api.onrender.com/hubs/workstation`).
3. **Database Maintenance:**
   - Seed data never overwrites existing records. All timesheets, shift logs, tasks, and audit sessions persist safely in PostgreSQL across deployments.
