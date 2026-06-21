<?php
/** Marketing Broadcast — external CSS */
header('Content-Type: text/css; charset=utf-8');
header('Cache-Control: public, max-age=3600');
?>
.epc-mb-hub { --epc-mb-pink: #db2777; --epc-mb-blue: #2563eb; }
.epc-mb-hub .epc-mb-tabs { display: flex; flex-wrap: wrap; gap: 8px; margin: 16px 0 20px; }
.epc-mb-hub .epc-mb-tabs .btn { border-radius: 999px; }
.epc-mb-hub .epc-mb-tabs .btn-primary { background: linear-gradient(135deg, #db2777, #7c3aed); border: none; color: #fff; font-weight: 700; }
.epc-mb-hub .epc-mb-kpi { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 10px; margin-bottom: 18px; }
.epc-mb-hub .epc-mb-kpi__item { background: #fff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 12px; text-align: center; }
.epc-mb-hub .epc-mb-kpi__val { font-size: 22px; font-weight: 800; color: #db2777; }
.epc-mb-hub .epc-mb-kpi__label { font-size: 11px; color: #64748b; text-transform: uppercase; letter-spacing: .4px; }
.epc-mb-hub .epc-mb-panel { background: #fff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px 22px; margin-bottom: 16px; }
.epc-mb-hub .epc-mb-guide-step { border-left: 4px solid #db2777; padding: 12px 16px; margin: 12px 0; background: #fdf2f8; border-radius: 0 8px 8px 0; }
.epc-mb-hub .epc-mb-wa-links { background: #ecfdf5; border: 1px solid #86efac; border-radius: 10px; padding: 14px 18px; margin-bottom: 16px; }
.epc-mb-hub .epc-mb-form textarea.epc-mb-html-body { font-family: Consolas, monospace; font-size: 12px; }
