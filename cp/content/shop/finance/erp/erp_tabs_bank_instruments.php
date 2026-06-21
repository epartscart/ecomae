<?php
defined('_ASTEXE_') or die('No access');
/**
 * Bank instruments — letters of credit, bank guarantees and SBLC with a status
 * lifecycle (draft -> issued -> amended/utilized -> expired/closed) + events.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_cft_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$summary = epc_cft_instrument_summary($db_link, $companyId);

$tabBase = epc_erp_tab_url($erpUrl, 'bank_instruments', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['in']) ? (int) $_GET['in'] : 0;
$typeLabels = array('lc' => 'Letter of credit', 'bg' => 'Bank guarantee', 'sblc' => 'Standby LC');

erp_page_header(
    '<i class="fa fa-certificate"></i> Bank instruments',
    'Letters of credit, bank guarantees and standby LCs with a full status lifecycle and event log. Outstanding exposure tracks open instruments.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Cash and bank management'),
        array('label' => 'Bank instruments'),
    )
);

erp_stat_cards(array(
    array('label' => 'Instruments', 'value' => (string) $summary['count']),
    array('label' => 'Issued', 'value' => (string) $summary['issued']),
    array('label' => 'Utilized', 'value' => (string) $summary['utilized']),
    array('label' => 'Closed', 'value' => (string) $summary['closed']),
    array('label' => 'Outstanding exposure', 'value' => epc_erp_money($summary['exposure'], 0)),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $inst = epc_cft_instrument_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to instruments</a></p>
    <?php if ($inst): $events = epc_cft_instr_events($db_link, $detailId);
        $trans = epc_cft_instr_transitions(); $next = $trans[$inst['status']] ?? array(); ?>
    <div class="row"><div class="col-md-5">
        <div class="panel panel-default">
            <div class="panel-heading"><strong><?php echo epc_erp_h($inst['ref']); ?></strong>
                <span class="label label-default pull-right"><?php echo epc_erp_h($inst['status']); ?></span></div>
            <div class="panel-body">
                <p>Type: <strong><?php echo epc_erp_h($typeLabels[$inst['type']] ?? $inst['type']); ?></strong></p>
                <p>Beneficiary: <?php echo epc_erp_h($inst['beneficiary']); ?></p>
                <p>Bank: <?php echo epc_erp_h($inst['bank']); ?></p>
                <p>Amount: <strong><?php echo epc_erp_money($inst['amount'], 2) . ' ' . epc_erp_h($inst['currency']); ?></strong></p>
                <p>Expiry: <?php echo epc_erp_h($inst['expiry_date']); ?></p>
                <?php if (!empty($next)): ?>
                <hr>
                <h5>Advance status</h5>
                <div class="form-group"><input type="text" id="epc_bi_detail" class="form-control input-sm" placeholder="Event detail (optional)"></div>
                <?php foreach ($next as $ns): ?>
                    <button class="btn btn-sm btn-<?php echo in_array($ns, array('cancelled', 'expired'), true) ? 'danger' : 'primary'; ?> epc-bi-status" data-id="<?php echo (int) $inst['id']; ?>" data-status="<?php echo epc_erp_h($ns); ?>"><?php echo epc_erp_h($ns); ?></button>
                <?php endforeach; ?>
                <?php endif; ?>
            </div>
        </div>
    </div><div class="col-md-7">
        <h5>Event log</h5>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Event</th><th>Detail</th><th class="text-right">Amount</th><th>When</th></tr></thead>
            <tbody>
            <?php foreach ($events as $e): ?>
                <tr><td><span class="label label-info"><?php echo epc_erp_h($e['event_type']); ?></span></td><td><?php echo epc_erp_h($e['detail']); ?></td>
                <td class="text-right"><?php echo (float) $e['amount'] > 0 ? epc_erp_money($e['amount'], 2) : '—'; ?></td>
                <td><?php echo epc_erp_h(date('Y-m-d H:i', (int) $e['time_created'])); ?></td></tr>
            <?php endforeach; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else: $instruments = epc_cft_instruments($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New instrument</h5>
            <form id="epc_bi_new" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Type</label><select name="type" class="form-control input-sm"><option value="lc">Letter of credit</option><option value="bg">Bank guarantee</option><option value="sblc">Standby LC</option></select></div>
                <div class="form-group"><label>Reference (optional)</label><input type="text" name="ref" class="form-control input-sm" placeholder="auto if blank"></div>
                <div class="form-group"><label>Beneficiary</label><input type="text" name="beneficiary" class="form-control input-sm"></div>
                <div class="form-group"><label>Bank</label><input type="text" name="bank" class="form-control input-sm"></div>
                <div class="row">
                    <div class="col-xs-7 form-group"><label>Amount</label><input type="number" step="0.01" name="amount" class="form-control input-sm" required></div>
                    <div class="col-xs-5 form-group"><label>Currency</label><input type="text" name="currency" class="form-control input-sm" placeholder="AED"></div>
                </div>
                <div class="form-group"><label>Expiry date</label><input type="date" name="expiry_date" class="form-control input-sm"></div>
                <button class="btn btn-success btn-sm">Create instrument</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Ref</th><th>Type</th><th>Beneficiary</th><th class="text-right">Amount</th><th>Status</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($instruments)): ?><tr><td colspan="6" class="text-muted">No instruments yet.</td></tr>
            <?php else: foreach ($instruments as $it):
                $lbl = array('draft' => 'default', 'issued' => 'info', 'amended' => 'primary', 'utilized' => 'warning', 'expired' => 'danger', 'closed' => 'success', 'cancelled' => 'default'); ?>
                <tr><td><strong><?php echo epc_erp_h($it['ref']); ?></strong></td><td><?php echo epc_erp_h(strtoupper($it['type'])); ?></td><td><?php echo epc_erp_h($it['beneficiary']); ?></td>
                <td class="text-right"><?php echo epc_erp_money($it['amount'], 0); ?></td>
                <td><span class="label label-<?php echo $lbl[$it['status']] ?? 'default'; ?>"><?php echo epc_erp_h($it['status']); ?></span></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'in=' . (int) $it['id']); ?>">Open</a></td></tr>
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
    bind('epc_bi_new', 'cft_instrument_save');
    document.querySelectorAll('.epc-bi-status').forEach(function(b){ b.addEventListener('click', function(){ var d=document.getElementById('epc_bi_detail'); var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); fd.append('status',b.getAttribute('data-status')); fd.append('detail', d?d.value:''); post('cft_instrument_status', fd).then(msg); }); });
})();
</script>
