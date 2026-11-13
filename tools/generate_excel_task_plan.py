import os
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

def build_styled_task_workbook(output_paths):
    wb = openpyxl.Workbook()

    # -------------------------------------------------------------
    # Styling Constants (Exact Match to Punya_Work_Tracker Design)
    # -------------------------------------------------------------
    header_font = Font(name="Segoe UI", size=11, bold=True, color="FFFFFF")
    header_fill = PatternFill(start_color="1E3A8A", end_color="1E3A8A", fill_type="solid") # Deep Navy

    kpi_header_font = Font(name="Segoe UI", size=10, bold=True, color="FFFFFF")
    kpi_header_fill = PatternFill(start_color="0F766E", end_color="0F766E", fill_type="solid") # Teal

    kpi_val_font = Font(name="Segoe UI", size=16, bold=True, color="0F766E")
    kpi_val_fill = PatternFill(start_color="F0FDFA", end_color="F0FDFA", fill_type="solid") # Soft Mint

    regular_font = Font(name="Segoe UI", size=10)
    bold_font = Font(name="Segoe UI", size=10, bold=True)
    section_font = Font(name="Segoe UI", size=12, bold=True, color="1E3A8A")
    title_font = Font(name="Segoe UI", size=14, bold=True, color="1E3A8A")
    sub_font = Font(name="Segoe UI", size=9, italic=True, color="64748B")

    thin_side = Side(border_style="thin", color="CBD5E1")
    cell_border = Border(left=thin_side, right=thin_side, top=thin_side, bottom=thin_side)

    align_center = Alignment(horizontal="center", vertical="center", wrap_text=True)
    align_left = Alignment(horizontal="left", vertical="center", wrap_text=True)
    align_right = Alignment(horizontal="right", vertical="center")

    # Priority Badges
    crit_fill = PatternFill(start_color="FEE2E2", end_color="FEE2E2", fill_type="solid")
    crit_font = Font(name="Segoe UI", size=10, bold=True, color="991B1B")

    high_fill = PatternFill(start_color="FEF3C7", end_color="FEF3C7", fill_type="solid")
    high_font = Font(name="Segoe UI", size=10, bold=True, color="92400E")

    med_fill = PatternFill(start_color="DBEAFE", end_color="DBEAFE", fill_type="solid")
    med_font = Font(name="Segoe UI", size=10, bold=True, color="1E40AF")

    proof_fill = PatternFill(start_color="D1FAE5", end_color="D1FAE5", fill_type="solid")
    proof_font = Font(name="Segoe UI", size=10, bold=True, color="065F46")

    # -------------------------------------------------------------
    # Sheet 1: Task Dispatch (Import Matrix)
    # -------------------------------------------------------------
    ws1 = wb.active
    ws1.title = "Task Dispatch"
    ws1.views.sheetView[0].showGridLines = True

    headers = ["Project / Task Title", "Assigned Staff", "Scope / Description", "Priority", "Due Date", "Proof Required"]
    ws1.row_dimensions[1].height = 28

    for col_idx, h in enumerate(headers, 1):
        cell = ws1.cell(row=1, column=col_idx, value=h)
        cell.font = header_font
        cell.fill = header_fill
        cell.alignment = align_center
        cell.border = cell_border

    tasks_data = [
        ("Design a CSR plan", "Saheer Khan", "Comprehensive CSR strategy and execution blueprint for Punnya Heart Foundation", "HIGH", "2026-11-15", "YES"),
        ("Develop Cause Marketing opportunities", "Saheer Khan", "Identify and structure cause marketing initiatives and brand alliances", "HIGH", "2026-11-20", "YES"),
        ("Develop a pitch deck for CSR with a detailed project", "Saheer Khan", "Executive presentation deck with detailed project roadmap and budget for CSR partners", "CRITICAL", "2026-11-25", "YES"),
        ("Create Brochures and social media posts", "Saheer Khan", "Marketing collateral including printed brochures and digital social media creatives", "MEDIUM", "2026-11-22", "YES"),
        ("Camp outlay and establish contacts with possible CSR partners", "Saheer Khan", "Plan health camp budget outlay and initiate outreach to prospective corporate CSR partners", "HIGH", "2026-11-28", "YES"),
        ("Detail the Branding opportunities for the CSR supporters", "Saheer Khan", "Catalog of corporate sponsor visibility and co-branding opportunities across camps and media", "MEDIUM", "2026-11-30", "YES"),
    ]

    for row_idx, row_vals in enumerate(tasks_data, 2):
        ws1.row_dimensions[row_idx].height = 26
        for col_idx, val in enumerate(row_vals, 1):
            cell = ws1.cell(row=row_idx, column=col_idx, value=val)
            cell.font = regular_font
            cell.border = cell_border
            
            if col_idx in [1, 3]:
                cell.alignment = align_left
            else:
                cell.alignment = align_center
                
            # Priority styling
            if col_idx == 4:
                if val == "CRITICAL":
                    cell.fill = crit_fill
                    cell.font = crit_font
                elif val == "HIGH":
                    cell.fill = high_fill
                    cell.font = high_font
                elif val == "MEDIUM":
                    cell.fill = med_fill
                    cell.font = med_font
            elif col_idx == 6:
                cell.fill = proof_fill
                cell.font = proof_font

    col_widths = {"A": 42.0, "B": 20.0, "C": 72.0, "D": 16.0, "E": 16.0, "F": 18.0}
    for col, width in col_widths.items():
        ws1.column_dimensions[col].width = width

    # -------------------------------------------------------------
    # Sheet 2: Executive Dispatch Overview
    # -------------------------------------------------------------
    ws2 = wb.create_sheet("Executive Dispatch Overview")
    ws2.views.sheetView[0].showGridLines = True

    ws2.cell(1, 1, "PUNNYA HEART FOUNDATION — CSR TASK DISPATCH & WORKFORCE ALLOCATION").font = title_font
    ws2.cell(2, 1, "Assignee: Saheer Khan | Organization: Punnya Heart Foundation | Dispatch Status: Ready for Import & Execution").font = sub_font

    # KPI Metric Cards
    kpi_cols = [
        (1, 2, "TOTAL TASKS ASSIGNED", "=COUNTA('Task Dispatch'!A2:A100)"),
        (3, 4, "CRITICAL PRIORITY", '=COUNTIF(\'Task Dispatch\'!D2:D100, "CRITICAL")'),
        (5, 6, "HIGH PRIORITY", '=COUNTIF(\'Task Dispatch\'!D2:D100, "HIGH")'),
        (7, 8, "DELIVERABLES REQUIRED", '=COUNTIF(\'Task Dispatch\'!F2:F100, "YES")')
    ]

    ws2.row_dimensions[4].height = 24
    ws2.row_dimensions[5].height = 36

    for start_col, end_col, title, formula in kpi_cols:
        ws2.merge_cells(start_row=4, start_column=start_col, end_row=4, end_column=end_col)
        h_cell = ws2.cell(4, start_col, title)
        h_cell.font = kpi_header_font
        h_cell.fill = kpi_header_fill
        h_cell.alignment = align_center
        h_cell.border = cell_border
        ws2.cell(4, end_col).border = cell_border

        ws2.merge_cells(start_row=5, start_column=start_col, end_row=5, end_column=end_col)
        v_cell = ws2.cell(5, start_col, formula)
        v_cell.font = kpi_val_font
        v_cell.fill = kpi_val_fill
        v_cell.alignment = align_center
        v_cell.border = cell_border
        ws2.cell(5, end_col).border = cell_border

    # Section 1: CSR Pillars & Strategic Objectives
    ws2.cell(8, 1, "1. CSR CAMPAIGN ROADMAP & STRATEGIC OBJECTIVES").font = section_font
    headers_sec1 = ["Pillar No", "Strategic Objective / Deliverable", "Lead Assignee", "Target Window", "Execution Scope & Impact"]
    ws2.row_dimensions[9].height = 26

    for idx, h in enumerate(headers_sec1, 1):
        c = ws2.cell(9, idx, h)
        c.font = header_font
        c.fill = header_fill
        c.alignment = align_center
        c.border = cell_border

    pillars = [
        ("Pillar 1", "CSR Master Strategy & Execution Blueprint", "Saheer Khan", "Nov 15, 2026", "Institutional CSR framework, compliance alignment, and prospective corporate funding model."),
        ("Pillar 2", "Cause Marketing Alliances & Synergies", "Saheer Khan", "Nov 20, 2026", "Corporate co-branding, joint public awareness campaigns, and heart health advocacy partnerships."),
        ("Pillar 3", "Executive CSR Pitch Deck & Financial Project Model", "Saheer Khan", "Nov 25, 2026", "Board-level presentation deck with detailed project outlay, budget forecasts, and ROI impact metrics."),
        ("Pillar 4", "Marketing Collateral (Brochures & Social Creatives)", "Saheer Khan", "Nov 22, 2026", "High-impact printed brochures, digital leaflets, and viral social media assets for heart screenings."),
        ("Pillar 5", "Cardiac Camp Outlay & Corporate Outreach Network", "Saheer Khan", "Nov 28, 2026", "Detailed camp expenditure layout, logistics coordination, and direct outreach to corporate CSR heads."),
        ("Pillar 6", "Corporate Sponsor Visibility & Branding Catalog", "Saheer Khan", "Nov 30, 2026", "Comprehensive multi-tier sponsor benefits matrix, stall branding, banner placement, and media credits."),
    ]

    for r_idx, row_data in enumerate(pillars, 10):
        ws2.row_dimensions[r_idx].height = 24
        for c_idx, val in enumerate(row_data, 1):
            c = ws2.cell(r_idx, c_idx, val)
            c.font = regular_font
            c.border = cell_border
            c.alignment = align_center if c_idx in [1, 3, 4] else align_left
            if c_idx == 1:
                c.font = bold_font

    widths_ws2 = {"A": 16.0, "B": 50.0, "C": 20.0, "D": 20.0, "E": 68.0, "F": 12.0, "G": 12.0, "H": 25.0}
    for col, width in widths_ws2.items():
        ws2.column_dimensions[col].width = width

    # Save to all requested destinations
    for p in output_paths:
        try:
            os.makedirs(os.path.dirname(os.path.abspath(p)), exist_ok=True)
            wb.save(p)
            print(f"[SUCCESS] Saved Excel Workbook to: {p}")
        except Exception as e:
            print(f"[ERROR] Could not save to {p}: {e}")

if __name__ == "__main__":
    destinations = [
        r"d:\punnya heart\punnya task management system\tasks_saheer_khan.xlsx",
        r"d:\punnya heart\punnya task management system\docs\tasks_saheer_khan.xlsx",
        os.path.join(os.path.expanduser("~"), "Documents", "Punya_Work_Tracker", "tasks_saheer_khan.xlsx"),
        os.path.join(os.path.expanduser("~"), "Desktop", "tasks_saheer_khan.xlsx")
    ]
    build_styled_task_workbook(destinations)
