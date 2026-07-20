<?php
/**
 * Super CP Tenant hub — Industry Templates (show / apply / guide).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_tenant_templates_catalog.php';

$tplSection = isset($_GET['tpl_section']) ? preg_replace('/[^a-z_]/', '', (string) $_GET['tpl_section']) : 'gallery';
if (!in_array($tplSection, array('gallery', 'packages', 'guide'), true)) {
	$tplSection = 'gallery';
}
$filterQ = trim((string) ($_GET['q'] ?? ''));
$catalog = epc_th_industry_templates_catalog();
$packages = epc_th_storefront_packages_catalog();
$guideSteps = epc_th_templates_guide_steps();
$prefillTemplate = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['template'] ?? '')));
$prefillTenant = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));

if ($filterQ !== '') {
	$qq = strtolower($filterQ);
	$catalog = array_values(array_filter($catalog, function ($c) use ($qq) {
		$hay = strtolower(
			($c['label'] ?? '') . ' ' . ($c['template_key'] ?? '') . ' ' . ($c['description'] ?? '') . ' '
			. ($c['industry_code'] ?? '') . ' ' . implode(' ', $c['portal_names'] ?? array())
		);
		return strpos($hay, $qq) !== false;
	}));
}

$tplBase = $hubUrl . '?tab=templates';
?>
<style>
.epc-tpl-hero{position:relative;overflow:hidden;border-radius:14px;padding:20px 22px 16px;margin:0 0 16px;background:linear-gradient(125deg,#0f172a 0%,#1e293b 48%,#0f766e 100%);color:#fff;box-shadow:0 10px 28px rgba(0,0,0,.12);}
.epc-tpl-hero h3{margin:0 0 6px;font-size:20px;font-weight:800;color:#fff!important;}
.epc-tpl-hero p{margin:0;max-width:780px;font-size:13px;line-height:1.5;color:rgba(255,255,255,.9);}
.epc-tpl-nav{display:flex;flex-wrap:wrap;gap:6px;margin:0 0 16px;}
.epc-tpl-nav a{display:inline-flex;align-items:center;gap:6px;padding:7px 12px;border-radius:8px;border:1px solid #e5e5e5;background:#fff;color:#404040!important;font-size:12px;font-weight:700;text-decoration:none!important;}
.epc-tpl-nav a.active,.epc-tpl-nav a:hover{background:#0f172a;border-color:#0f172a;color:#fff!important;}
.epc-tpl-toolbar{display:flex;flex-wrap:wrap;gap:10px;align-items:center;margin:0 0 14px;}
.epc-tpl-toolbar input[type=search]{min-width:220px;max-width:360px;}
.epc-tpl-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:14px;margin:0 0 18px;}
.epc-tpl-card{border:1px solid #e5e5e5;border-radius:12px;background:#fff;box-shadow:0 4px 14px rgba(0,0,0,.04);overflow:hidden;display:flex;flex-direction:column;min-height:100%;}
.epc-tpl-card__top{padding:14px 14px 10px;border-bottom:1px solid #f1f5f9;position:relative;}
.epc-tpl-card__top .ico{width:36px;height:36px;border-radius:10px;display:inline-flex;align-items:center;justify-content:center;color:#fff;font-size:16px;margin-bottom:8px;}
.epc-tpl-card h4{margin:0 0 4px;font-size:15px;font-weight:800;}
.epc-tpl-card .desc{font-size:12px;color:#64748b;line-height:1.4;min-height:34px;}
.epc-tpl-card__meta{padding:10px 14px;font-size:11.5px;color:#475569;display:grid;gap:4px;flex:1;}
.epc-tpl-card__meta code{font-size:11px;background:#f1f5f9;padding:1px 5px;border-radius:4px;}
.epc-tpl-card__actions{padding:10px 14px 14px;display:flex;flex-wrap:wrap;gap:6px;border-top:1px solid #f1f5f9;}
.epc-tpl-card__actions .btn{font-size:11.5px;font-weight:700;}
.epc-tpl-apply{background:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;padding:14px 16px;margin:0 0 18px;}
.epc-tpl-apply h4{margin:0 0 10px;font-size:14px;font-weight:800;}
.epc-tpl-guide{display:grid;gap:12px;}
.epc-tpl-guide article{padding:14px 16px;border-radius:12px;border:1px solid #e5e5e5;background:#fff;}
.epc-tpl-guide article h4{margin:0 0 6px;font-size:14px;font-weight:800;}
.epc-tpl-guide article .body{font-size:13px;line-height:1.5;color:#334155;}
.epc-tpl-flow{display:flex;flex-wrap:wrap;align-items:center;gap:6px;margin:12px 0 0;font-size:12px;font-weight:700;}
.epc-tpl-flow span{padding:6px 10px;border-radius:999px;background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.2);}
</style>

<div class="epc-tpl-hero">
	<h3><i class="fa fa-th-large"></i> Industry templates — control centre</h3>
	<p>Every industry on <strong>/platform/industries</strong> is a reusable template: live hub, demo CP/ERP, storefront package, and ERP pack. Preview demos here, then apply the template to a specific tenant.</p>
	<div class="epc-tpl-flow">
		<span>1. Preview demo</span>
		<span>→</span>
		<span>2. Choose tenant</span>
		<span>→</span>
		<span>3. Apply theme + packs</span>
		<span>→</span>
		<span>4. ERP Setup pack</span>
	</div>
</div>

<div class="epc-tpl-nav">
	<a class="<?php echo $tplSection === 'gallery' ? 'active' : ''; ?>" href="<?php echo epc_th_h($tplBase . '&tpl_section=gallery'); ?>"><i class="fa fa-th-large"></i> Template gallery (<?php echo count($catalog); ?>)</a>
	<a class="<?php echo $tplSection === 'packages' ? 'active' : ''; ?>" href="<?php echo epc_th_h($tplBase . '&tpl_section=packages'); ?>"><i class="fa fa-paint-brush"></i> Storefront packages</a>
	<a class="<?php echo $tplSection === 'guide' ? 'active' : ''; ?>" href="<?php echo epc_th_h($tplBase . '&tpl_section=guide'); ?>"><i class="fa fa-book"></i> Guide</a>
	<a href="https://www.ecomae.com/platform/industries" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Platform industries</a>
	<a href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard"><i class="fa fa-rocket"></i> Onboard client</a>
	<a href="<?php echo epc_th_h($portalSettings); ?>"><i class="fa fa-cog"></i> Industry settings</a>
</div>

<?php if ($tplSection === 'gallery'): ?>

	<div class="epc-tpl-apply">
		<h4><i class="fa fa-magic"></i> Apply template to tenant</h4>
		<form method="post" class="form-inline" style="display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end;">
			<input type="hidden" name="epc_th_apply_template" value="1">
			<div class="form-group">
				<label style="display:block;font-size:11px;font-weight:700;color:#64748b;">Template</label>
				<select name="template_key" class="form-control input-sm" required style="min-width:220px;">
					<option value="">— select —</option>
					<?php
					$allForSelect = epc_th_industry_templates_catalog();
					foreach ($allForSelect as $c):
						$sel = ($prefillTemplate !== '' && ($prefillTemplate === $c['template_key'] || $prefillTemplate === $c['id'])) ? ' selected' : '';
					?>
					<option value="<?php echo epc_th_h($c['template_key']); ?>"<?php echo $sel; ?>>
						<?php echo epc_th_h($c['label'] . ' (' . $c['template_key'] . ')'); ?>
					</option>
					<?php endforeach; ?>
				</select>
			</div>
			<div class="form-group">
				<label style="display:block;font-size:11px;font-weight:700;color:#64748b;">Tenant</label>
				<select name="site_key" class="form-control input-sm" required style="min-width:200px;">
					<option value="">— select tenant —</option>
					<?php foreach ($tenants as $t):
						$sk = (string) ($t['site_key'] ?? '');
						$sel = ($prefillTenant !== '' && $prefillTenant === $sk) ? ' selected' : '';
					?>
					<option value="<?php echo epc_th_h($sk); ?>"<?php echo $sel; ?>>
						<?php echo epc_th_h($sk . ' · ' . ($t['hostname'] ?? '') . ' · ' . ($t['industry_code'] ?? '')); ?>
					</option>
					<?php endforeach; ?>
				</select>
			</div>
			<div class="form-group">
				<label style="display:block;font-size:11px;font-weight:700;color:#64748b;">
					<input type="checkbox" name="push_client" value="1" checked> Sync CP packs to client DB
				</label>
			</div>
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-check"></i> Apply to tenant</button>
		</form>
		<p class="text-muted" style="margin:10px 0 0;font-size:12px;">Applies industry code, visual theme, and storefront package. ERP pack is recorded as a hint — finish in client ERP → Setup.</p>
	</div>

	<form method="get" class="epc-tpl-toolbar">
		<input type="hidden" name="tab" value="templates">
		<input type="hidden" name="tpl_section" value="gallery">
		<input type="search" name="q" class="form-control input-sm" placeholder="Filter by name, key, industry…" value="<?php echo epc_th_h($filterQ); ?>">
		<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-search"></i> Filter</button>
		<?php if ($filterQ !== ''): ?>
		<a class="btn btn-link btn-sm" href="<?php echo epc_th_h($tplBase . '&tpl_section=gallery'); ?>">Clear</a>
		<?php endif; ?>
		<span class="text-muted" style="font-size:12px;"><?php echo count($catalog); ?> template(s)</span>
	</form>

	<div class="epc-tpl-grid">
		<?php if (!$catalog): ?>
			<div class="alert alert-info">No templates match your filter.</div>
		<?php endif; ?>
		<?php foreach ($catalog as $c): ?>
		<div class="epc-tpl-card">
			<div class="epc-tpl-card__top">
				<span class="ico" style="background:<?php echo epc_th_h($c['primary']); ?>;"><i class="fa <?php echo epc_th_h($c['icon']); ?>"></i></span>
				<h4><?php echo epc_th_h($c['label']); ?></h4>
				<div class="desc"><?php echo epc_th_h($c['description']); ?></div>
			</div>
			<div class="epc-tpl-card__meta">
				<div><strong>Template key</strong> <code><?php echo epc_th_h($c['template_key']); ?></code></div>
				<div><strong>Default industry</strong> <code><?php echo epc_th_h($c['industry_code']); ?></code></div>
				<?php if ($c['storefront_label'] !== ''): ?>
				<div><strong>Storefront</strong> <?php echo epc_th_h($c['storefront_label']); ?></div>
				<?php else: ?>
				<div><strong>Storefront</strong> <span class="text-muted">Theme only (no premium package)</span></div>
				<?php endif; ?>
				<?php if ($c['erp_pack'] !== ''): ?>
				<div><strong>ERP pack</strong> <?php echo epc_th_h($c['erp_pack_label'] !== '' ? $c['erp_pack_label'] : $c['erp_pack']); ?></div>
				<?php endif; ?>
				<?php if (!empty($c['portal_names'])): ?>
				<div><strong>Portal industries</strong> <?php echo epc_th_h(implode(', ', array_slice($c['portal_names'], 0, 4))); ?><?php echo count($c['portal_names']) > 4 ? '…' : ''; ?></div>
				<?php endif; ?>
			</div>
			<div class="epc-tpl-card__actions">
				<a class="btn btn-default btn-xs" target="_blank" rel="noopener" href="<?php echo epc_th_h($c['live_url']); ?>"><i class="fa fa-globe"></i> Live hub</a>
				<a class="btn btn-default btn-xs" target="_blank" rel="noopener" href="<?php echo epc_th_h($c['demo_cp_url']); ?>"><i class="fa fa-desktop"></i> Demo CP</a>
				<a class="btn btn-info btn-xs" target="_blank" rel="noopener" href="<?php echo epc_th_h($c['demo_erp_url']); ?>"><i class="fa fa-university"></i> Demo ERP</a>
				<a class="btn btn-primary btn-xs" href="<?php echo epc_th_h($tplBase . '&tpl_section=gallery&template=' . rawurlencode($c['template_key'])); ?>#"><i class="fa fa-magic"></i> Select to apply</a>
			</div>
		</div>
		<?php endforeach; ?>
	</div>

<?php elseif ($tplSection === 'packages'): ?>

	<div class="panel panel-default">
		<div class="panel-heading"><i class="fa fa-paint-brush"></i> Premium storefront packages</div>
		<div class="panel-body">
			<p class="text-muted">These are full layout packages (header/home/hero). They auto-attach when you apply a matching industry template, or you can set them under Industry settings.</p>
			<table class="table table-bordered table-condensed" style="font-size:13px;">
				<thead><tr><th>Package</th><th>Industry</th><th>Theme</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($packages as $p): ?>
					<tr>
						<td><strong><?php echo epc_th_h($p['label']); ?></strong><br><small class="text-muted"><?php echo epc_th_h($p['desc']); ?></small></td>
						<td><code><?php echo epc_th_h($p['industry_code']); ?></code></td>
						<td><?php echo epc_th_h($p['theme_template']); ?></td>
						<td><?php echo !empty($p['implemented']) ? '<span class="label label-success">Ready</span>' : '<span class="label label-default">Placeholder</span>'; ?></td>
						<td>
							<?php
							$matchTpl = '';
							foreach (epc_th_industry_templates_catalog() as $c) {
								if ($c['storefront_package'] === $p['id'] || $c['industry_code'] === $p['industry_code']) {
									$matchTpl = $c['template_key'];
									break;
								}
							}
							if ($matchTpl !== ''):
							?>
							<a class="btn btn-xs btn-primary" href="<?php echo epc_th_h($tplBase . '&tpl_section=gallery&template=' . rawurlencode($matchTpl)); ?>">Apply via template</a>
							<?php endif; ?>
						</td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		</div>
	</div>

<?php else: /* guide */ ?>

	<div class="epc-tpl-guide">
		<?php foreach ($guideSteps as $i => $step): ?>
		<article>
			<h4><?php echo ($i + 1) . '. ' . epc_th_h($step['title']); ?></h4>
			<div class="body"><?php echo $step['body']; // trusted HTML from helper ?></div>
		</article>
		<?php endforeach; ?>
		<article>
			<h4>Quick links</h4>
			<div class="body">
				<a class="btn btn-sm btn-default" href="https://www.ecomae.com/platform/industries" target="_blank" rel="noopener">Platform industries</a>
				<a class="btn btn-sm btn-default" href="<?php echo epc_th_h($hubUrl); ?>?tab=onboard">Onboard client</a>
				<a class="btn btn-sm btn-default" href="<?php echo epc_th_h($hubUrl); ?>?tab=demos">Demo tenants</a>
				<a class="btn btn-sm btn-default" href="<?php echo epc_th_h($portalSettings); ?>">Industry settings</a>
				<a class="btn btn-sm btn-default" href="<?php echo epc_th_h($hubUrl); ?>?tab=guide">Onboard guide</a>
			</div>
		</article>
	</div>

<?php endif; ?>
