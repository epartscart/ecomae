<?php
/**
 * ERP → Shortcut Icons (legacy tab).
 * Same per-user store as ERP/CP dashboard custom shortcuts.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_shortcut_icons.php';

$uid = epc_shortcuts_user_id();
$rows = array();
if (isset($db_link) && $db_link instanceof PDO && $uid > 0) {
	$rows = epc_shortcuts_list_for_surface($db_link, $uid, 'erp');
}
$tiles = epc_shortcuts_as_tiles($rows);
$csrf = isset($csrf) ? (string) $csrf : '';
$ajax = isset($erpAjaxEndpoint) ? (string) $erpAjaxEndpoint : '';
if ($ajax === '' && function_exists('epc_erp_configure_portal_urls')) {
	$u = epc_erp_configure_portal_urls(
		(isset($epc_erp_portal) && $epc_erp_portal === 'frontend') ? 'frontend' : 'cp'
	);
	$ajax = (string) ($u['erpAjaxUrl'] ?? '');
}
?>
<style>
.si-wrap{max-width:1100px}
.si-head{display:flex;justify-content:space-between;align-items:flex-start;gap:12px;flex-wrap:wrap;margin:0 0 14px}
.si-head h2{margin:0;font-size:1.2rem}
.si-head p{margin:6px 0 0;color:#64748b;font-size:.9rem;max-width:52rem}
.si-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:12px}
.si-card{border:1px solid #e2e8f0;border-radius:12px;background:#fff;padding:14px;display:flex;gap:10px;align-items:flex-start;text-decoration:none;color:inherit;box-shadow:0 1px 2px rgba(15,23,42,.04)}
.si-card:hover{border-color:#94a3b8}
.si-ico{width:40px;height:40px;border-radius:10px;display:grid;place-items:center;font-size:1rem;flex:0 0 auto;color:#fff}
.si-meta{min-width:0;flex:1}
.si-meta strong{display:block;font-size:.92rem}
.si-meta span{display:block;font-size:.78rem;color:#64748b;word-break:break-all;margin-top:2px}
.si-del{border:0;background:#fee2e2;color:#991b1b;border-radius:8px;width:28px;height:28px;cursor:pointer;font-weight:800}
.si-form{margin-top:16px;border:1px solid #e2e8f0;border-radius:12px;background:#f8fafc;padding:14px}
.si-form h3{margin:0 0 10px;font-size:1rem}
.si-row{display:grid;grid-template-columns:1fr 1fr;gap:10px}
@media(max-width:720px){.si-row{grid-template-columns:1fr}}
.si-form label{display:block;font-size:.78rem;font-weight:700;color:#475569;margin:0 0 4px}
.si-form input{width:100%;border:1px solid #cbd5e1;border-radius:8px;padding:8px 10px;font:inherit;box-sizing:border-box}
.si-actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:12px}
.si-btn{border:0;border-radius:8px;padding:8px 14px;font-weight:700;cursor:pointer;background:#2563eb;color:#fff}
.si-btn.ghost{background:#e2e8f0;color:#0f172a}
.si-msg{margin:10px 0 0;font-size:.85rem;color:#0f766e;min-height:1.2em}
.si-empty{padding:20px;border:1px dashed #cbd5e1;border-radius:12px;color:#64748b;text-align:center}
</style>

<div class="si-wrap" id="siRoot" data-ajax="<?= htmlspecialchars($ajax, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?>" data-csrf="<?= htmlspecialchars($csrf, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?>">
  <div class="si-head">
    <div>
      <h2>Shortcut icons</h2>
      <p>Same shortcuts as your ERP dashboard Quick actions. Prefer editing there (Edit shortcuts → add/remove). Changes sync here.</p>
    </div>
    <a class="si-btn ghost" href="<?= htmlspecialchars((string) ($erpUrl ?? '/erp/'), ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?>" style="text-decoration:none;display:inline-flex;align-items:center">Open dashboard</a>
  </div>

  <div class="si-grid" id="siGrid">
    <?php if ($tiles === []): ?>
      <div class="si-empty" style="grid-column:1/-1">No shortcuts yet. Add below or use Edit shortcuts on the ERP dashboard.</div>
    <?php else: ?>
      <?php foreach ($tiles as $r): ?>
        <div class="si-card" data-id="<?= (int) ($r['id'] ?? 0) ?>">
          <div class="si-ico" style="background:<?= htmlspecialchars((string) ($r['color'] ?? '#2563eb'), ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?>"><i class="fa <?= htmlspecialchars((string) ($r['icon'] ?? 'fa-star'), ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?>"></i></div>
          <div class="si-meta">
            <strong><?= htmlspecialchars((string) ($r['label'] ?? ''), ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?></strong>
            <span><?= htmlspecialchars((string) ($r['url'] ?? ''), ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8') ?></span>
          </div>
          <button type="button" class="si-del" data-del="<?= (int) ($r['id'] ?? 0) ?>" title="Remove">×</button>
        </div>
      <?php endforeach; ?>
    <?php endif; ?>
  </div>

  <form class="si-form" id="siForm">
    <h3>Add shortcut</h3>
    <div class="si-row">
      <div>
        <label for="siLabel">Label</label>
        <input id="siLabel" name="label" required maxlength="100" placeholder="e.g. New GRN">
      </div>
      <div>
        <label for="siHref">Link</label>
        <input id="siHref" name="target_url" required maxlength="500" placeholder="/erp/?area=operations&amp;tab=inventory">
      </div>
      <div>
        <label for="siIcon">Icon class</label>
        <input id="siIcon" name="icon_class" maxlength="100" value="fa fa-star" placeholder="fa fa-star">
      </div>
      <div>
        <label for="siColor">Color</label>
        <input id="siColor" name="icon_color" maxlength="20" value="#2563eb" placeholder="#2563eb">
      </div>
    </div>
    <div class="si-actions">
      <button type="submit" class="si-btn">Add</button>
      <button type="button" class="si-btn ghost" id="siReset">Reset to defaults</button>
    </div>
    <p class="si-msg" id="siMsg" aria-live="polite"></p>
  </form>
</div>
<script>
(function () {
  var root = document.getElementById('siRoot');
  if (!root) return;
  var ajax = root.getAttribute('data-ajax') || '';
  var csrf = root.getAttribute('data-csrf') || '';
  var msg = document.getElementById('siMsg');
  function setMsg(t, ok) {
    if (!msg) return;
    msg.textContent = t || '';
    msg.style.color = ok === false ? '#b91c1c' : '#0f766e';
  }
  function post(action, extra) {
    var fd = new FormData();
    fd.append('action', action);
    fd.append('csrf_guard_key', csrf);
    fd.append('surface', 'erp');
    if (extra) Object.keys(extra).forEach(function (k) { fd.append(k, extra[k]); });
    return fetch(ajax, { method: 'POST', body: fd, credentials: 'same-origin' })
      .then(function (r) { return r.json(); });
  }
  document.getElementById('siForm').addEventListener('submit', function (e) {
    e.preventDefault();
    var label = document.getElementById('siLabel').value.trim();
    var href = document.getElementById('siHref').value.trim();
    var icon = document.getElementById('siIcon').value.trim() || 'fa fa-star';
    var color = document.getElementById('siColor').value.trim() || '#2563eb';
    post('shortcut_add', { label: label, target_url: href, icon_class: icon, icon_color: color }).then(function (j) {
      if (!j || !j.status) { setMsg((j && j.message) || 'Failed', false); return; }
      setMsg('Added — reloading…', true);
      location.reload();
    }).catch(function () { setMsg('Network error', false); });
  });
  root.addEventListener('click', function (e) {
    var t = e.target;
    if (!t || !t.getAttribute) return;
    var id = t.getAttribute('data-del');
    if (!id) return;
    if (!confirm('Remove this shortcut?')) return;
    post('shortcut_delete', { id: id }).then(function (j) {
      if (!j || !j.status) { setMsg((j && j.message) || 'Failed', false); return; }
      location.reload();
    });
  });
  document.getElementById('siReset').addEventListener('click', function () {
    if (!confirm('Reset ERP shortcuts to defaults?')) return;
    post('shortcut_reset', {}).then(function (j) {
      if (!j || !j.status) { setMsg((j && j.message) || 'Failed', false); return; }
      location.reload();
    });
  });
})();
</script>
