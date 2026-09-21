import { User } from './types';

export interface ParsedImportTask {
  id: string;
  title: string;
  description: string;
  assignedTo: string;
  assignedToName: string;
  priority: 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
  dueDate?: string;
  deliverableRequired: boolean;
  selected: boolean;
  rawStaffHeader?: string;
}

export interface ParseResult {
  tasks: ParsedImportTask[];
  detectedFormat: 'matrix' | 'linear' | 'unknown';
  detectedFormatLabel: string;
  totalFound: number;
  unassignedCount: number;
  matchedStaffCount: number;
}

/**
 * Splits raw CSV/TSV text into an array of string rows
 */
export function parseDelimitedRows(text: string): string[][] {
  if (!text || !text.trim()) return [];

  // Detect delimiter (tab vs comma) from first few non-empty lines
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0);
  if (lines.length === 0) return [];

  const sample = lines.slice(0, 5).join('\n');
  const tabCount = (sample.match(/\t/g) || []).length;
  const commaCount = (sample.match(/,/g) || []).length;
  const delimiter = tabCount > commaCount ? '\t' : ',';

  const rows: string[][] = [];
  let currentRow: string[] = [];
  let currentCell = '';
  let insideQuotes = false;

  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    const nextChar = text[i + 1];

    if (char === '"') {
      if (insideQuotes && nextChar === '"') {
        currentCell += '"';
        i++;
      } else {
        insideQuotes = !insideQuotes;
      }
    } else if (char === delimiter && !insideQuotes) {
      currentRow.push(currentCell.trim());
      currentCell = '';
    } else if ((char === '\r' || char === '\n') && !insideQuotes) {
      if (char === '\r' && nextChar === '\n') {
        i++;
      }
      currentRow.push(currentCell.trim());
      if (currentRow.some((c) => c.length > 0)) {
        rows.push(currentRow);
      }
      currentRow = [];
      currentCell = '';
    } else {
      currentCell += char;
    }
  }

  if (currentCell.length > 0 || currentRow.length > 0) {
    currentRow.push(currentCell.trim());
    if (currentRow.some((c) => c.length > 0)) {
      rows.push(currentRow);
    }
  }

  return rows;
}

/**
 * Parses dates like DD-MM-YYYY, DD/MM/YYYY, YYYY-MM-DD into ISO string
 */
export function parseDateToIso(dateStr?: string): string | undefined {
  if (!dateStr || !dateStr.trim()) return undefined;
  const clean = dateStr.trim();

  // DD-MM-YYYY or DD/MM/YYYY
  const dmyMatch = clean.match(/^(\d{1,2})[-/](\d{1,2})[-/](\d{4})$/);
  if (dmyMatch) {
    const day = parseInt(dmyMatch[1], 10);
    const month = parseInt(dmyMatch[2], 10) - 1;
    const year = parseInt(dmyMatch[3], 10);
    const d = new Date(Date.UTC(year, month, day, 18, 30, 0));
    if (!isNaN(d.getTime())) return d.toISOString();
  }

  // YYYY-MM-DD
  const ymdMatch = clean.match(/^(\d{4})[-/](\d{1,2})[-/](\d{1,2})$/);
  if (ymdMatch) {
    const year = parseInt(ymdMatch[1], 10);
    const month = parseInt(ymdMatch[2], 10) - 1;
    const day = parseInt(ymdMatch[3], 10);
    const d = new Date(Date.UTC(year, month, day, 18, 30, 0));
    if (!isNaN(d.getTime())) return d.toISOString();
  }

  const parsed = new Date(clean);
  if (!isNaN(parsed.getTime())) return parsed.toISOString();
  return undefined;
}

/**
 * Fuzzy matches a staff column header or name to a User object
 */
export function matchUserByName(query: string, users: User[]): User | undefined {
  if (!query || !query.trim()) return undefined;
  const q = query.trim().toLowerCase();

  // Exact match
  const exact = users.find(
    (u) =>
      u.username?.toLowerCase() === q ||
      u.fullName?.toLowerCase() === q ||
      u.email?.toLowerCase() === q
  );
  if (exact) return exact;

  // Substring / prefix match
  const partial = users.find(
    (u) =>
      (u.fullName && u.fullName.toLowerCase().includes(q)) ||
      (u.username && u.username.toLowerCase().includes(q)) ||
      (u.email && u.email.toLowerCase().includes(q)) ||
      q.includes(u.username?.toLowerCase() || '') ||
      q.includes(u.fullName?.toLowerCase() || '')
  );
  if (partial) return partial;

  return undefined;
}

