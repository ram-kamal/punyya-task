# Punnya Heart Desk & Work Management System — Complete UI Design System (`design.md`)

**Document Version:** 1.0.3  
**Target Systems:** Windows Desktop Console (Avalonia XAML / Fluent Mica), macOS Console, Web Portal (Next.js & Tailwind CSS)  
**Brand Identity:** Punnya Heart Foundation  

---

## 1. Design Vision & Philosophy

Punnya Heart Desk is an enterprise-grade Workstation Telemetry and Task Management suite designed with **Windows 11 Fluent Mica Dark Glassmorphism**. The aesthetic combines medical precision with modern executive control:
* **Deep Dark Surfaces:** Low-glare, dark matte slate backgrounds designed for all-day focus.
* **Translucent Glass Borders:** Subtle translucent borders (`1px solid #262938` / `#25FFFFFF`) with corner radii from `10px` to `14px`.
* **High Information Density with Clarity:** Clear tabular layouts, monospace HUD digital clocks, colored status badges, and prominent callouts.
* **Full Accessibility:** WCAG 2.1 AA compliant contrast ratios, full screen reader (`AutomationProperties`) accessibility, and consistent tab stops.

---

## 2. Color Palette & Thematic System

### A. Primary Surfaces & Backgrounds
| Token Name | Hex Code | Usage |
| :--- | :--- | :--- |
| **`SurfaceBase`** | `#0D0E12` | Root window background & modal backdrop |
| **`SurfacePanel`** | `#151720` | Primary container cards, tables, and tab views |
| **`SurfaceElevated`** | `#1C1F2B` | Interactive items, table rows, button backgrounds |
| **`SurfaceCardHeader`**| `#0F1117` | Table header bars and card section banners |
| **`BorderDefault`** | `#262938` | Subtle separators, table grid borders, card outlines |
| **`BorderSubtle`** | `#1C1F2B` | Inter-row separators |
| **`BorderFocused`** | `#E11D48` | Focused inputs, primary selection rings |

### B. Functional & Brand Accent Colors
| Category | Primary | Accent / Light | Usage |
| :--- | :--- | :--- | :--- |
| **Brand (Punnya Heart)** | `#E11D48` (Rose 600) | `#FB7185` (Rose 400) | Brand icon, primary action buttons, active tab indicators |
| **Active / Telemetry** | `#10B981` (Emerald 500) | `#34D399` (Emerald 400) | Online status, Clock In button, active time metrics, completed tasks |
| **Idle / Warning** | `#F59E0B` (Amber 500) | `#FBBF24` (Amber 400) | Idle state, Clocked Out badge, pending leave requests, medium priority |
| **Info / Monospace HUD**| `#0EA5E9` (Sky 500) | `#38BDF8` (Sky 400) | Monospace shift clock, task codes, version pills, active focus app |
| **Critical / High Priority**| `#EF4444` (Red 500) | `#F87171` (Red 400) | Clock Out button, urgent task priority, rejection actions |
| **Offline / Neutral** | `#64748B` (Slate 500) | `#94A3B8` (Slate 400) | Offline workstations, secondary labels, disabled state |

### C. Typography & Text Hierarchy
* **Primary Font Family:** `Segoe UI`, `Inter`, `-apple-system`, `sans-serif`
* **HUD & Monospace Clock:** `Consolas`, `Cascadia Code`, `Segoe UI`, `monospace`
* **Text Scale:**
  * **Window Title / App Header:** 17px – 18px (Bold)
  * **Tab Headers & Section Titles:** 14px – 15px (Bold)
  * **Table Headers & Section Badges:** 10px – 11px (Bold, Uppercase, Tracking +0.5px)
  * **Body / Row Text:** 12px – 12.5px (SemiBold / Regular)
  * **Subtitles & Auxiliary Labels:** 10.5px – 11.5px (Slate `#94A3B8`)
  * **Digital Clock HUD:** 12.5px – 14px (Consolas Bold `#38BDF8`)

---

## 3. UI Component Blueprint

