# Punnya Heart Desk — Workstation Desktop Client User Manual

> **Application:** Punnya Heart Desk (Windows Workstation Client)  
> **Version:** 1.0.0-Beta (Self-Contained x64)  
> **Audience:** Employees, Remote Staff, and On-Site Workstation Operators  
> **Organization:** Punnya Heart Foundation  

---

## Table of Contents
1. [Overview & Key Concepts](#1-overview--key-concepts)
2. [Installation & First-Time Setup](#2-installation--first-time-setup)
3. [Signing In & Workstation Pairing](#3-signing-in--workstation-pairing)
4. [Main Dashboard & Navigation Overview](#4-main-dashboard--navigation-overview)
5. [Shift Attendance & Time Tracking](#5-shift-attendance--time-tracking)
6. [Workstation Telemetry & Focus Tracking](#6-workstation-telemetry--focus-tracking)
7. [Task Feed & Deliverable-Linked Workflow](#7-task-feed--deliverable-linked-workflow)
8. [Employee Leave Center](#8-employee-leave-center)
9. [Offline Mode & Automatic Sync](#9-offline-mode--automatic-sync)
10. [Real-Time Alerts & Notification Toast](#10-real-time-alerts--notification-toast)
11. [Accessibility & Keyboard Shortcuts](#11-accessibility--keyboard-shortcuts)
12. [Troubleshooting & Frequently Asked Questions](#12-troubleshooting--frequently-asked-questions)

---

## 1. Overview & Key Concepts

**Punnya Heart Desk** is the official Windows desktop client designed specifically for Punnya Heart Foundation staff. It combines **shift attendance logging**, **event-driven workstation telemetry**, and **deliverable-linked task management** into a single native Windows application.

### Why Punnya Heart Desk is Unique
- **Ultra-Lightweight & High Performance:** Uses native Win32 hooks (`SetWinEventHook`) that run only when you switch application windows. It consumes less than 1% CPU throughout an entire 8-hour shift.
- **Proof-of-Work Deliverable Gate:** A task cannot be marked completed without attaching verifiable output (a file or a work link). This ensures clear accountability for remote and in-office work.
- **Tamper-Proof Offline Logging:** If your Internet connection drops, your work logs are safely queued in a cryptographically signed local SQLite database (`telemetry_offline.db`) and uploaded automatically the moment connectivity is restored.
- **Full Accessibility:** Engineered according to WCAG 2.1 AA guidelines with comprehensive screen-reader narration (Windows Narrator / NVDA) and keyboard tab navigation.

---

## 2. Installation & First-Time Setup

### System Requirements
- **Operating System:** Windows 10 (version 1809+) or Windows 11 (64-bit).
- **RAM:** Minimum 2 GB (Recommended 4 GB).
- **Disk Space:** Approximately 180 MB free disk space.
- **Network:** Active Internet connection (supports offline queuing if temporarily disconnected).

### Installation Steps

1. **Download the Setup Installer:**
   Locate `PunnyaHeartDesk_Setup_v1.0.exe` provided by your IT administrator.
2. **Run the Installer:**
   - Double-click `PunnyaHeartDesk_Setup_v1.0.exe`.
   - The installer runs cleanly in user-space (`AppData\Local\Programs\Punnya Heart Desk`) without requiring administrative permissions.
   - Choose whether you want a **Desktop Shortcut** created.
   - *(Recommended)* Check **"Launch Punnya Heart Desk automatically when Windows starts"** so your shift workstation is always ready.
3. **Finish & Launch:**
   - Click **Finish**. The application will launch immediately.

> [!TIP]
> If using the portable ZIP package (`PunnyaHeartDesk-Beta-v1.0-win-x64.zip`), simply extract the ZIP to any local folder and launch `PunnyaHeartDesk.exe` directly.

---

## 3. Signing In & Workstation Pairing

When you launch the application, you will be presented with the **Punnya Heart Desk Sign-In Dialog**:

```
+-------------------------------------------------------+
|  ♥  Punnya Heart Foundation                           |
|     Sign in to your workstation to log shift & tasks  |
|                                                       |
|  Username or Email:  [ employee@punnyaheart.org     ] |
|  Password:           [ ********************         ] |
|                                                       |
|  [✓] Working from Home (Remote)                       |
|      Paired Device: WFH-DESKTOP-ABC                   |
|                                                       |
|  > ⚙ Advanced Connection Settings (Optional)          |
|                                                       |
|  [         Clock In & Launch Desk                   ] |
+-------------------------------------------------------+
```

### Form Fields & Interactive Controls:

| Control Name | Type | Purpose | How to Use |
| :--- | :--- | :--- | :--- |
| **Username or Email** | Text Input | Identifies your staff profile | Enter your official organization email or username (e.g., `dhvaj` or `dhyanidhvaj@gmail.com`). |
| **Password** | Password Input | Authenticates your identity | Enter your password. Keystrokes are masked for security. |
| **Working from Home (Remote)** | Checkbox Toggle | Switches device mode between remote and in-office | - **Checked (Default):** Sets your station ID to `WFH-<YourPCName>`. Use this when telecommuting.<br>- **Unchecked:** Sets your station ID to `DESK-<YourPCName>` for fixed office workstations. |
| **Paired Device** | Status Text | Visual verification of station identifier | Automatically updates as you toggle the Remote checkbox. |
| **⚙ Advanced Connection Settings** | Expander | Technical endpoint configuration | Click to expand if directed by IT. Contains:<br>• **Server API URL:** Defaults to `https://punnya-api.onrender.com`.<br>• **Station Identifier:** Allows manual override of station ID. |
| **Clock In & Launch Desk** | Button | Logs in and initiates shift session | Validates your credentials, starts your shift session, connects live telemetry, and opens the main dashboard. |

---

## 4. Main Dashboard & Navigation Overview

Once signed in, the main dashboard provides a unified control center:

```
+----------------------------------------------------------------------------------------------------+
| ♥ Punnya Heart Desk  [● LIVE CONNECTED]     Punnya Heart Foundation • Workstation Telemetry & Task Desk |
|                                             [👤 dhvaj (WFH-PC)]  [ Sign Out ]                       |
+----------------------------------------------------------------------------------------------------+
| +-----------------------------------------------+ +-----------------------------------------------+ |
| | SHIFT MANAGEMENT               [● ACTIVE]     | | WORKSTATION TELEMETRY & ACTIVE TASK           | |
| | Clocked in at 09:15 AM                        | | Focused App: Code.exe - VS Code               | |
| | Total Elapsed: 3.8 hrs (Active: 3h 20m)       | | Window Title: MainWindow.xaml.cs              | |
| | [ Clock Out (End Shift) ]                     | | Active Task: [PHF-102] Implement SignalR Hub  | |
| |                                               | | [ 🌴 Leave Center ]                           | |
| +-----------------------------------------------+ +-----------------------------------------------+ |
+----------------------------------------------------------------------------------------------------+
| TOOLBAR: [ Search tasks...                 ]  Status: [ ALL ▼ ]                  [ ↻ Refresh Tasks ]|
+----------------------------------------------------------------------------------------------------+
| TASK FEED (Assigned to you):                                                                       |
| ┌────────────────────────────────────────────────────────────────────────────────────────────────┐ |
| │ [PHF-102] Implement SignalR Hub                    [HIGH] [IN PROGRESS]                        │ |
| │ Connect live workstation events to the web management feed.                                    │ |
| │ Deliverable: None Attached                                                                     │ |
| │ [ Pause Task ]  [ Attach Deliverable (Required) ]  [ Mark Completed ✓ (Locked) ]               │ |
| └────────────────────────────────────────────────────────────────────────────────────────────────┘ |
+----------------------------------------------------------------------------------------------------+
```

### Top Fluent App Bar Elements

- **♥ Emblem:** Organization brand icon.
- **Connection Status Pill:**
  - **`LIVE CONNECTED` (Green):** Successfully connected to the cloud server and SignalR real-time stream.
  - **`RECONNECTING...` (Amber):** Network disruption detected; reconnecting automatically.
  - **`OFFLINE BUFFERING` (Gray/Red):** Completely offline; all events are being stored locally in the tamper-proof buffer.
- **User Profile Pill (`TxtUserInfo`):** Displays your current authenticated name, role, and station ID.
- **Sign Out Button (`BtnLogout`):** Ends your current UI session and returns to the sign-in modal.

---

## 5. Shift Attendance & Time Tracking

Shift tracking is located in **Panel A (Left Card)**:

### Features & Controls
1. **Status Badge:** Displays `SHIFT ACTIVE` in green when clocked in, or `NOT CLOCKED IN` in neutral gray.
2. **Clock-In Time:** Displays the exact starting time of your shift (e.g., `Clocked in at 09:15 AM`).
3. **Total Hours:** Displays total elapsed shift time in decimal hours (e.g., `4.5 hrs`).
4. **Active Time vs. Idle Time:**
   - **Active:** Time spent actively typing, clicking, or switching task windows.
   - **Idle:** Time where no user input occurred for over 5 minutes (300 seconds).
5. **Clock Out Button (`BtnClockOut`):**
   - Click when completing your workday.
   - Confirms shift completion, finalizes total active and idle hours, logs the `CLOCK_OUT` event to management, and resets the shift timer.

---

## 6. Workstation Telemetry & Focus Tracking

Located in **Panel B (Right Card)**:

### How Telemetry Works
- The app automatically detects which window you are currently working in (e.g., `Excel`, `Visual Studio`, `Chrome`, `Word`).
- **Focused App:** Shows the current process name (e.g., `WINWORD.EXE`).
- **Window Title:** Displays the document or page title you are viewing.
- **Privacy Sanitization:** Windows system processes (such as Lock Screen, Logon UI, or Program Manager) are automatically sanitized to display generic `"Windows Desktop"` to protect system privacy.
- **Idle Threshold:** If no keyboard or mouse activity is detected for 5 minutes, your status automatically transitions to `IDLE` or `LOCKED`.

---

## 7. Task Feed & Deliverable-Linked Workflow

### Searching & Filtering Tasks
- **Search Bar (`TxtTaskSearch`):** Type any keyword, task title, or task code (e.g. `PHF-102`) to filter tasks instantly.
- **Status Dropdown (`CmbTaskFilter`):** Filter by:
  - `ALL`: Displays all tasks assigned to you.
  - `PENDING`: Unstarted tasks waiting for work.
  - `IN_PROGRESS`: Tasks currently active.
  - `COMPLETED`: Finished tasks with verified deliverables.
- **Refresh Button (`BtnRefreshTasks`):** Instantly queries the server for newly assigned tasks.

---

### Task Lifecycle & Step-by-Step Execution

#### Step 1: Start Working on a Task
- Click **Start Working** on any assigned task card.
- The task status updates to `IN_PROGRESS`.
- The top telemetry panel links your active window focus directly to this task code (`_activeTaskCode`), ensuring your work time is credited to the correct ticket.

#### Step 2: Pausing Work
- If you need to switch to another task, click **Pause Task**.
- Status returns to `PENDING` and active telemetry unlinks.

#### Step 3: Attaching Proof of Work (Deliverable)
> [!IMPORTANT]
> To prevent accidental or incomplete task sign-offs, the **"Mark Completed ✓"** button is **permanently disabled (locked)** until a valid deliverable is submitted.

1. Click **Attach Proof / Deliverable**. The submission dialog appears:

```
+-----------------------------------------------------------+
|  Attach Deliverable / Proof of Work                       |
|  Task: [PHF-102] Implement SignalR Hub                    |
|                                                           |
|  Deliverable Type:  (●) Local File    ( ) External URL    |
|                                                           |
|  [ Browse Local File... ]                                 |
|  Selected: C:\Reports\SignalR_Hub_Documentation.pdf       |
|                                                           |
|  Submission Notes / Comments:                             |
|  [ Tested and verified on staging cluster.              ] |
|                                                           |
|  [ Cancel ]                     [ Upload & Attach Proof ] |
+-----------------------------------------------------------+
```

2. Choose either:
   - **Local File:** Click **Browse Local File...** to select your file (`.pdf`, `.docx`, `.xlsx`, `.png`, `.jpg`, `.zip`, `.txt`, `.csv` up to 25 MB).
   - **External URL:** Enter a URL (e.g., Google Drive link, GitHub Pull Request, Figma board).
3. Enter any notes in the comment box.
4. Click **Upload & Attach Proof**. The file is uploaded to cloud storage, and the task card updates to show `Deliverable Attached ✓`.

#### Step 4: Mark Completed
- The **Mark Completed ✓** button is now **unlocked and highlighted in green**.
- Click **Mark Completed ✓** to finalize the task. Management is notified immediately via live SignalR feed.

---

## 8. Employee Leave Center

Need to take time off? Click the **🌴 Leave Center** button on Panel B to open the Leave Center Dialog.

```
+------------------------------------------------------------------------------------------+
| Employee Leave Center                                                                [✕] |
| Apply for time-off and track management review status in real time                       |
+-------------------------------------------+----------------------------------------------+
| APPLY FOR LEAVE                           | MY SUBMITTED REQUESTS       [● LIVE AUTO-SYNC]|
|                                           |                                              |
| Leave Type:                               | [SICK LEAVE]                     [APPROVED]  |
| [ CASUAL — Casual Leave               ▼ ] | 2026-09-15 to 2026-09-16                     |
|                                           | Reason: Doctor appointment                   |
| Start Date (YYYY-MM-DD):                  | Manager Note: Approved by Operations Lead.   |
| [ 2026-09-20                            ] |                                              |
|                                           | [CASUAL LEAVE]                    [PENDING]  |
| End Date (YYYY-MM-DD):                    | 2026-09-28 to 2026-09-29                     |
| [ 2026-09-21                            ] | Reason: Personal family trip                 |
|                                           |                                              |
| Reason / Justification:                   |                                              |
| [ Attending family ceremony in Pune.    ] |                                              |
|                                           |                                              |
| [        Submit Application ✓           ] |                                              |
+-------------------------------------------+----------------------------------------------+
```

### Applying for Leave:
1. Select **Leave Type**: `CASUAL`, `SICK`, `MEDICAL`, `ANNUAL`, or `EMERGENCY`.
2. Enter **Start Date** and **End Date** in `YYYY-MM-DD` format.
3. Provide a clear **Reason / Justification**.
4. Click **Submit Application ✓**.
5. Your request immediately appears in the right-hand panel with a yellow `PENDING` badge.

### Tracking Review Status:
- When a manager approves or rejects your request, the status updates dynamically without needing a restart.
- An in-app popup toast banner will notify you:
  - **`APPROVED` (Green):** Displays manager approval remarks.
  - **`REJECTED` (Rose):** Displays the reason for rejection provided by management.

---

## 9. Offline Mode & Automatic Sync

If your network drops while working:
1. The connection badge transitions to `OFFLINE BUFFERING`.
2. **Your logs are NOT lost.** Every process switch, window focus event, and shift tick is encrypted and saved into `telemetry_offline.db` inside your Windows `LocalAppData` directory.
3. Once your connection returns:
   - The connection badge switches back to `LIVE CONNECTED`.
   - The background worker flushes all stored logs to the server.
   - Shift timers and statistics sync automatically.

---

## 10. Real-Time Alerts & Notification Toast

A floating Windows 11 Fluent notification banner appears in the top-right corner when:
- A manager assigns a **new task** to your workstation.
- A task priority or status is updated.
- Your **leave request** has been reviewed (approved or rejected).

Click the **✕** button on the banner to dismiss it early, or let it auto-dismiss after 8 seconds.

---

## 11. Accessibility & Keyboard Shortcuts

Punnya Heart Desk is built for all users:

| Key Combination | Action |
| :--- | :--- |
| `Tab` | Moves focus to the next interactive control in logical order. |
| `Shift + Tab` | Moves focus to the previous control. |
| `Space` / `Enter` | Activates the focused button, checkbox, or combo box. |
| `Esc` | Closes active modals (Leave Center, Deliverable Submission). |
| `Alt + Down Arrow` | Opens dropdown lists (Leave Type, Filter selections). |

- **Screen Readers:** Fully compatible with **Windows Narrator** (`Win + Ctrl + Enter`) and **NVDA**. Every label, status, and button has descriptive ARIA-equivalent text.

---

## 12. Troubleshooting & Frequently Asked Questions

### Q1: The app displays "Invalid email or password" on sign-in.
- Verify whether your username or email has any leading/trailing spaces.
- If you forgot your password, contact your operations manager to perform a 1-click password reset from the Admin Console.

### Q2: Why is the "Mark Completed ✓" button disabled on my task?
- You must attach a deliverable first. Click **Attach Proof / Deliverable**, upload a file or paste your deliverable link, then click **Upload & Attach Proof**. Once attached, the button unlocks.

### Q3: My status shows "IDLE" even though I am working.
- The idle detector monitors keyboard and mouse input. If you were in a video call or reading a lengthy document for more than 5 minutes without interacting with the computer, it enters idle mode. Interacting with your mouse or keyboard immediately marks you active again.

### Q4: Where are my offline logs stored on my PC?
- Offline telemetry is securely stored in:  
  `%LOCALAPPDATA%\PHFWorkManagement\telemetry_offline.db`
- Unhandled logs and diagnostics are stored in:  
  `%LOCALAPPDATA%\PHFWorkManagement\logs\wpf_error.log`
