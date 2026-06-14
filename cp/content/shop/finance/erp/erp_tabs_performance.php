<?php
defined('_ASTEXE_') or die('No access');
/**
 * Performance management — reviews with weighted goals and a finalized
 * weighted overall rating.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_hrt_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$summary = epc_hrt_summary($db_link, $companyId);

$tabBase = epc_erp_tab_url($erpUrl, 'performance', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['rev']) ? (int) $_GET['rev'] : 0;

erp_page_header(
    '<i class="fa fa-star-half-o"></i> Performance management',
    'Performance reviews with weighted goals and a finalized weighted overall rating.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Human resources'),
        array('label' => 'Performance management'),
    )
);

erp_stat_cards(array(
    array('label' => 'Open reviews', 'value' => (string) $summary['reviews_open']),
    array('label' => 'Completed reviews', 'value' => (string) $summary['reviews_done']),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $rev = epc_hrt_review_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to reviews</a></p>
    <?php if ($rev): $goals = epc_hrt_goals($db_link, $detailId); $editable = $rev['status'] !== 'completed'; ?>
    <div class="row"><div class="col-md-4">
        <div class="panel panel-default">
            <div class="panel-heading"><strong><?php echo epc_erp_h($rev['employee_name']); ?></strong>
                <span class="label label-default pull-right"><?php echo epc_erp_h($rev['status']); ?></span></div>
            <div class="panel-body">
                <p>Period: <?php echo epc_erp_h($rev['period']); ?></p>
                <p>Reviewer: <?php echo epc_erp_h($rev['reviewer']); ?></p>
                <p>Current weighted rating: <strong><?php echo number_format(epc_hrt_review_weighted_rating($db_link, $detailId), 2); ?></strong> / 5</p>
                <?php if ((float) $rev['overall_rating'] > 0): ?><p>Final overall: <strong><?php echo number_format((float) $rev['overall_rating'], 2); ?></strong> / 5</p><?php endif; ?>
                <?php if ($editable): ?>
                <hr>
                <button class="btn btn-primary btn-sm epc-perf-finalize" data-id="<?php echo (int) $rev['id']; ?>">Finalize review</button>
                <?php endif; ?>
            </div>
        </div>
    </div><div class="col-md-8">
        <?php if ($editable): ?>
        <div class="well well-sm">
            <form id="epc_perf_goal" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <input type="hidden" name="review_id" value="<?php echo (int) $rev['id']; ?>">
                <div class="row">
                    <div class="col-xs-5 form-group"><label>Goal</label><input type="text" name="title" class="form-control input-sm" required></div>
                    <div class="col-xs-3 form-group"><label>Target</label><input type="text" name="target" class="form-control input-sm"></div>
                    <div class="col-xs-2 form-group"><label>Weight</label><input type="number" step="0.5" name="weight" class="form-control input-sm" value="1"></div>
                    <div class="col-xs-2 form-group"><label>Rating 0-5</label><input type="number" name="rating" class="form-control input-sm" value="0" min="0" max="5"></div>
                </div>
                <button class="btn btn-primary btn-sm">Add goal</button>
            </form>
        </div>
        <?php endif; ?>
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Goal</th><th>Target</th><th class="text-right">Weight</th><th class="text-right">Rating</th></tr></thead>
            <tbody>
            <?php if (empty($goals)): ?><tr><td colspan="4" class="text-muted">No goals yet.</td></tr>
            <?php else: foreach ($goals as $g): ?>
                <tr><td><?php echo epc_erp_h($g['title']); ?></td><td><?php echo epc_erp_h($g['target']); ?></td>
                <td class="text-right"><?php echo (float) $g['weight']; ?></td><td class="text-right"><strong><?php echo (int) $g['rating']; ?></strong></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else: $reviews = epc_hrt_reviews($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New review</h5>
            <form id="epc_perf_new" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Employee name</label><input type="text" name="employee_name" class="form-control input-sm" required></div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Period</label><input type="text" name="period" class="form-control input-sm" placeholder="H1-2026"></div>
                    <div class="col-xs-6 form-group"><label>Reviewer</label><input type="text" name="reviewer" class="form-control input-sm"></div>
                </div>
                <button class="btn btn-success btn-sm">Create review</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Employee</th><th>Period</th><th>Status</th><th class="text-right">Overall</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($reviews)): ?><tr><td colspan="5" class="text-muted">No reviews yet.</td></tr>
            <?php else: foreach ($reviews as $rv):
                $lbl = array('draft' => 'default', 'in_progress' => 'warning', 'completed' => 'success'); ?>
                <tr><td><strong><?php echo epc_erp_h($rv['employee_name']); ?></strong></td><td><?php echo epc_erp_h($rv['period']); ?></td>
                <td><span class="label label-<?php echo $lbl[$rv['status']] ?? 'default'; ?>"><?php echo epc_erp_h($rv['status']); ?></span></td>
                <td class="text-right"><?php echo (float) $rv['overall_rating'] > 0 ? number_format((float) $rv['overall_rating'], 2) : '—'; ?></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'rev=' . (int) $rv['id']); ?>">Open</a></td></tr>
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
    bind('epc_perf_new', 'hrt_review_save');
    bind('epc_perf_goal', 'hrt_goal_add');
    document.querySelectorAll('.epc-perf-finalize').forEach(function(b){ b.addEventListener('click', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',b.getAttribute('data-id')); post('hrt_review_finalize', fd).then(msg); }); });
})();
</script>