### A. Global Top Navigation Header
```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ [♥] Punnya Heart Desk  [Win 11 Mica] [ADMIN] [● LIVE CONNECTED] [v1.0.3]   [✔ Auto-Boot] │
│     Workstation Telemetry & Workforce Management                [@dhvaj] [Sign Out]   │
├────────────────────────────────────────────────────────────────────────────────────────┤
│ [💻 My Desk & Shift] [👥 Workforce Fleet] [📋 Task Dispatch] [⏱️ Timesheets]            │
│ [🏖️ Leave Approvals] [👥 Staff Directory] [📊 Reports & Audits]  [📢 Push] [+ Dispatch]│
└────────────────────────────────────────────────────────────────────────────────────────┘
```
1. **Brand Cluster (Left):**
   * Rounded heart badge (Gradient `#E11D48` $\rightarrow$ `#FB7185`).
   * Platform Pill (`Windows 11 Mica` / `macOS Edition`).
   * Live connection status indicator (pulsing `#10B981` dot).
   * App version badge (`v1.0.3`).
2. **User Controls (Right):**
   * Auto-Launch on Startup checkbox toggle.
   * User profile indicator with role pill (`ADMIN` / `EMPLOYEE` / `VIEWER`).
   * Sign Out button.
3. **Tab Bar:**
   * Active tab: `#E11D48` background with `#FFFFFF` text, subtle elevation.
   * Inactive tab: Translucent glass `#1C1F2B`, border `#2E3347`, text `#94A3B8`.
   * Action buttons on far right:
     * `📢 Push Update Dialog` (Glass action button).
     * `+ Dispatch Task` (Primary rose gradient button).

---

### B. View 1: 💻 My Desk & Personal Shift (Dual Admin & Employee Role)
* **Left Glass Card: Shift Management**
  * Current status badge: `Active Shift` (`#10B981`) or `Clocked Out` (`#F59E0B`).
  * Digital Clock HUD: `04:26:15` (Sky Blue Monospace, updating every second).
  * Shift summary: `Shift Started: 10:01 AM (Total Today: 4h 26m)`.
  * Toggle Button: **Clock In** (Green `#10B981`) / **Clock Out** (Red `#E11D48`).
* **Right Glass Card: Workstation Telemetry**
  * Telemetry hook indicator: `Win32 & System Hook` (`#34D399`).
  * Active Application: Focused executable & title (e.g. `Active App: Antigravity IDE`).
  * Current Task Code Badge: e.g. `CSR-01` or `No task linked`.
* **My Assigned Tasks Table:**
  * Columns: `TASK CODE & TITLE`, `PRIORITY`, `STATUS`, `DELIVERABLE GATE`, `ACTIONS`.
  * Deliverable proof actions:
    * `Upload File` / `Link URL` / `🔗 View Deliverable`.
    * Deliverable Gate badge: `VERIFIED PROOF ✓` or `PROOF REQUIRED ⚠️`.

---

### C. View 2: 👥 Workforce Fleet (Real-Time Team Surveillance)
* **Top Metric Counters:**
  * Active Stations (`#34D399`), Idle Stations (`#FBBF24`), Total Registered (`#CBD5E1`).
* **Surveillance Table:**
  * Columns: `EMPLOYEE`, `STATION / DEVICE`, `CURRENT TASK`, `FOREGROUND APP`, `ACTIVE TODAY`, `USER STATE`.
  * `ACTIVE TODAY` displays accumulated active work time alongside shift time.
  * State Badge:
    * `ACTIVE` (Emerald gradient badge with green glow).
    * `IDLE` (Amber badge).
    * `OFFLINE` (Muted slate badge).

---

### D. View 3: 📋 Task Dispatch (Company-Wide Management)
* **Search & Filter Bar:**
  * Full-text search (Task code, title, employee name).
  * Priority Filter: `ALL`, `URGENT`, `HIGH`, `MEDIUM`, `LOW`.
  * Status Filter: `ALL`, `PENDING`, `IN_PROGRESS`, `BLOCKED`, `COMPLETED`.
* **Actions:**
  * `+ Dispatch New Task` button $\rightarrow$ Opens Glass Dispatch Modal.
  * `Export Tasks` (CSV / Excel).
  * `Import Tasks` (Bulk CSV / Excel Task Importer).

---

