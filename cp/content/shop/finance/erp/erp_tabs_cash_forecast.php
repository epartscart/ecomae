<?php
defined('_ASTEXE_') or die('No access');
/**
 * Cash flow forecast — dated in/out lines projected to a running balance with
 * closing and minimum-balance visibility.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_cft_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;

$tabBase = epc_erp_tab_url($erpUrl, 'cash_forecast', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['fc']) ? (int) $_GET['fc'] : 0;

erp_page_header(
    '<i class="fa fa-area-chart"></i> Cash flow forecast',
    'Project cash position from an opening balance and dated inflows/outflows — running balance, closing and minimum balance.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Cash and bank management'),
        array('label' => 'Cash flow forecast'),
    )
);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $fc = epc_cft_forecast_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to forecasts</a></p>
    <?php if ($fc): $proj = epc_cft_projection($db_link, $detailId); ?>
    <?php erp_stat_cards(array(
        array('label' => 'Opening', 'value' => epc_erp_money($proj['opening'], 0)),
        array('label' => 'Total in', 'value' => epc_erp_money($proj['total_in'], 0)),
        array('label' => 'Total out', 'value' => epc_erp_money($proj['total_out'], 0)),
        array('label' => 'Closing', 'value' => epc_erp_money($proj['closing'], 0)),
        array('label' => 'Min balance', 'value' => epc_erp_money($proj['min_balance'], 0)),
    )); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> Add forecast line</h5>
            <form id="epc_cf_line" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <input type="hidden" name="forecast_id" value="<?php echo (int) $fc['id']; ?>">
                <div class="form-group"><label>Due date</label><input type="date" name="due_date" class="form-control input-sm" required></div>
                <div class="form-group"><label>Direction</label><select name="direction" class="form-control input-sm"><option value="in">Inflow</option><option value="out">Outflow</option></select></div>
                <div class="form-group"><label>Amount</label><input type="number" step="0.01" name="amount" class="form-control input-sm" required></div>
                <div class="form-group"><label>Category</label><input type="text" name="category" class="form-control input-sm" placeholder="AR / AP / Payroll"></div>
                <div class="form-group"><label>Source</label><input type="text" name="source" class="form-control input-sm"></div>
                <button class="btn btn-success btn-sm">Add line</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Date</th><th>Category</th><th>Source</th><th class="text-right">In</th><th class="text-right">Out</th><th class="text-right">Balance</th></tr></thead>
            <tbody>
            <tr class="active"><td colspan="5"><strong>Opening balance</strong></td><td class="text-right"><strong><?php echo epc_erp_money($proj['opening'], 2); ?></strong></td></tr>
            <?php if (empty($proj['rows'])): ?><tr><td colspan="6" class="text-muted">No forecast lines.</td></tr>
            <?php else: foreach ($proj['rows'] as $r): ?>
                <tr><td><?php echo epc_erp_h($r['due_date']); ?></td><td><?php echo epc_erp_h($r['category']); ?></td><td><?php echo epc_erp_h($r['source']); ?></td>
                <td class="text-right"><?php echo $r['direction'] === 'in' ? epc_erp_money($r['amount'], 2) : '—'; ?></td>
                <td class="text-right"><?php echo $r['direction'] === 'out' ? epc_erp_money($r['amount'], 2) : '—'; ?></td>
                <td class="text-right <?php echo (float) $r['running_balance'] < 0 ? 'text-danger' : ''; ?>"><strong><?php echo epc_erp_money($r['running_balance'], 2); ?></strong></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else: $forecasts = epc_cft_forecasts($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New forecast</h5>
            <form id="epc_cf_new" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
                <div class="row">
                    <div class="col-xs-7 form-group"><label>Opening balance</label><input type="number" step="0.01" name="opening_balance" class="form-control input-sm" value="0"></div>
                    <div class="col-xs-5 form-group"><label>Currency</label><input type="text" name="currency" class="form-control input-sm" placeholder="AED"></div>
                </div>
                <button class="btn btn-success btn-sm">Create forecast</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Forecast</th><th>Currency</th><th class="text-right">Opening</th><th class="text-right">Closing</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($forecasts)): ?><tr><td colspan="5" class="text-muted">No forecasts yet.</td></tr>
            <?php else: foreach ($forecasts as $f): $pr = epc_cft_projection($db_link, (int) $f['id']); ?>
                <tr><td><strong><?php echo epc_erp_h($f['name']); ?></strong></td><td><?php echo epc_erp_h($f['currency']); ?></td>
                <td class="text-right"><?php echo epc_erp_money($f['opening_balance'], 0); ?></td>
                <td class="text-right <?php echo $pr['closing'] < 0 ? 'text-danger' : ''; ?>"><?php echo epc_erp_money($pr['closing'], 0); ?></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'fc=' . (int) $f['id']); ?>">Open</a></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
<?php endif; ?>

<script>
(function(){
    var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
    function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
    function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
    function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
    bind('epc_cf_new', 'cft_forecast_save');
    bind('epc_cf_line', 'cft_line_add');
})();
</script>
