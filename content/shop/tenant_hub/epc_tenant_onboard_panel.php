<?php
/**
 * Super CP — client onboard tab (intro form + launch checklist).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';
$onboardCountries = epc_countries_registration_options();

$editKey = isset($_GET['edit']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['edit'])) : '';
$editTenant = null;
$editIntro = epc_portal_intro_defaults();
if ($editKey !== '') {
	foreach ($tenants as $t) {
		if ($t['site_key'] === $editKey) {
			$editTenant = $t;
			$editIntro = $t['intro'];
			break;
		}
	}
}
$editCountryCode = 'AE';
if (!empty($editIntro['country_code'])) {
	$editCountryCode = strtoupper(substr((string) $editIntro['country_code'], 0, 2));
} elseif (!empty($editIntro['country'])) {
	$cc = epc_countries_normalize_code((string) $editIntro['country']);
	if ($cc !== '') {
		$editCountryCode = $cc;
	}
}

$checklistKey = isset($_GET['checklist']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['checklist'])) : '';
if ($checklistKey === '' && !empty($flash['site_key'])) {
	$checklistKey = (string) $flash['site_key'];
}
$checklist = $checklistKey !== '' ? epc_th_launch_checklist($db_link, $checklistKey) : null;
$guideSteps = epc_portal_onboard_guide_steps();
$introFields = epc_portal_intro_field_defs();
?>

<div class="epc-th-onboard">
<div class="epc-th-onboard-intro">
	<h4><i class="fa fa-rocket"></i> Onboard a new client — launch portal immediately</h4>
	<p>Submit the client intro form once. The platform registers the tenant, seeds branding &amp; contact settings (including the animated ECOM AE hub in the storefront header via <code>use_animated_hub_logo</code>), and prepares the client CP at <code>https://www.client.com/cp/</code>. Finish DNS + set <strong>Live</strong> when the domain points here.</p>
</div>

<div class="row">
	<div class="col-md-7">
		<div class="panel panel-success">
			<div class="panel-heading"><i class="fa fa-file-text-o"></i> Client intro form<?php echo $editTenant ? ' — edit ' . epc_th_h($editTenant['hostname']) : ''; ?></div>
			<div class="panel-body">
				<form method="post" id="epc_th_intro_form">
					<input type="hidden" name="epc_th_intro" value="1">

					<div class="row">
						<div class="col-md-6 form-group">
							<label>Quick template</label>
							<select class="form-control" id="epc_intro_template" onchange="epcIntroApplyTemplate(this)">
								<option value="">— custom —</option>
								<?php foreach ($templates as $tk => $tpl): ?>
								<option value="<?php echo epc_th_h($tk); ?>"><?php echo epc_th_h($tpl['label']); ?></option>
								<?php endforeach; ?>
							</select>
						</div>
						<div class="col-md-6 form-group">
							<label>Launch status</label>
							<select class="form-control" name="status">
								<option value="dns_pending" selected>Awaiting DNS (recommended)</option>
								<option value="draft">Draft only</option>
								<option value="live">Live now (DNS already points here)</option>
							</select>
						</div>
					</div>
					<?php
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
					$erpModRegistry = epc_portal_erp_modules_registry();
					$erpModPresets = epc_portal_erp_modules_presets_ui();
					$introErpMods = !empty($editIntro['erp_modules']) && is_array($editIntro['erp_modules'])
						? $editIntro['erp_modules']
						: epc_portal_erp_modules_default_ids('erp_only');
					?>
					<div class="alert alert-info" style="margin-bottom:14px">
						<label style="font-weight:normal;margin:0;cursor:pointer">
							<input type="checkbox" name="erp_only" id="epc_intro_erp_only" value="1" onchange="epcIntroToggleErpOnly()">
							<strong>ERP only</strong> — no e-commerce storefront or shop CP sidebar. Client gets finance/ERP shell at <code>/cp/shop/finance/erp?epc_erp_shell=1</code>.
						</label>
						<div class="row" style="margin-top:10px">
							<div class="col-sm-6 form-group">
								<label>Access mode</label>
								<select class="form-control" name="access_mode" id="epc_intro_access_mode" onchange="epcIntroToggleErpOnly()">
									<option value="full">Full commerce</option>
									<option value="erp_only" selected>ERP only</option>
									<option value="mixed">Mixed (partial ERP + optional shop)</option>
								</select>
							</div>
							<div class="col-sm-6 form-group">
								<label>ERP module preset</label>
								<input type="hidden" name="erp_modules_preset" id="epc_intro_erp_modules_preset" value="">
								<select class="form-control" id="epc_intro_erp_preset" onchange="epcIntroApplyErpPreset(this)">
									<option value="">— pick preset —</option>
									<?php foreach ($erpModPresets as $pid => $preset): ?>
									<option value="<?php echo epc_th_h($pid); ?>"><?php echo epc_th_h($preset['label']); ?></option>
									<?php endforeach; ?>
								</select>
							</div>
						</div>
						<div class="row" id="epc_intro_erp_modules" style="margin-top:6px">
							<?php foreach ($erpModRegistry as $modId => $mod):
								$on = in_array($modId, $introErpMods, true);
								?>
							<div class="col-sm-6">
								<label style="font-weight:normal;font-size:12px">
									<input type="checkbox" name="erp_modules[]" value="<?php echo epc_th_h($modId); ?>" class="epc-intro-erp-mod"<?php echo $on ? ' checked' : ''; ?>>
									<?php echo epc_th_h($mod['label']); ?>
								</label>
							</div>
							<?php endforeach; ?>
						</div>
						<p class="text-muted small" style="margin:8px 0 0">Toggle which ERP areas appear in the professional shell. One tenant = one company DB on shared ecomae.com or one client domain.</p>
						<label style="font-weight:normal;margin-top:10px;display:block;cursor:pointer">
							<input type="checkbox" name="erp_only_shared" id="epc_intro_shared_erp" value="1" onchange="epcIntroToggleSharedErp()">
							<strong>Hosted on ecomae.com (shared)</strong> — no client domain or DNS; login at <code>www.ecomae.com/cp/</code>
						</label>
					</div>

					<h5 style="margin-top:8px"><i class="fa fa-building"></i> Business &amp; domain</h5>
					<div class="row">
						<div class="col-md-6 form-group">
							<label>Trade / brand name *</label>
							<input class="form-control" name="trade_name" id="epc_intro_trade" required value="<?php echo epc_th_h($editTenant['trade_name'] ?? ''); ?>">
						</div>
						<div class="col-md-6 form-group" id="epc_intro_hostname_wrap">
							<label>Primary domain <span id="epc_intro_hostname_req">*</span> <small class="text-muted">(www.client.com — skip if shared ERP)</small></label>
							<input class="form-control" name="hostname" id="epc_intro_hostname" placeholder="www.client.com" value="<?php echo epc_th_h($editTenant['hostname'] ?? ''); ?>" onchange="epcIntroSyncSiteKey()">
						</div>
					</div>
					<div class="row">
						<div class="col-md-4 form-group">
							<label>Site key</label>
							<input class="form-control" name="site_key" id="epc_intro_site_key" placeholder="auto from domain" value="<?php echo epc_th_h($editTenant['site_key'] ?? ''); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>Industry *</label>
							<select class="form-control" name="industry_code" id="epc_intro_industry">
								<?php
								$industryGroups = epc_portal_industries_grouped(epc_portal_settings_industries());
								foreach ($industryGroups as $grp) {
									if (empty($grp['industries'])) {
										continue;
									}
									?>
								<optgroup label="<?php echo epc_th_h($grp['name']); ?>">
									<?php foreach ($grp['industries'] as $ind) {
										$sel = ($editTenant && $editTenant['industry_code'] === $ind['code']) ? ' selected' : '';
										if (!$editTenant && $ind['code'] === 'auto_parts') {
											$sel = ' selected';
										}
										?>
									<option value="<?php echo epc_th_h($ind['code']); ?>"<?php echo $sel; ?>><?php echo epc_th_h($ind['name']); ?></option>
									<?php } ?>
								</optgroup>
								<?php } ?>
							</select>
						</div>
						<div class="col-md-4 form-group">
							<label>Hub / group name</label>
							<input class="form-control" name="hub_name" id="epc_intro_hub" value="<?php echo epc_th_h($editTenant['hub_name'] ?? 'Electronic World Group'); ?>">
						</div>
					</div>
					<div class="row">
						<div class="col-md-6 form-group">
							<label>Legal company name</label>
							<input class="form-control" name="legal_name" value="<?php echo epc_th_h($editIntro['legal_name']); ?>">
						</div>
						<div class="col-md-6 form-group">
							<label>TRN / VAT</label>
							<input class="form-control" name="trn" value="<?php echo epc_th_h($editIntro['trn']); ?>">
						</div>
					</div>

					<h5><i class="fa fa-user"></i> Contacts</h5>
					<div class="row">
						<div class="col-md-4 form-group">
							<label>Contact person *</label>
							<input class="form-control" name="contact_person" required value="<?php echo epc_th_h($editIntro['contact_person']); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>Contact email *</label>
							<input class="form-control" name="contact_email" type="email" required value="<?php echo epc_th_h($editIntro['contact_email']); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>Phone / WhatsApp</label>
							<input class="form-control" name="contact_phone" value="<?php echo epc_th_h($editIntro['contact_phone']); ?>">
						</div>
					</div>
					<div class="row">
						<div class="col-md-6 form-group">
							<label>Admin CP email * <small class="text-muted">(first login)</small></label>
							<input class="form-control" name="admin_email" type="email" required value="<?php echo epc_th_h($editIntro['admin_email']); ?>">
						</div>
						<div class="col-md-6 form-group">
							<label>From email (notifications)</label>
							<input class="form-control" name="from_email" id="epc_intro_from" value="<?php echo epc_th_h($editTenant['from_email'] ?? ''); ?>">
						</div>
					</div>
					<div class="row">
						<div class="col-md-4 form-group">
							<label>City</label>
							<input class="form-control" name="city" value="<?php echo epc_th_h($editIntro['city']); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>Country *</label>
							<select class="form-control epc-intro-country" name="country_code" id="epc_intro_country_code" required onchange="epcIntroCountryUi()">
								<option value="">— Select country —</option>
								<?php foreach ($onboardCountries as $cc => $cname) {
									$sel = ($editCountryCode === $cc) ? ' selected' : '';
									?>
								<option value="<?php echo epc_th_h($cc); ?>" data-name="<?php echo epc_th_h($cname); ?>"<?php echo $sel; ?>><?php echo epc_th_h($cname); ?></option>
								<?php } ?>
							</select>
							<input type="hidden" name="country" id="epc_intro_country_name" value="<?php echo epc_th_h($editIntro['country'] ?: ($onboardCountries[$editCountryCode] ?? '')); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>Domain registrar</label>
							<input class="form-control" name="domain_registrar" value="<?php echo epc_th_h($editIntro['domain_registrar']); ?>">
						</div>
					</div>
					<div class="form-group">
						<label>Head office address</label>
						<input class="form-control" name="head_office_address" value="<?php echo epc_th_h($editIntro['head_office_address']); ?>">
					</div>
					<div class="form-group">
						<label>Tagline</label>
						<input class="form-control" name="tagline" value="<?php echo epc_th_h($editIntro['tagline']); ?>">
					</div>
					<div class="alert alert-info" id="epc_th_auto_parts_pkg_note" style="display:none;margin-top:12px">
						<strong>Auto parts storefront:</strong> On save we seed <code>automotive_spareparts_pro</code> (piston homepage, animated SVG logo — not ECOM hub / tenant brand).
						Clients override <strong>colours only</strong> in CP → Industry Settings (<code>theme</code> JSON or visual style template). Preset: <code>epc_theme_presets/automotive_spareparts_pro.json</code>.
					</div>

					<h5><i class="fa fa-database"></i> Tenant database</h5>
					<?php
					$editScalePolicy = (string) ($editTenant['scale_policy'] ?? '');
					if ($editScalePolicy === '' && $editTenant) {
						$editScalePolicy = (!empty($editTenant['dedicated_db']) || !empty($editTenant['erp_only_shared'])
							|| (($editTenant['db_name'] ?? '') !== '' && ($editTenant['db_name'] ?? '') !== 'docpart'))
							? 'dedicated_mysql' : 'shared_docpart';
					}
					if ($editScalePolicy === '') {
						$editScalePolicy = 'dedicated_mysql'; // recommended default for 1000+ tenants
					}
					?>
					<div class="alert alert-success" style="margin-bottom:12px">
						<label style="font-weight:normal;margin:0 0 8px;display:block">
							<strong>Scale policy</strong> — recommended: dedicated MySQL per tenant (ready for 1000+ mix tenants).
						</label>
						<select class="form-control" name="scale_policy" id="epc_intro_scale_policy" onchange="epcIntroToggleScalePolicy()">
							<option value="dedicated_mysql"<?php echo $editScalePolicy === 'dedicated_mysql' ? ' selected' : ''; ?>>Dedicated MySQL (recommended)</option>
							<option value="shared_docpart"<?php echo $editScalePolicy === 'shared_docpart' ? ' selected' : ''; ?>>Shared docpart (legacy Model C)</option>
						</select>
						<input type="hidden" name="dedicated_db" id="epc_intro_dedicated_db" value="<?php echo $editScalePolicy === 'dedicated_mysql' ? '1' : '0'; ?>">
						<p class="text-muted small" style="margin:8px 0 0">Dedicated creates an isolated DB/user on save. Shared docpart keeps logical isolation on the common commerce database.</p>
					</div>
					<?php
					$editBcMode = (string) ($editTenant['blockchain_mode'] ?? 'anchor');
					if (!in_array($editBcMode, array('off', 'anchor', 'network'), true)) {
						$editBcMode = 'anchor';
					}
					?>
					<div class="alert alert-info" style="margin-bottom:12px">
						<label style="font-weight:normal;margin:0 0 8px;display:block">
							<strong>Blockchain BOS</strong> — cryptographic proof layer for enterprise integrity (default on).
						</label>
						<select class="form-control" name="blockchain_mode" id="epc_intro_blockchain_mode">
							<option value="anchor"<?php echo $editBcMode === 'anchor' ? ' selected' : ''; ?>>Anchor proofs (recommended)</option>
							<option value="network"<?php echo $editBcMode === 'network' ? ' selected' : ''; ?>>Network participant (roadmap)</option>
							<option value="off"<?php echo $editBcMode === 'off' ? ' selected' : ''; ?>>Off</option>
						</select>
						<p class="text-muted small" style="margin:8px 0 0">Hashes critical business facts and anchors Merkle batches — MySQL remains the operational database.</p>
					</div>
					<div class="row">
						<div class="col-md-4 form-group">
							<label>DB name</label>
							<input class="form-control" name="db_name" id="epc_intro_db" value="<?php echo epc_th_h($editTenant['db_name'] ?? ''); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>DB user</label>
							<input class="form-control" name="db_user" id="epc_intro_db_user" value="<?php echo epc_th_h($editTenant['db_user'] ?? ''); ?>">
						</div>
						<div class="col-md-4 form-group">
							<label>DB password</label>
							<input class="form-control" name="db_password" type="password" autocomplete="new-password" placeholder="<?php echo $editTenant ? 'leave blank to keep' : 'auto if dedicated'; ?>">
						</div>
					</div>
					<div class="form-group">
						<label>Internal notes</label>
						<textarea class="form-control" name="notes" rows="2"><?php echo epc_th_h($editTenant['notes'] ?? ''); ?></textarea>
					</div>
					<div class="form-group">
						<label>Launch notes</label>
						<textarea class="form-control" name="launch_notes" rows="2"><?php echo epc_th_h($editIntro['launch_notes']); ?></textarea>
					</div>

					<button type="submit" class="btn btn-success btn-lg"><i class="fa fa-rocket"></i> Submit intro &amp; register tenant</button>
				</form>
			</div>
		</div>
	</div>

	<div class="col-md-5">
		<?php if ($checklist !== null && !empty($checklist['items'])): ?>
		<div class="panel panel-info">
			<div class="panel-heading"><i class="fa fa-check-square-o"></i> Launch checklist — <?php echo epc_th_h($checklist['hostname']); ?></div>
			<div class="panel-body epc-th-checklist">
				<p><strong><?php echo (int) $checklist['done']; ?> / <?php echo (int) $checklist['total']; ?></strong> steps complete</p>
				<ul class="list-unstyled">
					<?php foreach ($checklist['items'] as $item): ?>
					<li class="<?php echo !empty($item['done']) ? 'done' : 'pending'; ?>">
						<i class="fa fa-<?php echo !empty($item['done']) ? 'check-circle' : 'circle-o'; ?>"></i>
						<?php echo epc_th_h($item['label']); ?>
						<?php if (!empty($item['hint'])): ?>
						<br><small class="text-muted"><?php echo epc_th_h($item['hint']); ?></small>
						<?php endif; ?>
					</li>
					<?php endforeach; ?>
				</ul>
				<p><?php if (!empty($checklist['erp_only_shared'])): ?>
				<a class="btn btn-primary btn-sm" target="_blank" href="<?php echo epc_th_h($checklist['erp_url']); ?>"><i class="fa fa-external-link"></i> Open client ERP</a>
				<?php else: ?>
				<a class="btn btn-primary btn-sm" target="_blank" href="<?php echo epc_th_h($checklist['cp_url']); ?>"><i class="fa fa-external-link"></i> Open client CP</a>
				<?php endif; ?>
				<a class="btn btn-default btn-sm" href="<?php echo epc_th_h($hubUrl); ?>?tab=dns&amp;dns=<?php echo epc_th_h($checklist['hostname']); ?>"><i class="fa fa-globe"></i> DNS steps</a></p>
			</div>
		</div>
		<?php endif; ?>

		<div class="panel panel-default">
			<div class="panel-heading"><i class="fa fa-book"></i> Onboarding guide</div>
			<div class="panel-body">
				<?php foreach ($guideSteps as $i => $step): ?>
				<div class="epc-th-step">
					<h5>Step <?php echo (int) ($i + 1); ?> — <?php echo epc_th_h($step['title']); ?></h5>
					<div><?php echo $step['body']; ?></div>
				</div>
				<?php endforeach; ?>
				<p class="text-muted small">Platform IP: <code><?php echo epc_th_h($stats['platform_ip']); ?></code> · Super CP: <a href="https://www.ecomae.com/cp/" target="_blank" rel="noopener">www.ecomae.com/cp</a></p>
			</div>
		</div>

		<?php if (count($tenants) > 0): ?>
		<div class="panel panel-default">
			<div class="panel-heading"><i class="fa fa-list"></i> Recent tenants</div>
			<ul class="list-group">
				<?php foreach (array_slice($tenants, 0, 8) as $t): ?>
				<li class="list-group-item">
					<strong><?php echo epc_th_h($t['hostname']); ?></strong>
					<span class="label <?php echo $t['intro_done'] ? 'label-success' : 'label-default'; ?>" style="margin-left:6px"><?php echo $t['intro_done'] ? 'Intro OK' : 'No intro'; ?></span>
					<br>
					<a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard&amp;edit=<?php echo epc_th_h($t['site_key']); ?>">Edit intro</a> ·
					<a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard&amp;checklist=<?php echo epc_th_h($t['site_key']); ?>">Checklist</a>
				</li>
				<?php endforeach; ?>
			</ul>
		</div>
		<?php endif; ?>
	</div>
</div>
</div>

<script>
var epcIntroTemplates = <?php echo json_encode($templates); ?>;
var epcIntroErpPresets = <?php echo json_encode($erpModPresets, JSON_UNESCAPED_UNICODE); ?>;
var epcIntroIndustryErpDefaults = <?php echo json_encode(function_exists('epc_portal_industry_erp_modules_preset_map') ? epc_portal_industry_erp_modules_preset_map() : array(), JSON_UNESCAPED_UNICODE); ?>;
function epcIntroToggleSharedErp() {
	var shared = document.getElementById('epc_intro_shared_erp');
	var host = document.getElementById('epc_intro_hostname');
	var req = document.getElementById('epc_intro_hostname_req');
	if (!shared || !host) return;
	if (shared.checked) {
		host.value = 'www.ecomae.com';
		host.removeAttribute('required');
		if (req) req.style.display = 'none';
		var erpCb = document.getElementById('epc_intro_erp_only');
		if (erpCb) { erpCb.checked = true; epcIntroToggleErpOnly(); }
	} else {
		if (host.value === 'www.ecomae.com') host.value = '';
		host.setAttribute('required', 'required');
		if (req) req.style.display = '';
	}
}
function epcIntroApplyErpPreset(sel) {
	var p = epcIntroErpPresets[sel.value];
	if (!p || !p.modules) return;
	document.querySelectorAll('.epc-intro-erp-mod').forEach(function(cb) {
		cb.checked = p.modules.indexOf(cb.value) !== -1;
	});
	var hid = document.getElementById('epc_intro_erp_modules_preset');
	if (hid) hid.value = sel.value;
}
function epcIntroApplyIndustryErpPreset() {
	var ind = document.getElementById('epc_intro_industry');
	var presetSel = document.getElementById('epc_intro_erp_preset');
	if (!ind || !presetSel) return;
	var pid = epcIntroIndustryErpDefaults[ind.value];
	if (!pid || !epcIntroErpPresets[pid]) return;
	presetSel.value = pid;
	epcIntroApplyErpPreset(presetSel);
}
function epcIntroToggleErpOnly() {
	var cb = document.getElementById('epc_intro_erp_only');
	var ind = document.getElementById('epc_intro_industry');
	var mode = document.getElementById('epc_intro_access_mode');
	if (!cb || !ind) return;
	if (cb.checked) {
		ind.value = 'erp_standalone';
		if (mode) mode.value = 'erp_only';
		epcIntroApplyIndustryErpPreset();
	} else if (mode && mode.value === 'erp_only') {
		mode.value = 'full';
	}
	epcIntroToggleAutoPartsNote();
}
function epcIntroApplyTemplate(sel) {
	var t = epcIntroTemplates[sel.value];
	if (!t) return;
	document.getElementById('epc_intro_site_key').value = sel.value;
	document.getElementById('epc_intro_hostname').value = t.hostname || '';
	document.getElementById('epc_intro_industry').value = t.industry || 'auto_parts';
	document.getElementById('epc_intro_trade').value = t.trade_name || '';
	document.getElementById('epc_intro_hub').value = t.hub_name || '';
	document.getElementById('epc_intro_from').value = t.from_email || '';
	document.getElementById('epc_intro_db').value = sel.value.replace(/[^a-z0-9_]/g, '');
	var erpCb = document.getElementById('epc_intro_erp_only');
	if (erpCb) {
		erpCb.checked = !!(t.access_mode === 'erp_only' || t.industry === 'erp_standalone');
		epcIntroToggleErpOnly();
	}
	var sharedCb = document.getElementById('epc_intro_shared_erp');
	if (sharedCb) {
		sharedCb.checked = !!(t.erp_only_shared || t.hosted_on === 'platform' || t.hostname === 'www.ecomae.com');
		epcIntroToggleSharedErp();
	}
}
function epcIntroToggleScalePolicy() {
	var sel = document.getElementById('epc_intro_scale_policy');
	var hid = document.getElementById('epc_intro_dedicated_db');
	var db = document.getElementById('epc_intro_db');
	var dbUser = document.getElementById('epc_intro_db_user');
	var keyEl = document.getElementById('epc_intro_site_key');
	if (!sel || !hid) return;
	var dedicated = sel.value === 'dedicated_mysql';
	hid.value = dedicated ? '1' : '0';
	var key = keyEl && keyEl.value ? keyEl.value.replace(/[^a-z0-9_]/g, '') : '';
	if (dedicated) {
		if (db && (!db.value || db.value === 'docpart') && key) db.value = key;
		if (dbUser && (!dbUser.value || dbUser.value === 'docpart') && key) dbUser.value = key;
	} else {
		if (db && (!db.value || db.value === key)) db.value = 'docpart';
		if (dbUser && (!dbUser.value || dbUser.value === key)) dbUser.value = 'docpart';
	}
}
function epcIntroSyncSiteKey() {
	var host = document.getElementById('epc_intro_hostname').value.toLowerCase().replace(/^www\./, '');
	var key = host.replace(/\.[^.]+$/, '').replace(/[^a-z0-9]/g, '_');
	document.getElementById('epc_intro_site_key').value = key;
	var scale = document.getElementById('epc_intro_scale_policy');
	var dedicated = !scale || scale.value === 'dedicated_mysql';
	var db = document.getElementById('epc_intro_db');
	var dbUser = document.getElementById('epc_intro_db_user');
	if (dedicated) {
		if (db && (!db.value || db.value === 'docpart')) db.value = key;
		if (dbUser && (!dbUser.value || dbUser.value === 'docpart')) dbUser.value = key;
	} else if (db && !db.value) {
		db.value = 'docpart';
	}
}
function epcIntroCountryUi() {
	var sel = document.getElementById('epc_intro_country_code');
	var hid = document.getElementById('epc_intro_country_name');
	if (!sel || !hid) return;
	var opt = sel.options[sel.selectedIndex];
	hid.value = opt && opt.getAttribute('data-name') ? opt.getAttribute('data-name') : '';
}
epcIntroCountryUi();
if (document.getElementById('epc_intro_scale_policy')) {
	epcIntroToggleScalePolicy();
}
function epcIntroToggleAutoPartsNote() {
	var el = document.getElementById('epc_th_auto_parts_pkg_note');
	var ind = document.getElementById('epc_intro_industry');
	if (!el || !ind) return;
	el.style.display = ind.value === 'auto_parts' ? '' : 'none';
}
document.getElementById('epc_intro_industry').addEventListener('change', function () {
	epcIntroToggleAutoPartsNote();
	var erpCb = document.getElementById('epc_intro_erp_only');
	if (erpCb && erpCb.checked) {
		epcIntroApplyIndustryErpPreset();
	}
});
epcIntroToggleAutoPartsNote();
</script>
