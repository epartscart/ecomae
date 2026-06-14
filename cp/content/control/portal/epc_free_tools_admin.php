<?php
/**
 * BOC — Free Tools control. Operator view of the public Free Tools tier:
 * usage metrics (registered users, active users, per-tool & per-country counts,
 * recent signups) and an activate/deactivate switch for each tool. Deactivated
 * tools show "temporarily unavailable" to the public and skip compute.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_free_tools.php';

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
    echo '<div class="alert alert-warning">Free Tools control is available on <strong>BOC</strong> only.</div>';
    return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
    global $DP_Config;
    echo '<div class="alert alert-warning">Please <a href="/' . epc_boc_h((string) $DP_Config->backend_dir) . '/">log in to BOC</a>.</div>';
    return;
}

$h = 'epc_boc_h';
$catalog = epc_free_tools_catalog();
$disabled = epc_free_tools_disabled_map();
$stats = epc_free_tools_usage_stats();
$toolCounts = array();
if (!empty($stats['by_tool'])) {
    foreach ($stats['by_tool'] as $r) {
        $toolCounts[(string) $r['tool']] = (int) $r['c'];
    }
}
$backend = trim((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), '/');
$ajax = '/' . $backend . '/content/control/portal/ajax_epc_free_tools_admin.php';
$fmt = static function ($n) { return number_format((int) $n); };
?>
<div class="col-lg-12 epc-erp-shell" id="epc-free-tools-admin">
    <div class="hpanel"><div class="panel-body">
        <div style="background:linear-gradient(135deg,#7f1d1d,#dc2626);color:#fff;border-radius:12px;padding:18px;margin-bottom:16px">
            <h3 style="margin:0 0 6px;color:#fff"><i class="fa fa-magic"></i> Free Tools control</h3>
            <p style="margin:0;opacity:.92">Usage of the public free business tools, and on/off control for each tool. Country-driven; deactivating a tool shows visitors a "temporarily unavailable" notice.</p>
        </div>

        <?php if (empty($stats['ok'])): ?>
        <p class="text-muted">Usage data is not available yet (no free-tool database).</p>
        <?php else: ?>
        <div class="row" style="margin-bottom:6px">
            <?php
            $cards = array(
                array('Registered users', $fmt($stats['accounts']), 'fa-users', '#dc2626'),
                array('With password', $fmt($stats['with_password']), 'fa-key', '#2563eb'),
                array('Active (30 days)', $fmt($stats['active_30d']), 'fa-bolt', '#16a34a'),
                array('Saved results', $fmt($stats['saves']), 'fa-save', '#7c3aed'),
            );
            foreach ($cards as $c): ?>
            <div class="col-md-3 col-sm-6">
                <div style="border:1px solid #eee;border-radius:10px;padding:14px;margin-bottom:12px">
                    <div style="font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:#888"><i class="fa <?php echo $h($c[2] ? $c[2] : ''); ?>"></i> <?php echo $h($c[0]); ?></div>
                    <div style="font-size:24px;font-weight:800;color:<?php echo $h($c[3]); ?>"><?php echo $h($c[1]); ?></div>
                </div>
            </div>
            <?php endforeach; ?>
        </div>
        <?php endif; ?>

        <h4 style="margin:18px 0 8px"><i class="fa fa-toggle-on"></i> Tools &mdash; activate / deactivate</h4>
        <table class="table table-condensed table-striped" style="font-size:13px">
            <thead><tr><th>Tool</th><th>Tag</th><th class="text-right">Saved results</th><th>Status</th><th style="width:150px">Control</th></tr></thead>
            <tbody>
            <?php foreach ($catalog as $key => $meta):
                $isOff = !empty($disabled[$key]);
                $cnt = isset($toolCounts[$key]) ? $toolCounts[$key] : 0; ?>
                <tr data-eft-key="<?php echo $h($key); ?>">
                    <td><i class="fa <?php echo $h($meta['icon']); ?>"></i> <strong><?php echo $h($meta['name']); ?></strong></td>
                    <td><span class="label label-default"><?php echo $h($meta['tag'] ?? ''); ?></span></td>
                    <td class="text-right"><?php echo $fmt($cnt); ?></td>
                    <td class="eft-status">
                        <?php if ($isOff): ?><span class="label label-warning">Deactivated</span><?php else: ?><span class="label label-success">Active</span><?php endif; ?>
                    </td>
                    <td>
                        <button type="button" class="btn btn-xs <?php echo $isOff ? 'btn-success' : 'btn-warning'; ?> eft-toggle" data-key="<?php echo $h($key); ?>" data-active="<?php echo $isOff ? '1' : '0'; ?>">
                            <?php echo $isOff ? '<i class="fa fa-play"></i> Activate' : '<i class="fa fa-pause"></i> Deactivate'; ?>
                        </button>
                    </td>
                </tr>
            <?php endforeach; ?>
            </tbody>
        </table>

        <?php if (!empty($stats['by_country'])): ?>
        <h4 style="margin:22px 0 8px"><i class="fa fa-globe"></i> Registrations by country</h4>
        <table class="table table-condensed" style="font-size:13px;max-width:420px">
            <thead><tr><th>Country</th><th class="text-right">Users</th></tr></thead>
            <tbody>
            <?php foreach ($stats['by_country'] as $r): ?>
                <tr><td><?php echo $h($r['country'] !== '' ? $r['country'] : '—'); ?></td><td class="text-right"><?php echo $fmt($r['c']); ?></td></tr>
            <?php endforeach; ?>
            </tbody>
        </table>
        <?php endif; ?>

        <?php if (!empty($stats['recent'])): ?>
        <h4 style="margin:22px 0 8px"><i class="fa fa-clock-o"></i> Recent registrations</h4>
        <table class="table table-condensed table-striped" style="font-size:12px">
            <thead><tr><th>Email</th><th>Company</th><th>Country</th><th>Registered</th><th>Last seen</th><th class="text-right">Logins</th><th class="text-right">Tool uses</th></tr></thead>
            <tbody>
            <?php foreach ($stats['recent'] as $r): ?>
                <tr>
                    <td><?php echo $h($r['email'] ?? ''); ?></td>
                    <td><?php echo $h($r['company'] ?? ''); ?></td>
                    <td><?php echo $h($r['country'] ?? ''); ?></td>
                    <td class="text-muted"><?php echo $h(!empty($r['time_created']) ? date('Y-m-d', (int) $r['time_created']) : ''); ?></td>
                    <td class="text-muted"><?php echo $h(!empty($r['time_last_seen']) ? date('Y-m-d', (int) $r['time_last_seen']) : ''); ?></td>
                    <td class="text-right"><?php echo $fmt($r['login_count'] ?? 0); ?></td>
                    <td class="text-right"><?php echo $fmt($r['use_count'] ?? 0); ?></td>
                </tr>
            <?php endforeach; ?>
            </tbody>
        </table>
        <?php endif; ?>

        <p class="eft-admin-msg text-muted" style="min-height:18px"></p>
    </div></div>
</div>
<script>
(function(){
    var AJAX=<?php echo json_encode($ajax); ?>;
    var msg=document.querySelector('#epc-free-tools-admin .eft-admin-msg');
    document.querySelectorAll('#epc-free-tools-admin .eft-toggle').forEach(function(btn){
        btn.addEventListener('click',function(){
            var key=btn.getAttribute('data-key');
            var makeActive=btn.getAttribute('data-active')==='1';
            btn.disabled=true;if(msg){msg.className='eft-admin-msg text-muted';msg.textContent='Saving…';}
            var fd=new FormData();fd.append('tool',key);fd.append('active',makeActive?'1':'0');
            fetch(AJAX,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}).then(function(d){
                btn.disabled=false;
                if(!d||!d.ok){if(msg){msg.className='eft-admin-msg text-danger';msg.textContent=(d&&d.message)||'Could not update.';}return;}
                var row=btn.closest('tr');var st=row.querySelector('.eft-status');
                if(makeActive){
                    btn.setAttribute('data-active','0');btn.className='btn btn-xs btn-warning eft-toggle';btn.innerHTML='<i class="fa fa-pause"></i> Deactivate';
                    if(st)st.innerHTML='<span class="label label-success">Active</span>';
                }else{
                    btn.setAttribute('data-active','1');btn.className='btn btn-xs btn-success eft-toggle';btn.innerHTML='<i class="fa fa-play"></i> Activate';
                    if(st)st.innerHTML='<span class="label label-warning">Deactivated</span>';
                }
                if(msg){msg.className='eft-admin-msg text-success';msg.textContent=(d.message)||'Updated.';}
            }).catch(function(){btn.disabled=false;if(msg){msg.className='eft-admin-msg text-danger';msg.textContent='Network error.';}});
        });
    });
})();
</script>