### E. View 4: ⏱️ Timesheets & Productivity Analytics
* **Period Toggle:** `TODAY`, `THIS WEEK`, `THIS MONTH`.
* **Aggregate Metric Cards:**
  * `TOTAL WORKFORCE HOURS`, `ACTIVE FOCUS TIME`, `TOTAL IDLE TIME`, `AVG EFFICIENCY %`.
* **Detailed Timesheet Grid:**
  * Columns: `DATE`, `EMPLOYEE`, `STATION`, `CLOCK IN`, `CLOCK OUT`, `TOTAL TIME`, `ACTIVE TIME`, `IDLE TIME`, `EFFICIENCY %`, `TASKS DONE`.
  * Export: 1-click **Export CSV** and **Export Styled Excel Report**.

---

### F. View 5: 🏖️ Leaves Center & Approval Desk
* **Personal View (Employees & Admins):**
  * Apply for Leave modal (Start Date, End Date, Leave Type: `SICK`, `CASUAL`, `ANNUAL`, `EMERGENCY`, Reason).
  * Personal leave balance & history cards.
* **Admin Approval Hub:**
  * Pending requests table with Employee details, leave type tag, date range, and reason.
  * One-click action buttons: **Approve ✓** (`#10B981`) and **Reject ✕** (`#EF4444`).

---

### G. View 6: 👥 Staff Directory
* Roster of team members with avatar initials, role (`ADMIN`, `MANAGER`, `EMPLOYEE`, `VIEWER`), work mode (`IN_OFFICE`, `REMOTE_WFH`), station pairing, and status.
* `+ Add Team Member` modal (Full Name, Email, Username, Password, Role, Work Mode, Station ID).

---

### H. View 7: 📊 Reports & Audits
* Sub-tabs: `Monthly Attendance Register`, `Daily Time & Focus Audit Log`, `Staff Performance Matrix`.
* Automated month-end timesheet summaries, present/absent counts, and focus score audits.

---

## 4. Modal Dialog Standards

All modals adhere to a unified glass structure:
* **Backdrop:** `#CC000000` with 8px Gaussian blur simulation.
* **Dialog Container:** Width `480px` – `580px`, background `#F0141722`, border `1px solid #40FFFFFF`, corner radius `14px`, padding `24px`.
* **Header:** Bold title (16px), subtitle (11.5px Slate `#94A3B8`).
* **Input Styling:**
  * Label: 11px uppercase `#94A3B8`.
  * Input Box: Background `#151720`, border `#262938`, text `#F8FAFC`, corner radius `8px`, padding `10px,8px`.
  * Focus state: Border `#E11D48`.
* **Footer:** Left-aligned `Cancel` button (Glass `#262938`) and right-aligned primary `Submit` button (Rose gradient `#E11D48` $\rightarrow$ `#F43F5E`).

---

## 5. Web Portal & Downloads Page Specifications (`web/`)

* **Framework:** Next.js with Tailwind CSS and Framer Motion transitions.
* **Layout:** Centered container (`max-w-6xl`), ambient radial glow backdrops (`rose-600/15` and `indigo-600/10`).
* **Download Cards:**
  1. **Featured Card: Official Windows Installer (`PunnyaHeartDesk-Setup.exe`):**
     * Prominent 2px Rose border with glow shadow.
     * Feature checkmarks: Windows 11 Fluent Mica Translucency, Live HUD Clock, Win32 Telemetry Hooks, Auto-Startup.
     * Big download button: `Download Windows Installer (v1.0.3)`.
  2. **Portable Windows Executable (`PunnyaHeartDesk.exe`):** Single-file portable direct binary.
  3. **Portable Windows Archive (`PunnyaHeartDesk-Windows.zip`):** Compressed archive.
  4. **macOS Editions:** Apple Silicon & Intel Universal DMG/Zip.
  5. **Android Attendance App:** APK with geolocation & office Wi-Fi shift beacon.

---

## 6. Build & Packaging Targets

* **Desktop Application:** Published single-file self-contained binary (`client/publish/win-x64/PunnyaHeartDesk.exe`).
* **Web Portal Static Export:** Static HTML/JS/CSS exported to `web/out/`.
* **Deployment Archive:** `punnya_task_portal.zip` containing `web/out/` with `.htaccess`, `index.php`, and `downloads/` assets for cPanel deployment.
