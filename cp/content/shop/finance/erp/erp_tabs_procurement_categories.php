<?php
defined('_ASTEXE_') or die('No access');
/**
 * Procurement categories & policies — category tree + per-category spending
 * policies (approval threshold + preferred vendor) that drive requisitions.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_proc_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$categories = epc_proc_categories($db_link, $companyId);
$policies = epc_proc_policies($db_link, $companyId);

erp_page_header(
    '<i class="fa fa-sitemap"></i> Categories & policies',
    'Procurement category tree and per-category spending policies — approval thresholds and preferred vendors that drive requisitions.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Procurement and sourcing'),
        array('label' => 'Categories & policies'),
    )
);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>
<div class="row">
    <div class="col-md-6">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New category</h5>
            <form id="epc_pc_cat" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="row">
                    <div class="col-xs-3 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" required></div>
                    <div class="col-xs-5 form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
                    <div class="col-xs-4 form-group"><label>Parent</label><select name="parent_id" class="form-control input-sm">
                        <option value="0">— top level —</option>
                        <?php foreach ($categories as $c): ?><option value="<?php echo (int) $c['id']; ?>"><?php echo epc_erp_h($c['code']); ?></option><?php endforeach; ?>
                    </select></div>
                </div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Default account</label><input type="text" name="default_account" class="form-control input-sm"></div>
                    <div class="col-xs-6 form-group"><label>&nbsp;</label><br><button class="btn btn-success btn-sm">Add category</button></div>
                </div>
            </form>
        </div>
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Code</th><th>Name</th><th>Parent</th><th>Account</th><th>Active</th></tr></thead>
            <tbody>
            <?php if (empty($categories)): ?><tr><td colspan="5" class="text-muted">No categories yet.</td></tr>
            <?php else: foreach ($categories as $c): $p = epc_proc_category_get($db_link, (int) $c['parent_id']); ?>
                <tr><td><strong><?php echo epc_erp_h($c['code']); ?></strong></td><td><?php echo epc_erp_h($c['name']); ?></td>
                <td><?php echo $p ? epc_erp_h($p['code']) : '—'; ?></td><td><?php echo epc_erp_h($c['default_account']); ?></td>
                <td><?php echo ((int) $c['active'] === 1) ? '<span class="label label-success">yes</span>' : '<span class="label label-default">no</span>'; ?></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div>
    <div class="col-md-6">
        <div class="well well-sm">
            <h5><i class="fa fa-gavel"></i> New policy</h5>
            <form id="epc_pc_pol" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Policy name</label><input type="text" name="name" class="form-control input-sm" required></div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Category</label><select name="category_id" class="form-control input-sm">
                        <option value="0">All categories (company default)</option>
                        <?php foreach ($categories as $c): ?><option value="<?php echo (int) $c['id']; ?>"><?php echo epc_erp_h($c['code'] . ' · ' . $c['name']); ?></option><?php endforeach; ?>
                    </select></div>
                    <div class="col-xs-6 form-group"><label>Approval threshold</label><input type="number" step="0.01" name="approval_threshold" class="form-control input-sm" value="0"></div>
                </div>
                <div class="form-group"><label>Preferred vendor</label><input type="text" name="preferred_vendor" class="form-control input-sm"></div>
                <button class="btn btn-success btn-sm">Add policy</button>
            </form>
            <p class="text-muted" style="font-size:11px;">Threshold 0 = auto-approve under this policy. Requisitions above the threshold for the matching category require approval before converting to a PO.</p>
        </div>
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Policy</th><th>Category</th><th class="text-right">Threshold</th><th>Vendor</th></tr></thead>
            <tbody>
            <?php if (empty($policies)): ?><tr><td colspan="4" class="text-muted">No policies yet.</td></tr>
            <?php else: foreach ($policies as $p): $cat = epc_proc_category_get($db_link, (int) $p['category_id']); ?>
                <tr><td><strong><?php echo epc_erp_h($p['name']); ?></strong></td><td><?php echo $cat ? epc_erp_h($cat['code']) : 'All'; ?></td>
                <td class="text-right"><?php echo epc_erp_money($p['approval_threshold'], 0); ?></td><td><?php echo epc_erp_h($p['preferred_vendor']); ?></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div>
</div>

<script>
(function(){
    var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
    function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
    function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
    function bind(id, action){ var f=document.getElementById(id); if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post(action, new FormData(f)).then(msg); }); }
    bind('epc_pc_cat', 'proc_category_save');
    bind('epc_pc_pol', 'proc_policy_save');
})();
</script>
