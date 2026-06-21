<?php
defined('_ASTEXE_') or die('No access');
/**
 * Electronic reporting — configurable export formats (CSV/XML/JSON) with a
 * field map, plus a run log with output preview.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_elec_reporting.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_er_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;

$tabBase = epc_erp_tab_url($erpUrl, 'elec_reporting', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['fmt']) ? (int) $_GET['fmt'] : 0;

erp_page_header(
    '<i class="fa fa-file-code-o"></i> Electronic reporting',
    'Configure electronic reporting formats (CSV / XML / JSON) with field mappings, then generate structured exports. Each generation is logged.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Tax'),
        array('label' => 'Electronic reporting'),
    )
);
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $fmt = epc_er_format_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to formats</a></p>
    <?php if ($fmt): $fields = epc_er_fields($db_link, $detailId); $runs = epc_er_runs($db_link, $companyId, $detailId); ?>
    <h4><?php echo epc_erp_h($fmt['name']); ?> <small><?php echo epc_erp_h(strtoupper($fmt['output_type'])); ?></small></h4>
    <div class="row"><div class="col-md-5">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> Add field</h5>
            <form id="epc_er_field" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <input type="hidden" name="format_id" value="<?php echo (int) $fmt['id']; ?>">
                <div class="form-group"><label>Label (column / element)</label><input type="text" name="label" class="form-control input-sm" required></div>
                <div class="form-group"><label>Source key (data field)</label><input type="text" name="source_key" class="form-control input-sm" required></div>
                <button class="btn btn-success btn-sm">Add field</button>
            </form>
        </div>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>#</th><th>Label</th><th>Source key</th></tr></thead>
            <tbody>
            <?php if (empty($fields)): ?><tr><td colspan="3" class="text-muted">No fields yet.</td></tr>
            <?php else: foreach ($fields as $f): ?>
                <tr><td><?php echo (int) $f['ordinal']; ?></td><td><?php echo epc_erp_h($f['label']); ?></td><td><code><?php echo epc_erp_h($f['source_key']); ?></code></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div><div class="col-md-7">
        <h5>Run log</h5>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>#</th><th>Rows</th><th>Type</th><th>When</th></tr></thead>
            <tbody>
            <?php if (empty($runs)): ?><tr><td colspan="4" class="text-muted">No runs yet.</td></tr>
            <?php else: foreach ($runs as $r): ?>
                <tr><td><?php echo (int) $r['id']; ?></td><td><?php echo (int) $r['row_count']; ?></td><td><?php echo epc_erp_h(strtoupper($r['output_type'])); ?></td><td><?php echo epc_erp_h(date('Y-m-d H:i', (int) $r['time_created'])); ?></td></tr>
                <?php if (!empty($r['preview'])): ?><tr><td colspan="4"><pre style="max-height:160px;overflow:auto;font-size:11px;"><?php echo epc_erp_h($r['preview']); ?></pre></td></tr><?php endif; ?>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else: $formats = epc_er_formats($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New format</h5>
            <form id="epc_er_new" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="row">
                    <div class="col-xs-5 form-group"><label>Code</label><input type="text" name="code" class="form-control input-sm" required></div>
                    <div class="col-xs-7 form-group"><label>Name</label><input type="text" name="name" class="form-control input-sm" required></div>
                </div>
                <div class="form-group"><label>Output type</label><select name="output_type" class="form-control input-sm"><option value="csv">CSV</option><option value="xml">XML</option><option value="json">JSON</option></select></div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Root element</label><input type="text" name="root_element" class="form-control input-sm" value="rows"></div>
                    <div class="col-xs-6 form-group"><label>Row element</label><input type="text" name="row_element" class="form-control input-sm" value="row"></div>
                </div>
                <button class="btn btn-success btn-sm">Create format</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Code</th><th>Name</th><th>Type</th><th class="text-right">Fields</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($formats)): ?><tr><td colspan="5" class="text-muted">No formats yet.</td></tr>
            <?php else: foreach ($formats as $f): ?>
                <tr><td><strong><?php echo epc_erp_h($f['code']); ?></strong></td><td><?php echo epc_erp_h($f['name']); ?></td><td><?php echo epc_erp_h(strtoupper($f['output_type'])); ?></td>
                <td class="text-right"><?php echo count(epc_er_fields($db_link, (int) $f['id'])); ?></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'fmt=' . (int) $f['id']); ?>">Open</a></td></tr>
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
    bind('epc_er_new', 'er_format_save');
    bind('epc_er_field', 'er_field_add');
})();
</script>
