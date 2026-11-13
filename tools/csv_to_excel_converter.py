"""
Universal CSV to Formatted Excel Converter
Applies Punnya Heart Foundation Executive Design System (Segoe UI, #1E3A8A Deep Navy, #0F766E Teal, #CBD5E1 Borders).
"""

import os
import sys
import csv
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

class PunnyaExcelStyle:
    HEADER_FONT = Font(name="Segoe UI", size=11, bold=True, color="FFFFFF")
    HEADER_FILL = PatternFill(start_color="1E3A8A", end_color="1E3A8A", fill_type="solid") # Deep Navy
    
    REGULAR_FONT = Font(name="Segoe UI", size=10)
    BOLD_FONT = Font(name="Segoe UI", size=10, bold=True)
    
    # Priority Styles
    PRIORITY_STYLES = {
        "CRITICAL": (PatternFill(start_color="FEE2E2", end_color="FEE2E2", fill_type="solid"), Font(name="Segoe UI", size=10, bold=True, color="991B1B")),
        "HIGH": (PatternFill(start_color="FEF3C7", end_color="FEF3C7", fill_type="solid"), Font(name="Segoe UI", size=10, bold=True, color="92400E")),
        "MEDIUM": (PatternFill(start_color="DBEAFE", end_color="DBEAFE", fill_type="solid"), Font(name="Segoe UI", size=10, bold=True, color="1E40AF")),
        "LOW": (PatternFill(start_color="F1F5F9", end_color="F1F5F9", fill_type="solid"), Font(name="Segoe UI", size=10, color="475569")),
    }
    
    # Status / Proof Styles
    POSITIVE_FILL = PatternFill(start_color="D1FAE5", end_color="D1FAE5", fill_type="solid")
    POSITIVE_FONT = Font(name="Segoe UI", size=10, bold=True, color="065F46")
    
    WARNING_FILL = PatternFill(start_color="FFEDD5", end_color="FFEDD5", fill_type="solid")
    WARNING_FONT = Font(name="Segoe UI", size=10, bold=True, color="C2410C")
    
    THIN_BORDER = Border(
        left=Side(border_style="thin", color="CBD5E1"),
        right=Side(border_style="thin", color="CBD5E1"),
        top=Side(border_style="thin", color="CBD5E1"),
        bottom=Side(border_style="thin", color="CBD5E1")
    )
    
    ALIGN_CENTER = Alignment(horizontal="center", vertical="center", wrap_text=True)
    ALIGN_LEFT = Alignment(horizontal="left", vertical="center", wrap_text=True)
    ALIGN_RIGHT = Alignment(horizontal="right", vertical="center")


def convert_csv_to_excel(csv_path: str, excel_path: str = None, sheet_title: str = "Data") -> str:
    """
    Converts a single CSV file into an aesthetically formatted .xlsx file.
    """
    if not os.path.exists(csv_path):
        raise FileNotFoundError(f"Source CSV not found: {csv_path}")

    if excel_path is None:
        base, _ = os.path.splitext(csv_path)
        excel_path = f"{base}.xlsx"

    # Read CSV (supporting UTF-8 BOM, standard UTF-8, and fallback cp1252)
    rows = []
    for enc in ["utf-8-sig", "utf-8", "cp1252"]:
        try:
            with open(csv_path, mode="r", encoding=enc, errors="strict") as f:
                reader = csv.reader(f)
                rows = list(reader)
            break
        except UnicodeDecodeError:
            continue

    if not rows:
        raise ValueError(f"CSV file is empty or could not be read: {csv_path}")

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = sheet_title[:31] # Excel limits sheet names to 31 chars
    ws.views.sheetView[0].showGridLines = True

    header_row = rows[0]
    num_cols = len(header_row)

    # 1. Format Header
    ws.row_dimensions[1].height = 28
    for col_idx, h_text in enumerate(header_row, 1):
        cell = ws.cell(row=1, column=col_idx, value=h_text)
        cell.font = PunnyaExcelStyle.HEADER_FONT
        cell.fill = PunnyaExcelStyle.HEADER_FILL
        cell.alignment = PunnyaExcelStyle.ALIGN_CENTER
        cell.border = PunnyaExcelStyle.THIN_BORDER

    # Track column widths
    col_max_len = {i: len(str(h_text or "")) for i, h_text in enumerate(header_row, 1)}

    # 2. Populate Data Rows
    for row_idx, row_data in enumerate(rows[1:], 2):
        ws.row_dimensions[row_idx].height = 24
        for col_idx in range(1, num_cols + 1):
            val = row_data[col_idx - 1] if col_idx - 1 < len(row_data) else ""
            cell = ws.cell(row=row_idx, column=col_idx, value=val)
            cell.font = PunnyaExcelStyle.REGULAR_FONT
            cell.border = PunnyaExcelStyle.THIN_BORDER

            val_str = str(val).strip()
            val_upper = val_str.upper()

            # Smart formatting based on content
            if val_upper in PunnyaExcelStyle.PRIORITY_STYLES:
                fill, font = PunnyaExcelStyle.PRIORITY_STYLES[val_upper]
                cell.fill = fill
                cell.font = font
                cell.alignment = PunnyaExcelStyle.ALIGN_CENTER
            elif val_upper in ["YES", "COMPLETED", "VERIFIED", "APPROVED", "ACTIVE"]:
                cell.fill = PunnyaExcelStyle.POSITIVE_FILL
                cell.font = PunnyaExcelStyle.POSITIVE_FONT
                cell.alignment = PunnyaExcelStyle.ALIGN_CENTER
            elif val_upper in ["PENDING", "IN_PROGRESS", "REVIEW", "REJECTED"]:
                cell.fill = PunnyaExcelStyle.WARNING_FILL
                cell.font = PunnyaExcelStyle.WARNING_FONT
                cell.alignment = PunnyaExcelStyle.ALIGN_CENTER
            elif any(dt_sep in val_str for dt_sep in ["-", "/"]) and len(val_str) <= 19 and any(char.isdigit() for char in val_str):
                cell.alignment = PunnyaExcelStyle.ALIGN_CENTER
            elif val_str.replace(".", "", 1).isdigit():
                cell.alignment = PunnyaExcelStyle.ALIGN_RIGHT
            else:
                cell.alignment = PunnyaExcelStyle.ALIGN_LEFT

            if len(val_str) > col_max_len[col_idx]:
                col_max_len[col_idx] = len(val_str)

    # 3. Apply Column Widths
    for col_idx, max_l in col_max_len.items():
        col_letter = get_column_letter(col_idx)
        # Add safety margin, clamp between 12 and 75
        computed_width = max(13.0, min(float(max_l + 4), 75.0))
        ws.column_dimensions[col_letter].width = computed_width

    os.makedirs(os.path.dirname(os.path.abspath(excel_path)), exist_ok=True)
    wb.save(excel_path)
    return excel_path

if __name__ == "__main__":
    if len(sys.argv) > 1:
        target_csv = sys.argv[1]
        out_excel = sys.argv[2] if len(sys.argv) > 2 else None
        res = convert_csv_to_excel(target_csv, out_excel)
        print(f"[SUCCESS] Converted {target_csv} -> {res}")
    else:
        # Batch convert existing CSV files in the project
        csv_candidates = [
            r"d:\punnya heart\punnya task management system\tasks_saheer_khan.csv",
        ]
        for c in csv_candidates:
            if os.path.exists(c):
                out = convert_csv_to_excel(c)
                print(f"[SUCCESS] Converted {c} -> {out}")
