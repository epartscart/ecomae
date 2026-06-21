<?php
/**
 * BOC — Free Tools control. Operator view of the public Free Tools tier:
 * usage metrics (registered users, active users, per-tool & per-country counts,
 * recent signups) and an activate/deactivate switch for each tool. Deactivated
 * tools show "temporarily unavailable" to the public and skip compute.
 *
 * Rendered inside the BOC console shell (same chrome as the other Super CP
 * control pages) so inline scripts and the operator nav work consistently.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_kernel.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_boc_console.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_free_tools.php';

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
    echo '<div class="alert alert-warning">Free Tools control is available on <strong>BOC</strong> (Super CP) only.</div>';
    return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
    global $DP_Config;
    echo '<div class="alert alert-warning">Please <a href="/' . epc_boc_h((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp')) . '/">log in to BOC</a>.</div>';
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
$base = '/' . $backend;
$ajax = $base . '/content/control/portal/ajax_epc_free_tools_admin.php';
$fmt = static function ($n) { return number_format((int) $n); };
$operatorName = (class_exists('DP_User') && method_exists('DP_User', 'getName') && (string) DP_User::getName() !== '') ? (string) DP_User::getName() : 'Operator';
$opId = (class_exists('DP_User') && method_exists('DP_User', 'getUserId')) ? (int) DP_User::getUserId() : 0;
global $db_link;
$nav = function_exists('epc_boc_nav_for_user') ? epc_boc_nav_for_user($db_link, $opId) : epc_boc_nav();

epc_boc_console_open(array('active' => 'free_tools', 'title' => 'Free Tools', 'base' => $base, 'operator' => $operatorName, 'env' => 'Production', 'nav' => $nav, 'scope' => 'Public free tools'));
?>
<div id="epc-free-tools-admin" data-ajax="<?php echo $h($ajax); ?>">
    <div class="epc-boc__hero" style="background:linear-gradient(135deg,#7f1d1d,#dc2626);color:#fff;border-radius:14px;padding:20px 22px;margin-bottom:18px">
        <div style="font-size:11px;letter-spacing:1.5px;text-transform:uppercase;opacity:.85;margin-bottom:6px">Growth &amp; Marketing</div>
        <h2 style="margin:0 0 6px;color:#fff;font-weight:800"><i class="fa fa-magic"></i> Free Tools control</h2>
        <div style="opacity:.92">Usage of the public free business tools, and on/off control for each tool. Country-driven; deactivating a tool shows visitors a &ldquo;temporarily unavailable&rdquo; notice.</div>
    </div>

    <?php if (empty($stats['ok'])): ?>
    <div class="epc-boc__panel"><p style="margin:0;color:#64748b">Usage data is not available yet (no free-tool database).</p></div>
    <?php else: ?>
    <div class="epc-boc__kpis" style="display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-bottom:18px">
        <?php
        $cards = array(
            array('Registered users', $fmt($stats['accounts']), 'fa-users', '#dc2626'),
            array('With password', $fmt($stats['with_password']), 'fa-key', '#2563eb'),
            array('Active (30 days)', $fmt($stats['active_30d']), 'fa-bolt', '#16a34a'),
            array('Saved results', $fmt($stats['saves']), 'fa-save', '#7c3aed'),
        );
        foreach ($cards as $c): ?>
        <div style="background:#fff;border:1px solid #e3e8ef;border-radius:12px;padding:16px">
            <div style="font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:#8295b5"><i class="fa <?php echo $h($c[2]); ?>"></i> <?php echo $h($c[0]); ?></div>
            <div style="font-size:26px;font-weight:800;color:<?php echo $h($c[3]); ?>;margin-top:4px"><?php echo $h($c[1]); ?></div>
        </div>
        <?php endforeach; ?>
    </div>
    <?php endif; ?>

    <div class="epc-boc__panel" style="background:#fff;border:1px solid #e3e8ef;border-radius:12px;padding:18px;margin-bottom:18px">
        <h3 style="margin:0 0 12px;font-size:15px"><i class="fa fa-toggle-on"></i> Tools &mdash; activate / deactivate</h3>
        <table class="table" style="width:100%;font-size:13px;border-collapse:collapse">
            <thead><tr style="text-align:left;border-bottom:2px solid #e3e8ef"><th style="padding:8px">Tool</th><th style="padding:8px">Tag</th><th style="padding:8px;text-align:right">Saved results</th><th style="padding:8px">Status</th><th style="padding:8px;width:150px">Control</th></tr></thead>
            <tbody>
            <?php foreach ($catalog as $key => $meta):
                $isOff = !empty($disabled[$key]);
                $cnt = isset($toolCounts[$key]) ? $toolCounts[$key] : 0; ?>
                <tr data-eft-key="<?php echo $h($key); ?>" style="border-bottom:1px solid #eef1f5">
                    <td style="padding:8px"><i class="fa <?php echo $h($meta['icon']); ?>"></i> <strong><?php echo $h($meta['name']); ?></strong></td>
                    <td style="padding:8px"><span style="background:#eef1f5;border-radius:6px;padding:2px 8px;font-size:11px"><?php echo $h($meta['tag'] ?? ''); ?></span></td>
                    <td style="padding:8px;text-align:right"><?php echo $fmt($cnt); ?></td>
                    <td class="eft-status" style="padding:8px">
                        <?php if ($isOff): ?><span style="background:#fef3c7;color:#b45309;border-radius:6px;padding:2px 8px;font-size:11px;font-weight:700">Deactivated</span><?php else: ?><span style="background:#dcfce7;color:#0a7d3c;border-radius:6px;padding:2px 8px;font-size:11px;font-weight:700">Active</span><?php endif; ?>
                    </td>
                    <td style="padding:8px">
                        <button type="button" class="eft-toggle" data-key="<?php echo $h($key); ?>" data-active="<?php echo $isOff ? '1' : '0'; ?>" style="cursor:pointer;border:0;border-radius:8px;padding:6px 12px;font-size:12px;font-weight:700;color:#fff;background:<?php echo $isOff ? '#16a34a' : '#b45309'; ?>">
                            <?php echo $isOff ? 'Activate' : 'Deactivate'; ?>
                        </button>
                    </td>
                </tr>
            <?php endforeach; ?>
            </tbody>
        </table>
        <p class="eft-admin-msg" style="min-height:18px;margin:10px 0 0;color:#64748b"></p>
    </div>

    <?php if (!empty($stats['by_country'])): ?>
    <div class="epc-boc__panel" style="background:#fff;border:1px solid #e3e8ef;border-radius:12px;padding:18px;margin-bottom:18px;max-width:460px">
        <h3 style="margin:0 0 12px;font-size:15px"><i class="fa fa-globe"></i> Registrations by country</h3>
        <table style="width:100%;font-size:13px;border-collapse:collapse">
            <thead><tr style="text-align:left;border-bottom:2px solid #e3e8ef"><th style="padding:6px">Country</th><th style="padding:6px;text-align:right">Users</th></tr></thead>
            <tbody>
            <?php foreach ($stats['by_country'] as $r): ?>
                <tr style="border-bottom:1px solid #eef1f5"><td style="padding:6px"><?php echo $h($r['country'] !== '' ? $r['country'] : '—'); ?></td><td style="padding:6px;text-align:right"><?php echo $fmt($r['c']); ?></td></tr>
            <?php endforeach; ?>
            </tbody>
        </table>
    </div>
    <?php endif; ?>

    <?php if (!empty($stats['recent'])): ?>
    <div class="epc-boc__panel" style="background:#fff;border:1px solid #e3e8ef;border-radius:12px;padding:18px">
        <h3 style="margin:0 0 12px;font-size:15px"><i class="fa fa-clock-o"></i> Recent registrations</h3>
        <table style="width:100%;font-size:12px;border-collapse:collapse">
            <thead><tr style="text-align:left;border-bottom:2px solid #e3e8ef"><th style="padding:6px">Email</th><th style="padding:6px">Company</th><th style="padding:6px">Country</th><th style="padding:6px">Registered</th><th style="padding:6px">Last seen</th><th style="padding:6px;text-align:right">Logins</th><th style="padding:6px;text-align:right">Tool uses</th></tr></thead>
            <tbody>
            <?php foreach ($stats['recent'] as $r): ?>
                <tr style="border-bottom:1px solid #eef1f5">
                    <td style="padding:6px"><?php echo $h($r['email'] ?? ''); ?></td>
                    <td style="padding:6px"><?php echo $h($r['company'] ?? ''); ?></td>
                    <td style="padding:6px"><?php echo $h($r['country'] ?? ''); ?></td>
                    <td style="padding:6px;color:#64748b"><?php echo $h(!empty($r['time_created']) ? date('Y-m-d', (int) $r['time_created']) : ''); ?></td>
                    <td style="padding:6px;color:#64748b"><?php echo $h(!empty($r['time_last_seen']) ? date('Y-m-d', (int) $r['time_last_seen']) : ''); ?></td>
                    <td style="padding:6px;text-align:right"><?php echo $fmt($r['login_count'] ?? 0); ?></td>
                    <td style="padding:6px;text-align:right"><?php echo $fmt($r['use_count'] ?? 0); ?></td>
                </tr>
            <?php endforeach; ?>
            </tbody>
        </table>
    </div>
    <?php endif; ?>
</div>
<script>
(function(){
    var root=document.getElementById('epc-free-tools-admin');
    if(!root){return;}
    var AJAX=root.getAttribute('data-ajax');
    var msg=root.querySelector('.eft-admin-msg');
    root.querySelectorAll('.eft-toggle').forEach(function(btn){
        btn.addEventListener('click',function(){
            var key=btn.getAttribute('data-key');
            var makeActive=btn.getAttribute('data-active')==='1';
            btn.disabled=true;if(msg){msg.style.color='#64748b';msg.textContent='Saving…';}
            var fd=new FormData();fd.append('action','toggle');fd.append('tool',key);fd.append('active',makeActive?'1':'0');
            fetch(AJAX,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}).then(function(d){
                btn.disabled=false;
                if(!d||!d.ok){if(msg){msg.style.color='#c01829';msg.textContent=(d&&d.message)||'Could not update.';}return;}
                var row=btn.closest('tr');var st=row.querySelector('.eft-status');
                if(makeActive){
                    btn.setAttribute('data-active','0');btn.style.background='#b45309';btn.textContent='Deactivate';
                    if(st)st.innerHTML='<span style="background:#dcfce7;color:#0a7d3c;border-radius:6px;padding:2px 8px;font-size:11px;font-weight:700">Active</span>';
                }else{
                    btn.setAttribute('data-active','1');btn.style.background='#16a34a';btn.textContent='Activate';
                    if(st)st.innerHTML='<span style="background:#fef3c7;color:#b45309;border-radius:6px;padding:2px 8px;font-size:11px;font-weight:700">Deactivated</span>';
                }
                if(msg){msg.style.color='#0a7d3c';msg.textContent=(d.message)||'Updated.';}
            }).catch(function(){btn.disabled=false;if(msg){msg.style.color='#c01829';msg.textContent='Network error.';}});
        });
    });
})();
</script>
<?php
epc_boc_console_close();
?>
