<?php
/**
 * Shared customizable dashboard shortcuts UI (CP + ERP).
 *
 * Usage:
 *   require_once .../epc_dash_shortcuts_ui.php;
 *   echo epc_dash_shortcuts_render(array(
 *     'surface' => 'cp', // or erp
 *     'ajax_url' => $erpAjaxUrl,
 *     'csrf' => $csrf,
 *     'catalog' => $catalog,
 *     'items' => $tiles,
 *     'variant' => 'cp', // cp|erp styling
 *   ));
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_shortcut_icons.php';

/**
 * @param array{
 *   surface:string,
 *   ajax_url:string,
 *   csrf:string,
 *   catalog:array,
 *   items:array,
 *   variant?:string,
 *   title?:string
 * } $opts
 */
function epc_dash_shortcuts_render(array $opts): string
{
	$surface = in_array(($opts['surface'] ?? ''), array('cp', 'erp'), true) ? $opts['surface'] : 'cp';
	$variant = in_array(($opts['variant'] ?? $surface), array('cp', 'erp'), true) ? ($opts['variant'] ?? $surface) : 'cp';
	$ajax = (string) ($opts['ajax_url'] ?? '');
	$csrf = (string) ($opts['csrf'] ?? '');
	$catalog = is_array($opts['catalog'] ?? null) ? $opts['catalog'] : array();
	$items = is_array($opts['items'] ?? null) ? $opts['items'] : array();
	$title = trim((string) ($opts['title'] ?? 'Quick actions'));
	$uid = 'eds_' . $surface . '_' . substr(md5($ajax . $surface), 0, 6);

	$pinnedKeys = array();
	foreach ($items as $it) {
		$k = (string) ($it['key'] ?? '');
		if ($k !== '') {
			$pinnedKeys[$k] = true;
		}
	}

	$toneCycle = array('red', 'black', 'crimson', 'stone', 'red', 'black', 'crimson', 'stone');
	$erpToneCycle = array('qa-blue', 'qa-indigo', 'qa-amber', 'qa-teal', 'qa-green', 'qa-pink', 'qa-slate', 'qa-rust');

	ob_start();
	?>
<style id="epc-dash-shortcuts-css">
/* Shared Quick actions format (CP + ERP): tall gradient tiles, frosted icon top-left, label bottom-left */
.eds-wrap{margin:0 0 16px;}
.eds-head{display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap;margin:0 0 12px;}
.eds-head h4{margin:0;font-size:13px;font-weight:700;color:#0f172a;letter-spacing:.01em;}
.eds-head h4 .fa{margin-right:8px;color:#dc2626;}
.eds-actions{display:flex;gap:8px;flex-wrap:wrap;}
.eds-btn{display:inline-flex;align-items:center;gap:6px;padding:7px 13px;border-radius:8px;border:1px solid #cbd5e1;background:#fff;color:#0f172a;font-size:12px;font-weight:700;cursor:pointer;text-decoration:none!important;line-height:1.2;}
.eds-btn:hover{border-color:#dc2626;color:#b91c1c;}
.eds-btn--primary{background:#dc2626;border-color:#b91c1c;color:#fff!important;}
.eds-btn--primary:hover{background:#b91c1c;color:#fff!important;}
.eds-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(172px,1fr));gap:12px;}
.eds-item{position:relative;display:flex;flex-direction:column;align-items:flex-start;justify-content:space-between;gap:14px;padding:14px 14px 12px;border-radius:12px;text-decoration:none!important;color:#fff!important;min-height:118px;box-shadow:0 8px 22px rgba(0,0,0,.08);transition:transform .15s ease,box-shadow .15s ease;overflow:hidden;}
.eds-item:hover{transform:translateY(-2px);color:#fff!important;box-shadow:0 12px 28px rgba(0,0,0,.14);}
.eds-item__ic{width:40px;height:40px;border-radius:10px;display:inline-flex;align-items:center;justify-content:center;color:#fff;flex-shrink:0;background:rgba(255,255,255,.18);font-size:17px;}
.eds-item__lb{font-size:14px;font-weight:700;line-height:1.25;letter-spacing:.01em;}
.eds-item__rm{position:absolute;top:6px;right:6px;width:24px;height:24px;border:0;border-radius:999px;background:rgba(0,0,0,.28);color:#fff;font-size:15px;line-height:1;cursor:pointer;display:none;align-items:center;justify-content:center;padding:0;}
.eds-wrap.is-editing .eds-item__rm{display:inline-flex;}
.eds-item__rm:hover{background:#fee2e2;color:#b91c1c;}
.eds-item--red{background:linear-gradient(145deg,#dc2626 0%,#7f1d1d 100%);}
.eds-item--black{background:linear-gradient(145deg,#1c1917 0%,#0a0a0a 100%);}
.eds-item--stone{background:linear-gradient(145deg,#57534e 0%,#292524 100%);}
.eds-item--crimson{background:linear-gradient(145deg,#b91c1c 0%,#450a0a 100%);}
.eds-item--blue,.eds-item--qa-blue{background:linear-gradient(145deg,#2563eb 0%,#1e3a8a 100%);}
.eds-item--indigo,.eds-item--qa-indigo{background:linear-gradient(145deg,#4f46e5 0%,#312e81 100%);}
.eds-item--amber,.eds-item--qa-amber{background:linear-gradient(145deg,#d97706 0%,#92400e 100%);}
.eds-item--teal,.eds-item--qa-teal{background:linear-gradient(145deg,#0d9488 0%,#115e59 100%);}
.eds-item--green,.eds-item--qa-green{background:linear-gradient(145deg,#059669 0%,#064e3b 100%);}
.eds-item--pink,.eds-item--qa-pink{background:linear-gradient(145deg,#db2777 0%,#9d174d 100%);}
.eds-item--slate,.eds-item--qa-slate{background:linear-gradient(145deg,#475569 0%,#1e293b 100%);}
.eds-item--rust,.eds-item--qa-rust{background:linear-gradient(145deg,#b45309 0%,#7c2d12 100%);}
.eds-item--violet{background:linear-gradient(145deg,#7c3aed 0%,#4c1d95 100%);}
.eds-item--emerald{background:linear-gradient(145deg,#059669 0%,#064e3b 100%);}
.eds-item--rose{background:linear-gradient(145deg,#e11d48 0%,#9f1239 100%);}
.eds-empty{padding:16px;border:1px dashed #cbd5e1;border-radius:12px;color:#64748b;font-size:13px;background:#fff;}
.eds-panel{display:none;margin-top:14px;padding:14px;border:1px solid #e2e8f0;border-radius:12px;background:#fff;box-shadow:0 8px 24px rgba(15,23,42,.06);}
.eds-wrap.is-editing .eds-panel{display:block;}
.eds-panel__lead{margin:0 0 12px;font-size:12.5px;color:#475569;line-height:1.45;}
.eds-cat{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:8px;margin:0 0 14px;}
.eds-cat__btn{display:flex;align-items:center;gap:8px;padding:9px 10px;border-radius:8px;border:1px solid #e2e8f0;background:#f8fafc;cursor:pointer;font-size:12px;font-weight:600;color:#0f172a;text-align:left;}
.eds-cat__btn .fa{width:16px;text-align:center;color:#64748b;}
.eds-cat__btn.is-on{border-color:#86efac;background:#ecfdf5;color:#065f46;}
.eds-cat__btn.is-on .fa{color:#059669;}
.eds-cat__btn.is-on::after{content:"✓";margin-left:auto;color:#059669;font-weight:800;}
.eds-custom{display:grid;grid-template-columns:1.2fr 1.4fr auto;gap:8px;align-items:end;}
.eds-custom label{display:block;font-size:11px;font-weight:700;color:#64748b;margin:0 0 4px;text-transform:uppercase;letter-spacing:.04em;}
.eds-custom input{width:100%;height:34px;border:1px solid #cbd5e1;border-radius:8px;padding:0 10px;font-size:13px;}
.eds-msg{margin:8px 0 0;font-size:12px;font-weight:600;min-height:16px;}
.eds-msg.is-ok{color:#047857;}
.eds-msg.is-err{color:#b91c1c;}
@media (max-width:700px){
	.eds-custom{grid-template-columns:1fr;}
	.eds-grid{grid-template-columns:repeat(auto-fill,minmax(148px,1fr));gap:10px;}
	.eds-item{min-height:104px;}
}
/* CP accents only — same tile geometry as ERP */
.eds-wrap--cp{margin:0;}
.eds-wrap--cp .eds-head h4{color:#0a0a0a;}
.eds-wrap--cp .eds-head h4 .fa{color:#dc2626;}
.eds-wrap--cp .eds-btn--primary{background:#0a0a0a;border-color:#0a0a0a;}
.eds-wrap--cp .eds-btn--primary:hover{background:#dc2626;border-color:#b91c1c;}
.eds-wrap--cp .eds-panel{border-color:rgba(0,0,0,.1);box-shadow:0 8px 22px rgba(0,0,0,.07);}
/* ERP accents only — same tile geometry as CP */
.eds-wrap--erp{margin:0;}
.eds-wrap--erp .eds-head h4{color:inherit;}
.eds-wrap--erp .eds-head h4 .fa{color:#2563eb;}
.eds-wrap--erp .eds-btn--primary{background:#2563eb;border-color:#1d4ed8;}
.eds-wrap--erp .eds-btn--primary:hover{background:#1d4ed8;}
</style>
<div class="eds-wrap eds-wrap--<?php echo htmlspecialchars($variant, ENT_QUOTES, 'UTF-8'); ?>" id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>" data-surface="<?php echo htmlspecialchars($surface, ENT_QUOTES, 'UTF-8'); ?>">
	<div class="eds-head">
		<h4><i class="fa fa-bolt"></i> <?php echo htmlspecialchars($title, ENT_QUOTES, 'UTF-8'); ?></h4>
		<div class="eds-actions">
			<button type="button" class="eds-btn eds-btn--primary eds-edit-toggle"><i class="fa fa-pencil"></i> <span class="eds-edit-label">Edit shortcuts</span></button>
		</div>
	</div>
	<div class="eds-grid eds-live-grid">
		<?php if (empty($items)) { ?>
		<div class="eds-empty eds-empty-live">No shortcuts yet. Click <strong>Edit shortcuts</strong> to add the ones you use every day.</div>
		<?php } else {
			$i = 0;
			foreach ($items as $it) {
			$icon = htmlspecialchars((string) ($it['icon'] ?? 'fa-star'), ENT_QUOTES, 'UTF-8');
			$label = htmlspecialchars((string) ($it['label'] ?? 'Shortcut'), ENT_QUOTES, 'UTF-8');
			$url = htmlspecialchars((string) ($it['url'] ?? '#'), ENT_QUOTES, 'UTF-8');
			$id = (int) ($it['id'] ?? 0);
			$key = htmlspecialchars((string) ($it['key'] ?? ''), ENT_QUOTES, 'UTF-8');
			$tone = trim((string) ($it['tone'] ?? ''));
			if ($tone === '') {
				$tone = ($variant === 'erp')
					? $erpToneCycle[$i % count($erpToneCycle)]
					: $toneCycle[$i % count($toneCycle)];
			}
			$toneClass = preg_replace('/[^a-z0-9\-]/', '', strtolower($tone));
			?>
		<a class="eds-item eds-item--<?php echo htmlspecialchars($toneClass, ENT_QUOTES, 'UTF-8'); ?>" href="<?php echo $url; ?>" data-id="<?php echo $id; ?>" data-key="<?php echo $key; ?>">
			<span class="eds-item__ic"><i class="fa <?php echo $icon; ?>"></i></span>
			<span class="eds-item__lb"><?php echo $label; ?></span>
			<button type="button" class="eds-item__rm" title="Remove" aria-label="Remove shortcut">&times;</button>
		</a>
		<?php $i++; } } ?>
	</div>

	<div class="eds-panel" aria-label="Customize shortcuts">
		<p class="eds-panel__lead">Turn catalogue items on or off. Changes save immediately. You can also add a custom link below.</p>
		<div class="eds-cat">
			<?php foreach ($catalog as $ck => $citem) {
				$on = !empty($pinnedKeys[$ck]);
				$cIcon = htmlspecialchars(preg_replace('/^fa\s+/', '', (string) ($citem['icon'] ?? 'fa-star')), ENT_QUOTES, 'UTF-8');
				$cLabel = htmlspecialchars((string) ($citem['label'] ?? $ck), ENT_QUOTES, 'UTF-8');
				$cKey = htmlspecialchars((string) $ck, ENT_QUOTES, 'UTF-8');
				?>
			<button type="button" class="eds-cat__btn<?php echo $on ? ' is-on' : ''; ?>" data-key="<?php echo $cKey; ?>" data-label="<?php echo $cLabel; ?>" data-icon="<?php echo htmlspecialchars((string) ($citem['icon'] ?? 'fa fa-star'), ENT_QUOTES, 'UTF-8'); ?>" data-color="<?php echo htmlspecialchars((string) ($citem['color'] ?? '#2563eb'), ENT_QUOTES, 'UTF-8'); ?>" data-url="<?php echo htmlspecialchars((string) ($citem['url'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>">
				<i class="fa <?php echo $cIcon; ?>"></i> <?php echo $cLabel; ?>
			</button>
			<?php } ?>
		</div>
		<div class="eds-custom">
			<div>
				<label for="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_label">Custom label</label>
				<input id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_label" type="text" placeholder="e.g. Daily sales" maxlength="100">
			</div>
			<div>
				<label for="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_url">URL</label>
				<input id="<?php echo htmlspecialchars($uid, ENT_QUOTES, 'UTF-8'); ?>_url" type="text" placeholder="/cp/shop/orders/orders" maxlength="500">
			</div>
			<button type="button" class="eds-btn eds-btn--primary eds-add-custom"><i class="fa fa-plus"></i> Add</button>
		</div>
		<p class="eds-msg" aria-live="polite"></p>
		<div style="margin-top:10px;">
			<button type="button" class="eds-btn eds-reset"><i class="fa fa-undo"></i> Reset to defaults</button>
		</div>
	</div>
</div>
<script>
(function () {
	var root = document.getElementById(<?php echo json_encode($uid); ?>);
	if (!root) return;
	var endpoint = <?php echo json_encode($ajax); ?>;
	var csrf = <?php echo json_encode($csrf); ?>;
	var surface = <?php echo json_encode($surface); ?>;
	var msg = root.querySelector('.eds-msg');
	var grid = root.querySelector('.eds-live-grid');
	var editBtn = root.querySelector('.eds-edit-toggle');
	var editLabel = root.querySelector('.eds-edit-label');

	function setMsg(t, ok) {
		if (!msg) return;
		msg.textContent = t || '';
		msg.className = 'eds-msg' + (t ? (ok ? ' is-ok' : ' is-err') : '');
	}
	function post(action, fields) {
		var fd = new FormData();
		fd.append('action', action);
		fd.append('csrf_guard_key', csrf);
		fd.append('surface', surface);
		Object.keys(fields || {}).forEach(function (k) { fd.append(k, fields[k]); });
		return fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(function (r) { return r.json(); });
	}
	function reloadSoon() { setTimeout(function () { location.reload(); }, 280); }

	if (editBtn) {
		editBtn.addEventListener('click', function () {
			root.classList.toggle('is-editing');
			var on = root.classList.contains('is-editing');
			if (editLabel) editLabel.textContent = on ? 'Done' : 'Edit shortcuts';
		});
	}

	root.addEventListener('click', function (e) {
		var rm = e.target.closest('.eds-item__rm');
		if (rm) {
			e.preventDefault();
			e.stopPropagation();
			var item = rm.closest('.eds-item');
			var id = item ? item.getAttribute('data-id') : '';
			if (!id) return;
			post('shortcut_delete', { id: id }).then(function (j) {
				if (j && j.status) {
					item.parentNode.removeChild(item);
					setMsg('Shortcut removed', true);
					if (!grid.querySelector('.eds-item')) {
						grid.innerHTML = '<div class="eds-empty eds-empty-live">No shortcuts yet. Use the catalogue below to add some.</div>';
					}
				} else {
					setMsg((j && j.message) || 'Could not remove', false);
				}
			}).catch(function () { setMsg('Network error', false); });
			return;
		}
		var cat = e.target.closest('.eds-cat__btn');
		if (cat) {
			e.preventDefault();
			var key = cat.getAttribute('data-key') || '';
			var on = cat.classList.contains('is-on');
			if (on) {
				post('shortcut_delete_key', { shortcut_key: key }).then(function (j) {
					if (j && j.status) { cat.classList.remove('is-on'); setMsg('Removed', true); reloadSoon(); }
					else setMsg((j && j.message) || 'Failed', false);
				});
			} else {
				post('shortcut_add', {
					shortcut_key: key,
					label: cat.getAttribute('data-label') || key,
					icon_class: cat.getAttribute('data-icon') || 'fa fa-star',
					icon_color: cat.getAttribute('data-color') || '#2563eb',
					target_url: cat.getAttribute('data-url') || ''
				}).then(function (j) {
					if (j && j.status) { cat.classList.add('is-on'); setMsg('Added', true); reloadSoon(); }
					else setMsg((j && j.message) || 'Failed', false);
				});
			}
		}
	});

	var addCustom = root.querySelector('.eds-add-custom');
	if (addCustom) {
		addCustom.addEventListener('click', function () {
			var labelEl = document.getElementById(<?php echo json_encode($uid . '_label'); ?>);
			var urlEl = document.getElementById(<?php echo json_encode($uid . '_url'); ?>);
			var label = labelEl ? labelEl.value.trim() : '';
			var url = urlEl ? urlEl.value.trim() : '';
			if (!label || !url) { setMsg('Label and URL are required', false); return; }
			post('shortcut_add', {
				label: label,
				target_url: url,
				icon_class: 'fa fa-link',
				icon_color: '#475569'
			}).then(function (j) {
				if (j && j.status) { setMsg('Custom shortcut added', true); reloadSoon(); }
				else setMsg((j && j.message) || 'Failed', false);
			});
		});
	}

	var resetBtn = root.querySelector('.eds-reset');
	if (resetBtn) {
		resetBtn.addEventListener('click', function () {
			if (!window.confirm('Reset shortcuts for this dashboard to the defaults?')) return;
			post('shortcut_reset', {}).then(function (j) {
				if (j && j.status) { setMsg('Reset', true); reloadSoon(); }
				else setMsg((j && j.message) || 'Failed', false);
			});
		});
	}
})();
</script>
	<?php
	return ob_get_clean();
}
