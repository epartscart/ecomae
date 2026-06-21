<?php
defined('_ASTEXE_') or die('No access');
/**
 * Purchase requisitions — draft -> submit -> policy-driven approval -> convert
 * to PO. Front of the procure-to-pay flow.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_proc_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$summary = epc_proc_summary($db_link, $companyId);
$categories = epc_proc_categories($db_link, $companyId, true);

$tabBase = epc_erp_tab_url($erpUrl, 'purchase_requisitions', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['rq']) ? (int) $_GET['rq'] : 0;

erp_page_header(
    '<i class="fa fa-list-alt"></i> Purchase requisitions',
    'Internal purchase requests with policy-driven approval — draft, submit, approve and convert to a purchase order.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Procurement and sourcing'),
        array('label' => 'Purchase requisitions'),
    )
);

erp_stat_cards(array(
    array('label' => 'Draft', 'value' => (string) $summary['draft']),
    array('label' => 'Submitted', 'value' => (string) $summary['submitted']),
    array('label' => 'Approved', 'value' => (string) $summary['approved']),
    array('label' => 'Converted', 'value' => (string) $summary['converted']),
    array('label' => 'Rejected', 'value' => (string) $summary['rejected']),
    array('label' => 'Open value', 'value' => epc_erp_money($summary['open_value'], 0)),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $req = epc_proc_req_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to requisitions</a></p>
    <?php if ($req): $lines = epc_proc_req_lines($db_link, $detailId); $isDraft = $req['status'] === 'draft'; ?>
    <div class="row"><div class="col-md-5">
        <div class="panel panel-default">
            <div class="panel-heading"><strong><?php echo epc_erp_h($req['req_number']); ?></strong>
                <span class="label label-default pull-right"><?php echo epc_erp_h($req['status']); ?></span></div>
            <div class="panel-body">
                <p>Requester: <strong><?php echo epc_erp_h($req['requester']); ?></strong></p>
                <p>Total: <strong><?php echo epc_erp_money($req['total'], 2); ?></strong>
                   <?php if ((int) $req['requires_approval'] === 1): ?><span class="label label-warning">needs approval</span><?php else: ?><span class="label label-success">within policy</span><?php endif; ?></p>
                <?php if ($req['justification'] !== ''): ?><p class="text-muted"><?php echo epc_erp_h($req['justification']); ?></p><?php endif; ?>
                <?php if ($req['decided_by'] !== ''): ?><p>Decision by <?php echo epc_erp_h($req['decided_by']); ?>: <?php echo epc_erp_h($req['decision_note']); ?></p><?php endif; ?>
                <?php if ($req['po_ref'] !== ''): ?><p>Converted to <strong><?php echo epc_erp_h($req['po_ref']); ?></strong></p><?php endif; ?>
                <hr>
                <?php if ($isDraft): ?>
                <button class="btn btn-success btn-sm epc-pr-act" data-act="proc_req_submit" data-id="<?php echo (int) $req['id']; ?>">Submit requisition</button>
                <?php elseif ($req['status'] === 'submitted'): ?>
                <div class="form-group"><input type="text" id="epc_pr_note" class="form-control input-sm" placeholder="Decision note"></div>
                <button class="btn btn-success btn-sm epc-pr-decide" data-approve="1" data-id="<?php echo (int) $req['id']; ?>">Approve</button>
                <button class="btn btn-danger btn-sm epc-pr-decide" data-approve="0" data-id="<?php echo (int) $req['id']; ?>">Reject</button>
                <?php elseif ($req['status'] === 'approved'): ?>
                <button class="btn btn-primary btn-sm epc-pr-act" data-act="proc_req_convert" data-id="<?php echo (int) $req['id']; ?>">Convert to purchase order</button>
                <?php endif; ?>
            </div>
        </div>
    </div><div class="col-md-7">
        <?php if ($isDraft): ?>
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> Add line</h5>
            <form id="epc_pr_line" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <input type="hidden" name="req_id" value="<?php echo (int) $req['id']; ?>">
                <div class="row">
                    <div class="col-xs-4 form-group"><label>Category</label><select name="category_id" class="form-control input-sm">
                        <option value="0">—</option>
                        <?php foreach ($categories as $c): ?><option value="<?php echo (int) $c['id']; ?>"><?php echo epc_erp_h($c['code'] . ' · ' . $c['name']); ?></option><?php endforeach; ?>
                    </select></div>
                    <div class="col-xs-4 form-group"><label>Item code</label><input type="text" name="item_code" class="form-control input-sm"></div>
                    <div class="col-xs-4 form-group"><label>Description</label><input type="text" name="description" class="form-control input-sm"></div>
                </div>
                <div class="row">
                    <div class="col-xs-4 form-group"><label>Qty</label><input type="number" step="0.0001" name="qty" class="form-control input-sm" required></div>
                    <div class="col-xs-4 form-group"><label>Unit price</label><input type="number" step="0.01" name="unit_price" class="form-control input-sm" required></div>
                    <div class="col-xs-4 form-group"><label>&nbsp;</label><br><button class="btn btn-primary btn-sm">Add line</button></div>
                </div>
            </form>
        </div>
        <?php endif; ?>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Category</th><th>Item</th><th>Description</th><th class="text-right">Qty</th><th class="text-right">Price</th><th class="text-right">Total</th><th>Vendor</th></tr></thead>
            <tbody>
            <?php if (empty($lines)): ?><tr><td colspan="7" class="text-muted">No lines yet.</td></tr>
            <?php else: foreach ($lines as $l): $cat = epc_proc_category_get($db_link, (int) $l['category_id']); ?>
                <tr><td><?php echo $cat ? epc_erp_h($cat['code']) : '—'; ?></td><td><?php echo epc_erp_h($l['item_code']); ?></td><td><?php echo epc_erp_h($l['description']); ?></td>
                <td class="text-right"><?php echo (float) $l['qty']; ?></td><td class="text-right"><?php echo epc_erp_money($l['unit_price'], 2); ?></td>
                <td class="text-right"><strong><?php echo epc_erp_money($l['line_total'], 2); ?></strong></td><td><?php echo epc_erp_h($l['preferred_vendor']); ?></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else:
    $reqs = epc_proc_reqs($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New requisition</h5>
            <form id="epc_pr_new" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Requester</label><input type="text" name="requester" class="form-control input-sm" required></div>
                <div class="form-group"><label>Business unit</label><select name="business_unit_id" class="form-control input-sm">
                    <option value="0">—</option>
                    <?php foreach ($db_link->query("SELECT `id`,`name` FROM `epc_erp_pm_business_units` ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $bu): ?>
                        <option value="<?php echo (int) $bu['id']; ?>"><?php echo epc_erp_h($bu['name']); ?></option>
                    <?php endforeach; ?>
                </select></div>
                <div class="form-group"><label>Justification</label><textarea name="justification" class="form-control input-sm" rows="2"></textarea></div>
                <button class="btn btn-success btn-sm">Create draft</button>
            </form>
            <p class="text-muted" style="font-size:11px;">Add lines, then submit. Requisitions over the category policy threshold need approval before they can convert to a PO.</p>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Number</th><th>Requester</th><th>Status</th><th class="text-right">Total</th><th>PO ref</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($reqs)): ?><tr><td colspan="6" class="text-muted">No requisitions yet.</td></tr>
            <?php else: foreach ($reqs as $r):
                $lbl = array('draft' => 'default', 'submitted' => 'warning', 'approved' => 'info', 'rejected' => 'danger', 'converted' => 'success'); ?>
                <tr><td><strong><?php echo epc_erp_h($r['req_number']); ?></strong></td><td><?php echo epc_erp_h($r['requester']); ?></td>
                <td><span class="label label-<?php echo $lbl[$r['status']] ?? 'default'; ?>"><?php echo epc_erp_h($r['status']); ?></span></td>
                <td class="text-right"><?php echo epc_erp_money($r['total'], 2); ?></td><td><?php echo epc_erp_h($r['po_ref']); ?></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'rq=' . (int) $r['id']); ?>">Open</a></td></tr>
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
    bind('epc_pr_new', 'proc_req_save');
    bind('epc_pr_line', 'proc_req_add_line');
    document.querySelectorAll('.epc-pr-act').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post(b.getAttribute('data-act'), fd).then(msg); }); });
    document.querySelectorAll('.epc-pr-decide').forEach(function(b){ b.addEventListener('click', function(){ var n=document.getElementById('epc_pr_note'); var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); fd.append('approve',b.getAttribute('data-approve')); fd.append('note', n?n.value:''); post('proc_req_decision', fd).then(msg); }); });
})();
</script>