const NON_STAFF_KEYWORDS = [
  'no',
  's.no',
  'sr',
  'sr no',
  'date',
  'day',
  'item',
  'task',
  'title',
  'project',
  'status',
  'stauts',
  'state',
  'remark',
  'remarks',
  'comment',
  'comments',
  'notes',
  'priority',
  'assignee',
  'assigned to',
  'staff',
  'scope',
  'description',
  'proof',
  'deliverable',
];

/**
 * Main parser: takes CSV/TSV text and system users, returns extracted tasks
 */
export function parseSheetToTasks(text: string, users: User[]): ParseResult {
  const rows = parseDelimitedRows(text);
  if (rows.length === 0) {
    return {
      tasks: [],
      detectedFormat: 'unknown',
      detectedFormatLabel: 'Empty or invalid format',
      totalFound: 0,
      unassignedCount: 0,
      matchedStaffCount: 0,
    };
  }

  // Find header row: look for row containing 'item', 'task', 'title', or 'project'
  let headerRowIdx = rows.findIndex((r) =>
    r.some((c) => {
      const lower = c.toLowerCase();
      return lower === 'item' || lower === 'task' || lower === 'title' || lower === 'project' || lower === 'task title';
    })
  );

  // Fallback: if no keyword match, assume row 0 is header
  if (headerRowIdx === -1) {
    headerRowIdx = 0;
  }

  const headers = rows[headerRowIdx].map((h) => h.trim());

  // Identify key column indices
  const itemColIdx = headers.findIndex((h) => {
    const l = h.toLowerCase();
    return l === 'item' || l === 'task' || l === 'title' || l === 'project' || l === 'task title' || l === 'project work';
  });

  const dateColIdx = headers.findIndex((h) => h.toLowerCase().includes('date'));
  const priorityColIdx = headers.findIndex((h) => h.toLowerCase().includes('priority'));
  const assigneeColIdx = headers.findIndex((h) => {
    const l = h.toLowerCase();
    return l === 'assignee' || l === 'assigned to' || l === 'staff' || l === 'assigned staff';
  });
  const descColIdx = headers.findIndex((h) => {
    const l = h.toLowerCase();
    return l === 'description' || l === 'scope' || l === 'responsibility' || l === 'remarks' || l === 'remark';
  });
  const deliverableColIdx = headers.findIndex((h) => {
    const l = h.toLowerCase();
    return l.includes('proof') || l.includes('deliverable');
  });

  // Check if this is a Team Matrix (e.g. columns for Ayush, Himanshu, Dhvaj, Divyanshu)
  const candidateStaffCols = headers
    .map((h, i) => ({ name: h, idx: i }))
    .filter((col) => {
      if (col.idx === itemColIdx || col.idx === dateColIdx || col.idx === priorityColIdx) return false;
      const l = col.name.toLowerCase();
      return !NON_STAFF_KEYWORDS.includes(l) && col.name.length > 0;
    });

  const isMatrix = candidateStaffCols.length > 0 && assigneeColIdx === -1;
  const tasks: ParsedImportTask[] = [];
  const matchedUserIds = new Set<string>();

  if (isMatrix) {
    // --- Parse Matrix Format ---
    for (let r = headerRowIdx + 1; r < rows.length; r++) {
      const row = rows[r];
      const projectTitle = itemColIdx >= 0 && itemColIdx < row.length ? row[itemColIdx].trim() : '';
      if (!projectTitle) continue;

      const dateVal = dateColIdx >= 0 && dateColIdx < row.length ? row[dateColIdx] : undefined;
      const dueDateIso = parseDateToIso(dateVal);

      for (const sc of candidateStaffCols) {
        if (sc.idx >= row.length) continue;
        const cellScope = row[sc.idx].trim();
        if (!cellScope) continue;

        // Skip non-actionable status words accidentally placed in staff cells
        const lowerScope = cellScope.toLowerCase();
        if (lowerScope === 'on going' || lowerScope === 'ongoing' || lowerScope === 'completed' || lowerScope === 'done') {
          // If the cell is just "On Going", skip or use generic scope
          continue;
        }

        const matched = matchUserByName(sc.name, users);
        if (matched) matchedUserIds.add(matched.id);

        tasks.push({
          id: `matrix_${r}_${sc.idx}_${Math.random().toString(36).substring(2, 7)}`,
          title: projectTitle,
          description: cellScope,
          assignedTo: matched ? matched.id : '',
          assignedToName: matched ? (matched.fullName || matched.username || matched.email) : sc.name,
          priority: 'MEDIUM',
          dueDate: dueDateIso,
          deliverableRequired: true,
          selected: true,
          rawStaffHeader: sc.name,
        });
      }
    }
  } else {
    // --- Parse Standard Linear Format ---
    for (let r = headerRowIdx + 1; r < rows.length; r++) {
      const row = rows[r];
      const title = (itemColIdx >= 0 ? row[itemColIdx] : row[0])?.trim() || '';
      if (!title) continue;

      const staffStr = (assigneeColIdx >= 0 ? row[assigneeColIdx] : '')?.trim() || '';
      const descStr = (descColIdx >= 0 ? row[descColIdx] : '')?.trim() || '';
      const priorityStr = (priorityColIdx >= 0 ? row[priorityColIdx] : '')?.trim().toUpperCase() || 'MEDIUM';
      const dateVal = dateColIdx >= 0 ? row[dateColIdx] : undefined;
      const deliverableVal = deliverableColIdx >= 0 ? row[deliverableColIdx]?.trim().toLowerCase() : 'yes';

      const validPriority = ['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'].includes(priorityStr)
        ? (priorityStr as 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL')
        : 'MEDIUM';

      const matched = matchUserByName(staffStr, users);
      if (matched) matchedUserIds.add(matched.id);

      tasks.push({
        id: `linear_${r}_${Math.random().toString(36).substring(2, 7)}`,
        title,
        description: descStr,
        assignedTo: matched ? matched.id : '',
        assignedToName: matched ? (matched.fullName || matched.username || matched.email) : staffStr,
        priority: validPriority,
        dueDate: parseDateToIso(dateVal),
        deliverableRequired: deliverableVal !== 'no' && deliverableVal !== 'false',
        selected: true,
        rawStaffHeader: staffStr,
      });
    }
  }

  const unassigned = tasks.filter((t) => !t.assignedTo).length;

  return {
    tasks,
    detectedFormat: isMatrix ? 'matrix' : 'linear',
    detectedFormatLabel: isMatrix
      ? `Team Responsibility Matrix (${candidateStaffCols.map((c) => c.name).join(', ')})`
      : 'Standard Task List',
    totalFound: tasks.length,
    unassignedCount: unassigned,
    matchedStaffCount: matchedUserIds.size,
  };
}

/**
 * Generates sample CSV text for downloading
 */
export function getSampleMatrixCsv(): string {
  return `No,Date,Item,Ayush,Himanshu,Dhvaj,Divyanshu,Status,Remarks
1,21-09-2026,Dr. Heart Vaccine - Urban Short Film,"Script, Production",Camera and editing,,,In Progress,
2,21-09-2026,Dr. Heart Vaccine - Rural Short Film,Story Board options,,,,,,
3,21-09-2026,NPPEHA LLM Videos,,,Development,Editing and finishing,In Progress,
4,21-09-2026,SAAOL Community Centre Video,To complete Hindi Narration,Video Editing,,,In Progress,
5,21-09-2026,Dr. Heart Prevention App,,,Development,Content support,In Progress,
6,21-09-2026,Punyya Website,,,Frontend & Backend,Content Support,In Progress,
7,21-09-2026,Dr Heart LLM,,,LLM Integration,Content Support,In Progress,`;
}

export function getSampleLinearCsv(): string {
  return `Project / Task Title,Assigned Staff,Scope / Description,Priority,Due Date,Proof Required
Dr. Heart Vaccine - Urban Short Film,Ayush,Script writing and production coordination,HIGH,2026-09-28,YES
Dr. Heart Vaccine - Urban Short Film,Himanshu,Camera capture and full 4K video editing,HIGH,2026-09-28,YES
Dr. Heart Prevention App,Dhvaj,Core mobile app development & API sync,CRITICAL,2026-10-05,YES
Dr. Heart Prevention App,Divyanshu,Health content review and UI support,MEDIUM,2026-10-05,YES
Punyya Website,Dhvaj,Production website updates and deployment,HIGH,2026-09-30,YES`;
}
