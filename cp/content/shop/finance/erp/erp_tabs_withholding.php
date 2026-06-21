<?php
defined('_ASTEXE_') or die('No access');
/**
 * Withholding tax — codes (rate + account), apply on vendor payments, issue
 * certificates and settle to the authority. Rates are tenant-configured.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_wht_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$codes = epc_wht_codes($db_link, $companyId);
$txns = epc_wht_txns($db_link, $companyId);
$summary = epc_wht_summary($db_link, $companyId);

erp_page_header(
    '<i class="fa fa-scissors"></i> Withholding tax',
    'Define withholding tax codes, apply them on vendor payments, issue certificates and settle to the authority. Rates are configured per company.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Tax'),
        array('label' => 'Withholding tax'),
    )
);

erp_stat_cards(array(
    array('label' => 'Codes', 'value' => (string) $summary['codes']),
    array('label' => 'Transactions', 'value' => (string) $summary['txns']),
    array('label' => 'Accrued', 'value' => epc_erp_money($summary['accrued'], 0)),
    array('label' => 'Settled', 'value' => epc_erp_money($summary['settled'], 0)),
    array('label' => 'Total withheld', 'value' => epc_erp_money($summary['total_withheld'], 0)),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<div class="row">
    <div class="col-md-5">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New withholding code</h5>
            <form id="epc_wht_code" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="row">
                    <div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" required></div>
                    <div class="col-xs-7 form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
                </div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Rate %</label><input type="number" step="0.01" name="rate" class="form-control input-sm" required></div>
                    <div class="col-xs-6 form-group"><label>GL account</label><input type="text" name="account" class="form-control input-sm"></div>
                </div>
                <button class="btn btn-success btn-sm">Save code</button>
            </form>
        </div>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Code</th><th>Name</th><th class="text-right">Rate</th></tr></thead>
            <tbody>
            <?php if (empty($codes)): ?><tr><td colspan="3" class="text-muted">No codes yet.</td></tr>
            <?php else: foreach ($codes as $c): ?>
                <tr><td><strong><?php echo epc_erp_h($c['code']); ?></strong></td><td><?php echo epc_erp_h($c['name']); ?></td><td class="text-right"><?php echo number_format((float) $c['rate'], 2); ?>%</td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div>
    <div class="col-md-7">
        <div class="well well-sm">
            <h5><i class="fa fa-money"></i> Apply withholding on a payment</h5>
            <form id="epc_wht_apply" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Code</label><select name="code_id" class="form-control input-sm" required>
                        <option value="">— select —</option>
                        <?php foreach ($codes as $c): ?><option value="<?php echo (int) $c['id']; ?>"><?php echo epc_erp_h($c['code'] . ' (' . number_format((float) $c['rate'], 2) . '%)'); ?></option><?php endforeach; ?>
                    </select></div>
                    <div class="col-xs-6 form-group"><label>Vendor</label><input type="text" name="vendor" class="form-control input-sm"></div>
                </div>
                <div class="row">
                    <div class="col-xs-5 form-group"><label>Doc ref</label><input type="text" name="doc_ref" class="form-control input-sm"></div>
                    <div class="col-xs-3 form-group"><label>Date</label><input type="date" name="txn_date" class="form-control input-sm"></div>
                    <div class="col-xs-4 form-group"><label>Base amount</label><input type="number" step="0.01" name="base_amount" class="form-control input-sm" required></div>
                </div>
                <button class="btn btn-primary btn-sm">Apply withholding</button>
            </form>
        </div>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Vendor</th><th>Ref</th><th class="text-right">Base</th><th class="text-right">Withheld</th><th>Certificate</th><th>Status</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($txns)): ?><tr><td colspan="7" class="text-muted">No withholding transactions yet.</td></tr>
            <?php else: foreach ($txns as $t): ?>
                <tr><td><?php echo epc_erp_h($t['vendor']); ?></td><td><?php echo epc_erp_h($t['doc_ref']); ?></td>
                <td class="text-right"><?php echo epc_erp_money($t['base_amount'], 2); ?></td>
                <td class="text-right"><strong><?php echo epc_erp_money($t['wht_amount'], 2); ?></strong></td>
                <td><?php echo $t['certificate_no'] !== '' ? epc_erp_h($t['certificate_no']) : '<button class="btn btn-xs btn-default epc-wht-cert" data-id="' . (int) $t['id'] . '">Issue</button>'; ?></td>
                <td><span class="label label-<?php echo $t['status'] === 'settled' ? 'success' : 'warning'; ?>"><?php echo epc_erp_h($t['status']); ?></span></td>
                <td><?php echo $t['status'] === 'accrued' ? '<button class="btn btn-xs btn-primary epc-wht-settle" data-id="' . (int) $t['id'] . '">Settle</button>' : ''; ?></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div>
</div>

<script>
(function(){
    var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
    var csrf = <?php echo json_encode($csrfLocal); ?>;
    function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
    function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
    function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
    function act(sel, action){ document.querySelectorAll(sel).forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post(action, fd).then(msg); }); }); }
    bind('epc_wht_code', 'wht_code_save');
    bind('epc_wht_apply', 'wht_record');
    act('.epc-wht-cert', 'wht_certificate');
    act('.epc-wht-settle', 'wht_settle');
})();
</script>
