<?php
defined('_ASTEXE_') or die('No access');
/**
 * Report center (shared) — renders the inquiries/reports registered for the
 * current module. The module area is derived from the tab key (rc_<area>);
 * selecting a report (?rpt=key) runs it and renders a filterable, exportable
 * table.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_report_center.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$rcTab = isset($tab) ? (string) $tab : '';
$rcArea = strpos($rcTab, 'rc_') === 0 ? substr($rcTab, 3) : epc_erp_tab_to_area($rcTab);
$reports = epc_rc_reports_for($rcArea);
$activeKey = isset($_GET['rpt']) ? (string) $_GET['rpt'] : '';

$tabBase = epc_erp_tab_url($erpUrl, $rcTab, $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';

erp_page_header(
    '<i class="fa fa-table"></i> Reports & inquiries',
    'Standard reports and inquiries for this module. Run a report to view results, filter live and export to CSV.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Reports & inquiries'),
    )
);
?>
<div class="row">
    <div class="col-md-3">
        <div class="list-group">
            <?php if (empty($reports)): ?>
                <div class="list-group-item text-muted">No reports configured for this module yet.</div>
            <?php else: foreach ($reports as $r): ?>
                <a class="list-group-item <?php echo $activeKey === $r['key'] ? 'active' : ''; ?>" href="<?php echo epc_erp_h($tabBase . $sep . 'rpt=' . $r['key']); ?>">
                    <strong><?php echo epc_erp_h($r['name']); ?></strong>
                    <br><small class="text-muted"><?php echo epc_erp_h($r['desc']); ?></small>
                </a>
            <?php endforeach; endif; ?>
        </div>
    </div>
    <div class="col-md-9">
        <?php if ($activeKey === '' || !epc_rc_report_get($activeKey)): ?>
            <div class="alert alert-info">Select a report on the left to run it.</div>
        <?php else: $report = epc_rc_report_get($activeKey); $result = epc_rc_run($db_link, $activeKey, $companyId); ?>
            <div class="clearfix" style="margin-bottom:8px;">
                <h4 class="pull-left" style="margin:0;"><?php echo epc_erp_h($report['name']); ?></h4>
                <div class="pull-right">
                    <input type="text" id="epc_rc_filter" class="form-control input-sm" style="display:inline-block;width:180px;" placeholder="Filter rows…">
                    <button class="btn btn-sm btn-default" id="epc_rc_csv"><i class="fa fa-download"></i> CSV</button>
                </div>
            </div>
            <p class="text-muted"><?php echo (int) count($result['rows']); ?> record(s)</p>
            <div style="overflow:auto;">
            <table class="table table-condensed table-bordered table-hover" id="epc_rc_table">
                <thead><tr>
                    <?php foreach ($result['columns'] as $c): ?><th><?php echo epc_erp_h(ucwords(str_replace('_', ' ', $c))); ?></th><?php endforeach; ?>
                </tr></thead>
                <tbody>
                <?php if (empty($result['rows'])): ?>
                    <tr><td colspan="<?php echo max(1, count($result['columns'])); ?>" class="text-muted">No data.</td></tr>
                <?php else: foreach ($result['rows'] as $row): ?>
                    <tr><?php foreach ($result['columns'] as $c): ?><td><?php echo epc_erp_h((string) ($row[$c] ?? '')); ?></td><?php endforeach; ?></tr>
                <?php endforeach; endif; ?>
                </tbody>
            </table>
            </div>
        <?php endif; ?>
    </div>
</div>

<script>
(function(){
    var fi = document.getElementById('epc_rc_filter');
    var tbl = document.getElementById('epc_rc_table');
    if (fi && tbl) {
        fi.addEventListener('keyup', function(){
            var q = fi.value.toLowerCase();
            tbl.querySelectorAll('tbody tr').forEach(function(tr){
                tr.style.display = tr.textContent.toLowerCase().indexOf(q) > -1 ? '' : 'none';
            });
        });
    }
    var csv = document.getElementById('epc_rc_csv');
    if (csv && tbl) {
        csv.addEventListener('click', function(){
            var rows = [];
            tbl.querySelectorAll('tr').forEach(function(tr){
                if (tr.style.display === 'none') return;
                var cells = [];
                tr.querySelectorAll('th,td').forEach(function(c){
                    var v = c.textContent.replace(/"/g,'""');
                    cells.push(/[",\n]/.test(v) ? '"'+v+'"' : v);
                });
                rows.push(cells.join(','));
            });
            var blob = new Blob([rows.join('\n')], {type:'text/csv'});
            var a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = 'report.csv';
            a.click();
        });
    }
})();
</script>
