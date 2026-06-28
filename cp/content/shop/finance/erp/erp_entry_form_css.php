<?php
/**
 * ERP entry-form CSS — boxed field layout matching jewellery-industry style.
 *
 * Use: <?php include __DIR__ . '/erp_entry_form_css.php'; ?> once at the top
 * of any tab that renders a voucher / master-data entry form.
 *
 * Layout conventions (from Suntech jewellery screenshots):
 *   .ef-window        — outer window with title bar
 *   .ef-title          — window title (e.g. "Metal Stock Master")
 *   .ef-toolbar        — navigation / action icons row
 *   .ef-section        — bordered field group (e.g. "Header Details")
 *   .ef-row            — horizontal row of label+field pairs
 *   .ef-field           — single label + input pair
 *   .ef-tabs           — tabbed sub-sections (Metals | Stones | Others)
 *   .ef-grid           — line-items data grid
 *   .ef-totals         — summary totals block
 */
defined('_ASTEXE_') or die('No access');
?>
<style>
/* ── ERP Entry Form — boxed layout ── */
.ef-window{border:1px solid #8faabc;border-radius:3px;background:#f0f4f7;margin-bottom:16px;box-shadow:0 1px 4px rgba(0,0,0,.08)}
.ef-title{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);color:#fff;font-size:13px;font-weight:600;padding:5px 12px;border-bottom:1px solid #3d6a7d;letter-spacing:.3px}
.ef-toolbar{background:#d8e4ec;padding:4px 8px;border-bottom:1px solid #b8c8d4;display:flex;gap:4px;flex-wrap:wrap;align-items:center}
.ef-toolbar .btn{padding:2px 8px;font-size:11px}
.ef-body{padding:10px 12px}

/* Field sections */
.ef-section{border:1px solid #a8bcc8;border-radius:3px;padding:10px 12px;margin-bottom:10px;background:#fff;position:relative}
.ef-section-title{position:absolute;top:-9px;left:10px;background:#f0f4f7;padding:0 6px;font-size:11px;font-weight:600;color:#4a6a7a}
.ef-section.alt{background:#f8fbfd}

/* Field rows — horizontal label+input pairs */
.ef-row{display:flex;flex-wrap:wrap;gap:6px 14px;margin-bottom:6px;align-items:center}
.ef-field{display:flex;align-items:center;gap:4px;font-size:12px}
.ef-field label{font-weight:600;color:#2c4a5a;white-space:nowrap;margin:0;min-width:auto;font-size:11px}
.ef-field input,.ef-field select,.ef-field textarea{border:1px solid #8fb8cc;background:#eaf6fb;padding:2px 6px;font-size:12px;border-radius:2px;min-width:80px}
.ef-field input:focus,.ef-field select:focus{background:#fff;border-color:#3498db;outline:none}
.ef-field input[type="checkbox"]{min-width:auto;width:14px;height:14px}
.ef-field .ef-readonly{background:#e8e8e8;cursor:default}
.ef-field-wide input,.ef-field-wide select{min-width:180px}
.ef-field-narrow input,.ef-field-narrow select{min-width:60px;max-width:80px}

/* Checkbox row */
.ef-checks{display:flex;flex-wrap:wrap;gap:6px 16px;font-size:11px;margin:6px 0}
.ef-checks label{font-weight:normal;color:#3a5a6a;cursor:pointer;display:flex;align-items:center;gap:3px}

/* Tabs */
.ef-tabs{margin-top:8px}
.ef-tabs .nav-tabs>li>a{font-size:12px;padding:5px 14px;font-weight:600;color:#4a6a7a}
.ef-tabs .nav-tabs>li.active>a{color:#2c4a5a;border-bottom:2px solid #3498db}
.ef-tabs .tab-content{padding:10px 0}

/* Data grid */
.ef-grid{width:100%;border-collapse:collapse;font-size:11px;margin:6px 0}
.ef-grid th{background:#c8dce6;color:#2c4a5a;font-weight:600;padding:4px 6px;border:1px solid #a8c0cc;text-align:left;font-size:11px;white-space:nowrap}
.ef-grid td{padding:3px 6px;border:1px solid #c8d8e0;background:#fff}
.ef-grid td input,.ef-grid td select{border:1px solid #b8d0dc;background:#eaf6fb;padding:1px 4px;font-size:11px;width:100%;box-sizing:border-box}
.ef-grid tbody tr:hover td{background:#f0f8ff}
.ef-grid tfoot td{background:#e0ecf2;font-weight:600}

/* Totals block */
.ef-totals{display:flex;flex-wrap:wrap;gap:4px 0;justify-content:flex-end;margin-top:8px}
.ef-totals .ef-tot-row{display:flex;align-items:center;width:280px;justify-content:space-between;border-bottom:1px dotted #ccc;padding:2px 0}
.ef-totals .ef-tot-row label{font-size:11px;font-weight:600;color:#4a6a7a;margin:0}
.ef-totals .ef-tot-row span,.ef-totals .ef-tot-row input{font-size:12px;text-align:right;min-width:80px}

/* Image placeholder */
.ef-image-box{border:1px solid #a8bcc8;background:#f8f8f8;width:100px;height:80px;display:flex;align-items:center;justify-content:center;font-size:10px;color:#999}

/* Narration / remarks */
.ef-narration{width:100%;min-height:50px;border:1px solid #a8bcc8;background:#fff;padding:4px 6px;font-size:12px;resize:vertical}

/* Action buttons */
.ef-actions{display:flex;gap:6px;margin-top:10px;justify-content:flex-end}
.ef-actions .btn{font-size:12px;padding:4px 14px}

/* Status bar */
.ef-status{background:#e0ecf2;border-top:1px solid #b8c8d4;padding:3px 12px;font-size:11px;color:#4a6a7a;display:flex;justify-content:space-between}

/* Price matrix (right-side pricing table) */
.ef-price-matrix{border-collapse:collapse;font-size:11px}
.ef-price-matrix th,.ef-price-matrix td{border:1px solid #a8c0cc;padding:2px 6px}
.ef-price-matrix th{background:#c8dce6;font-weight:600}

/* Responsive */
@media(max-width:768px){
	.ef-row{flex-direction:column;gap:4px}
	.ef-field{flex-direction:column;align-items:flex-start}
	.ef-totals .ef-tot-row{width:100%}
}
</style>
