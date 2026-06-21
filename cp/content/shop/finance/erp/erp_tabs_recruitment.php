<?php
defined('_ASTEXE_') or die('No access');
/**
 * Recruitment — job requisitions and applicant pipeline (applied -> screening
 * -> interview -> offer -> hired/rejected). Hiring fills headcount.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

epc_hrt_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$summary = epc_hrt_summary($db_link, $companyId);

$tabBase = epc_erp_tab_url($erpUrl, 'recruitment', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';
$detailId = isset($_GET['job']) ? (int) $_GET['job'] : 0;

erp_page_header(
    '<i class="fa fa-user-plus"></i> Recruitment',
    'Job requisitions and applicant pipeline — applied, screening, interview, offer and hire. Hiring fills the requisition headcount.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Human resources'),
        array('label' => 'Recruitment'),
    )
);

erp_stat_cards(array(
    array('label' => 'Open jobs', 'value' => (string) $summary['open_jobs']),
    array('label' => 'Filled jobs', 'value' => (string) $summary['filled_jobs']),
    array('label' => 'Applicants', 'value' => (string) $summary['applicants']),
    array('label' => 'Hired', 'value' => (string) $summary['hired']),
));
?>
<div id="epc_erp_msg" class="alert" style="display:none;"></div>

<?php if ($detailId > 0): $job = epc_hrt_job_get($db_link, $detailId); ?>
    <p><a href="<?php echo epc_erp_h($tabBase); ?>">&laquo; Back to jobs</a></p>
    <?php if ($job): $applicants = epc_hrt_applicants($db_link, $detailId);
        $stages = array_merge(epc_hrt_applicant_stages(), array('rejected')); ?>
    <div class="row"><div class="col-md-4">
        <div class="panel panel-default">
            <div class="panel-heading"><strong><?php echo epc_erp_h($job['title']); ?></strong>
                <span class="label label-default pull-right"><?php echo epc_erp_h($job['status']); ?></span></div>
            <div class="panel-body">
                <p>Department: <?php echo epc_erp_h($job['department']); ?></p>
                <p>Hiring manager: <?php echo epc_erp_h($job['hiring_manager']); ?></p>
                <p>Headcount: <strong><?php echo (int) $job['hired']; ?> / <?php echo (int) $job['headcount']; ?></strong></p>
                <hr>
                <form id="epc_rec_app" class="form">
                    <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                    <input type="hidden" name="job_id" value="<?php echo (int) $job['id']; ?>">
                    <h5>Add applicant</h5>
                    <div class="form-group"><input type="text" name="name" class="form-control input-sm" placeholder="Name" required></div>
                    <div class="form-group"><input type="email" name="email" class="form-control input-sm" placeholder="Email"></div>
                    <div class="form-group"><input type="text" name="phone" class="form-control input-sm" placeholder="Phone"></div>
                    <button class="btn btn-success btn-sm">Add applicant</button>
                </form>
            </div>
        </div>
    </div><div class="col-md-8">
        <table class="table table-condensed table-bordered">
            <thead><tr><th>Applicant</th><th>Email</th><th>Stage</th><th>Move to</th></tr></thead>
            <tbody>
            <?php if (empty($applicants)): ?><tr><td colspan="4" class="text-muted">No applicants yet.</td></tr>
            <?php else: foreach ($applicants as $a): ?>
                <tr><td><strong><?php echo epc_erp_h($a['name']); ?></strong></td><td><?php echo epc_erp_h($a['email']); ?></td>
                <td><span class="label label-<?php echo $a['stage'] === 'hired' ? 'success' : ($a['stage'] === 'rejected' ? 'danger' : 'info'); ?>"><?php echo epc_erp_h($a['stage']); ?></span></td>
                <td><select class="form-control input-sm epc-rec-stage" data-id="<?php echo (int) $a['id']; ?>">
                    <?php foreach ($stages as $s): ?><option value="<?php echo epc_erp_h($s); ?>" <?php echo $s === $a['stage'] ? 'selected' : ''; ?>><?php echo epc_erp_h($s); ?></option><?php endforeach; ?>
                </select></td></tr>
            <?php endforeach; endif; ?>
            </tbody>
        </table>
    </div></div>
    <?php endif; ?>
<?php else: $jobs = epc_hrt_jobs($db_link, $companyId); ?>
    <div class="row"><div class="col-md-4">
        <div class="well well-sm">
            <h5><i class="fa fa-plus-circle"></i> New job requisition</h5>
            <form id="epc_rec_job" class="form">
                <input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
                <div class="form-group"><label>Title</label><input type="text" name="title" class="form-control input-sm" required></div>
                <div class="row">
                    <div class="col-xs-6 form-group"><label>Department</label><input type="text" name="department" class="form-control input-sm"></div>
                    <div class="col-xs-6 form-group"><label>Headcount</label><input type="number" name="headcount" class="form-control input-sm" value="1"></div>
                </div>
                <div class="form-group"><label>Hiring manager</label><input type="text" name="hiring_manager" class="form-control input-sm"></div>
                <button class="btn btn-success btn-sm">Create requisition</button>
            </form>
        </div>
    </div><div class="col-md-8">
        <table class="table table-bordered table-condensed">
            <thead><tr><th>Title</th><th>Department</th><th>Status</th><th class="text-right">Hired</th><th></th></tr></thead>
            <tbody>
            <?php if (empty($jobs)): ?><tr><td colspan="5" class="text-muted">No job requisitions yet.</td></tr>
            <?php else: foreach ($jobs as $jb):
                $lbl = array('open' => 'info', 'on_hold' => 'warning', 'filled' => 'success', 'closed' => 'default'); ?>
                <tr><td><strong><?php echo epc_erp_h($jb['title']); ?></strong></td><td><?php echo epc_erp_h($jb['department']); ?></td>
                <td><span class="label label-<?php echo $lbl[$jb['status']] ?? 'default'; ?>"><?php echo epc_erp_h($jb['status']); ?></span></td>
                <td class="text-right"><?php echo (int) $jb['hired']; ?>/<?php echo (int) $jb['headcount']; ?></td>
                <td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h($tabBase . $sep . 'job=' . (int) $jb['id']); ?>">Open</a></td></tr>
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
    bind('epc_rec_job', 'hrt_job_save');
    bind('epc_rec_app', 'hrt_applicant_add');
    document.querySelectorAll('.epc-rec-stage').forEach(function(s){ s.addEventListener('change', function(){ var fd=new FormData(); fd.append('csrf_guard_key',csrf); fd.append('id',s.getAttribute('data-id')); fd.append('stage',s.value); post('hrt_applicant_stage', fd).then(msg); }); });
})();
</script>
