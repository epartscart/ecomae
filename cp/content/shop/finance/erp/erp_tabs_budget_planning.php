<?php
defined('_ASTEXE_') or die('No access');
/**
 * Budget planning & forecast positions — staged budget plans (draft -> review
 * -> approved -> published) with worksheet lines and planned headcount.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_budget_planning.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_bplan_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$summary = epc_bplan_summary($db_link, $companyId);

$tabBase = epc_erp_tab_url($erpUrl, 'budget_planning', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['bp']) ? (int) $_GET['bp'] : 0;

erp_page_header(
    '<i class="fa fa-line-chart"></i> Budget planning',
    'Staged budget plans with worksheet scenarios and forecast positions — draft, review, approve and publish to the budget register.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Budgeting'),
        array('label' => 'Budget planning'),
    )
);

erp_stat_cards(array(
    array('label' => 'Plans', 'value' => (string) $summary['plans']),
    array('label' => 'Draft', 'value' => (string) $summary['draft']),
    array('label' => 'In review', 'value' => (string) $summary['review']),
    array('label' => 'Approved', 'value' => (string) $summary['approved']),
    array('label' => 'Published', 'value' => (string) $summary['published']),
    array('label' => 'Published total', 'value' => epc_erp_money($summary['published_total'], 0)),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $plan = epc_bplan_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to plans</a></p>
    <?php if ($plan): $editable = in_array($plan['stage'], array('draft', 'review'), true);
        $lines = epc_bplan_lines($db_link, $detailId); $positions = epc_bplan_positions($db_link, $detailId); ?>
    <div class="row"><div class="col-md-4">
        <div class="panel panel-default">
            <div class="panel-heading"><strong><?php echo epc_erp_h($plan['name']); ?></strong>
                <span class="label label-default pull-right"><?php echo epc_erp_h($plan['stage']); ?></span></div>
            <div class="panel-body">
                <p>Fiscal year: <?php echo epc_erp_h($plan['fiscal_year']); ?></p>
                <p>Owner: <?php echo epc_erp_h($plan['owner']); ?></p>
                <p>Worksheet total: <strong><?php echo epc_erp_money(epc_bplan_total($db_link, $detailId), 2); ?></strong></p>
                <p>Position cost: <strong><?php echo epc_erp_money(epc_bplan_positions_cost($db_link, $detailId), 2); ?></strong></p>
                <?php if ((float) $plan['published_total'] > 0): ?><p>Published total: <strong><?php echo epc_erp_money($plan['published_total'], 2); ?></strong></p><?php endif; ?>
                <?php if ($plan['stage'] !== 'published'): ?>
                <hr>
                <button class="btn btn-primary btn-sm epc-bp-advance" data-id="<?php echo (int) $plan['id']; ?>">
                    <?php echo $plan['stage'] === 'approved' ? 'Publish plan' : 'Advance to ' . epc_erp_h(epc_bplan_stages()[array_search($plan['stage'], epc_bplan_stages(), true) + 1]); ?>
                </button>
                <?php endif; ?>
            </div>
        </div>
    </div><div class="col-md-8">
        <?php if ($editable): ?>
        <div class="well well-sm">
            <form id="epc_bp_line" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <input type="hidden" name="plan_id" value="<?php echo (int) $plan['id']; ?>">
                <div class="row">
                    <div class="col-xs-3 form-group"><label>Account</label><input type="text" name="account" class="form-control input-sm"></div>
                    <div class="col-xs-3 form-group"><label>Scenario</label><input type="text" name="scenario" class="form-control input-sm" value="base"></div>
                    <div class="col-xs-3 form-group"><label>Period</label><input type="text" name="period" class="form-control input-sm" placeholder="Q1"></div>
                    <div class="col-xs-3 form-group"><label>Amount</label><input type="number" step="0.01" name="amount" class="form-control input-sm" required></div>
                </div>
                <button class="btn btn-primary btn-sm">Add worksheet line</button>
            </form>
        </div>
        <?php endif; ?>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Account</th><th>Scenario</th><th>Period</th><th class="text-right">Amount</th></tr></thead>
            <tbody>
            <?php if (empty($lines)): ?><tr><td colspan="4" class="text-muted">No worksheet lines.</td></tr>
            <?php else: foreach ($lines as $l): ?>
                <tr><td><?php echo epc_erp_h($l['account']); ?></td><td><?php echo epc_erp_h($l['scenario']); ?></td><td><?php echo epc_erp_h($l['period']); ?></td><td class="text-right"><?php echo epc_erp_money($l['amount'], 2); ?></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>

        <?php if ($editable): ?>
        <div class="well well-sm">
            <form id="epc_bp_pos" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <input type="hidden" name="plan_id" value="<?php echo (int) $plan['id']; ?>">
                <div class="row">
                    <div class="col-xs-3 form-group"><label>Position title</label><input type="text" name="title" class="form-control input-sm" required></div>
                    <div class="col-xs-3 form-group"><label>Department</label><input type="text" name="department" class="form-control input-sm"></div>
                    <div class="col-xs-2 form-group"><label>Headcount</label><input type="number" name="headcount" class="form-control input-sm" value="1"></div>
                    <div class="col-xs-2 form-group"><label>Annual cost</label><input type="number" step="0.01" name="annual_cost" class="form-control input-sm"></div>
                    <div class="col-xs-2 form-group"><label>&nbsp;</label><br><button class="btn btn-primary btn-sm">Add position</button></div>
                </div>
            </form>
        </div>
        <?php endif; ?>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Position</th><th>Department</th><th class="text-right">Headcount</th><th class="text-right">Annual cost</th><th class="text-right">Total</th></tr></thead>
            <tbody>
            <?php if (empty($positions)): ?><tr><td colspan="5" class="text-muted">No forecast positions.</td></tr>
            <?php else: foreach ($positions as $p): ?>
                <tr><td><?php echo epc_erp_h($p['title']); ?></td><td><?php echo epc_erp_h($p['department']); ?></td><td class="text-right"><?php echo (int) $p['headcount']; ?></td>
                <td class="text-right"><?php echo epc_erp_money($p['annual_cost'], 2); ?></td><td class="text-right"><strong><?php echo epc_erp_money((int) $p['headcount'] * (float) $p['annual_cost'], 2); ?></strong></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else: $plans = epc_bplan_list($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New budget plan</h5>
            <form id="epc_bp_new" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Plan name</label><input type="text" name="name" class="form-control input-sm" required></div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Fiscal year</label><input type="text" name="fiscal_year" class="form-control input-sm" placeholder="2026"></div>
                    <div class="col-xs-6 form-group"><label>Owner</label><input type="text" name="owner" class="form-control input-sm"></div>
                </div>
                <button class="btn btn-success btn-sm">Create plan</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Plan</th><th>FY</th><th>Stage</th><th class="text-right">Published total</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($plans)): ?><tr><td colspan="5" class="text-muted">No budget plans yet.</td></tr>
            <?php else: foreach ($plans as $pl):
                $lbl = array('draft' => 'default', 'review' => 'warning', 'approved' => 'info', 'published' => 'success'); ?>
                <tr><td><strong><?php echo epc_erp_h($pl['name']); ?></strong></td><td><?php echo epc_erp_h($pl['fiscal_year']); ?></td>
                <td><span class="label label-<?php echo $lbl[$pl['stage']] ?? 'default'; ?>"><?php echo epc_erp_h($pl['stage']); ?></span></td>
                <td class="text-right"><?php echo epc_erp_money($pl['published_total'], 0); ?></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'bp=' . (int) $pl['id']); ?>">Open</a></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
<?php endif; ?>

<script>
(function(){
    var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
    var csrf = <?php echo json_encode($csrfLocal); ?>;
    function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
    function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
    function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
    bind('epc_bp_new', 'bplan_save');
    bind('epc_bp_line', 'bplan_line_add');
    bind('epc_bp_pos', 'bplan_position_add');
    document.querySelectorAll('.epc-bp-advance').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post('bplan_advance', fd).then(msg); }); });
})();
</script>
