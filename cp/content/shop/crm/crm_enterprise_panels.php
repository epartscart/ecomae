<?php
/**
 * Enterprise CRM panels — intelligence + accounts / customer 360.
 */
defined('_ASTEXE_') or die('No access');

if ($tab === 'intelligence'):
	$adv = isset($crmAdv) && is_array($crmAdv) ? $crmAdv : array();
	$bands = isset($adv['lead_bands']) ? $adv['lead_bands'] : array('hot' => 0, 'warm' => 0, 'cold' => 0, 'total' => 0);
	$forecast = isset($adv['forecast']) ? $adv['forecast'] : array();
	$funnel = isset($adv['funnel']) ? $adv['funnel'] : array();
	$sources = isset($adv['sources']) ? $adv['sources'] : array();
	$topLeads = isset($adv['top_leads']) ? $adv['top_leads'] : array();
	$nextActions = isset($adv['next_actions']) ? $adv['next_actions'] : array();
	$maxFunnel = max(1, (int)($funnel['leads'] ?? 1));
?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-line-chart"></i> Sales intelligence</h4>
		<p class="text-muted" style="margin-top:-4px;">Lead scoring, weighted forecast, conversion funnel, and next-best actions.</p>

		<div class="epc-crm-kpi">
			<div class="kpi"><div class="lbl">Hot leads</div><div class="val" style="color:#b91c1c;"><?php echo (int)($bands['hot'] ?? 0); ?></div><div class="hint">Score ≥ 70</div></div>
			<div class="kpi"><div class="lbl">Warm leads</div><div class="val amber"><?php echo (int)($bands['warm'] ?? 0); ?></div><div class="hint">Score 40–69</div></div>
			<div class="kpi"><div class="lbl">Cold leads</div><div class="val blue"><?php echo (int)($bands['cold'] ?? 0); ?></div><div class="hint">Score &lt; 40</div></div>
			<div class="kpi"><div class="lbl">Open pipeline</div><div class="val blue"><?php echo epc_crm_money($forecast['open_value'] ?? 0); ?></div><div class="hint"><?php echo (int)($forecast['open_count'] ?? 0); ?> deals</div></div>
			<div class="kpi"><div class="lbl">Weighted forecast</div><div class="val green"><?php echo epc_crm_money($forecast['weighted_value'] ?? 0); ?></div><div class="hint">Probability-adjusted</div></div>
			<div class="kpi"><div class="lbl">Win rate</div><div class="val"><?php echo epc_crm_h((string)($forecast['win_rate'] ?? 0)); ?>%</div><div class="hint">Closed won / closed</div></div>
		</div>

		<div class="epc-crm-grid-2">
			<div class="epc-crm-panel">
				<div class="epc-crm-panel-hd"><h4>Conversion funnel</h4></div>
				<div class="epc-crm-panel-bd">
					<div class="epc-crm-funnel">
						<?php
						$funnelRows = array(
							array('Leads', (int)($funnel['leads'] ?? 0)),
							array('Qualified', (int)($funnel['qualified'] ?? 0)),
							array('Opportunities', (int)($funnel['opportunities'] ?? 0)),
							array('Proposal+', (int)($funnel['proposals'] ?? 0)),
							array('Won', (int)($funnel['won'] ?? 0)),
						);
						foreach ($funnelRows as $fr):
							$pct = (int) round(($fr[1] / $maxFunnel) * 100);
						?>
						<div class="epc-crm-funnel-row">
							<div class="lbl"><?php echo epc_crm_h($fr[0]); ?></div>
							<div class="epc-crm-funnel-bar"><span style="width:<?php echo $pct; ?>%"></span></div>
							<div class="cnt"><?php echo (int)$fr[1]; ?></div>
						</div>
						<?php endforeach; ?>
					</div>
					<p class="text-muted" style="margin:12px 0 0;font-size:12px;">
						Lead → Opp: <strong><?php echo epc_crm_h((string)($funnel['lead_to_opp_pct'] ?? 0)); ?>%</strong>
						· Opp → Won: <strong><?php echo epc_crm_h((string)($funnel['opp_to_won_pct'] ?? 0)); ?>%</strong>
					</p>
				</div>
			</div>
			<div class="epc-crm-panel">
				<div class="epc-crm-panel-hd"><h4>Next-best actions</h4>
					<a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'activities')); ?>">All activities</a>
				</div>
				<div class="epc-crm-panel-bd">
					<?php if (empty($nextActions)): ?>
						<p class="text-muted" style="margin:0;">No open follow-ups with due dates. Add activities on leads and opportunities.</p>
					<?php else: ?>
					<ul class="epc-crm-action-list">
						<?php foreach ($nextActions as $a): ?>
						<li>
							<div>
								<strong><?php echo epc_crm_h($a['activity_type']); ?></strong>
								· <?php echo epc_crm_h($a['related_type'] . ' #' . (int)$a['related_id']); ?>
								<div class="text-muted" style="font-size:12px;"><?php echo epc_crm_h(mb_substr((string)$a['notes'], 0, 80)); ?></div>
							</div>
							<div style="text-align:right;">
								<?php if (!empty($a['is_overdue'])): ?>
									<span class="overdue">OVERDUE</span><br>
								<?php else: ?>
									<span class="due">Due</span><br>
								<?php endif; ?>
								<span style="font-size:12px;font-weight:700;"><?php echo (int)$a['due_date'] ? epc_crm_h(date('d M Y', (int)$a['due_date'])) : '—'; ?></span>
							</div>
						</li>
						<?php endforeach; ?>
					</ul>
					<?php endif; ?>
				</div>
			</div>
		</div>

		<div class="epc-crm-grid-2">
			<div class="epc-crm-panel">
				<div class="epc-crm-panel-hd"><h4>Top scored leads</h4>
					<a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads')); ?>">Manage leads</a>
				</div>
				<div class="epc-crm-panel-bd" style="padding:0;">
					<table class="table table-condensed" style="margin:0;">
						<thead><tr><th>Company</th><th>Score</th><th>Band</th><th>Expected</th><th></th></tr></thead>
						<tbody>
						<?php foreach (array_slice($topLeads, 0, 12) as $L):
							$band = (string)($L['lead_band'] ?? 'cold');
						?>
							<tr>
								<td>
									<strong><?php echo epc_crm_h($L['company']); ?></strong>
									<div class="text-muted" style="font-size:11px;"><?php echo epc_crm_h($L['contact_name'] ?: $L['email']); ?></div>
								</td>
								<td><span class="epc-crm-score"><?php echo (int)($L['lead_score'] ?? 0); ?></span></td>
								<td><span class="epc-crm-band epc-crm-band-<?php echo epc_crm_h($band); ?>"><?php echo epc_crm_h($band); ?></span></td>
								<td><?php echo epc_crm_money($L['expected_value'] ?? 0); ?></td>
								<td>
									<?php if (($L['status'] ?? '') !== 'converted'): ?>
									<button type="button" class="btn btn-xs btn-success epc-crm-convert" data-id="<?php echo (int)$L['id']; ?>">Convert</button>
									<?php endif; ?>
								</td>
							</tr>
						<?php endforeach; ?>
						<?php if (empty($topLeads)): ?>
							<tr><td colspan="5" class="text-muted">No leads yet.</td></tr>
						<?php endif; ?>
						</tbody>
					</table>
				</div>
			</div>
			<div class="epc-crm-panel">
				<div class="epc-crm-panel-hd"><h4>Lead sources</h4></div>
				<div class="epc-crm-panel-bd" style="padding:0;">
					<table class="table table-condensed" style="margin:0;">
						<thead><tr><th>Source</th><th>Leads</th><th>Qualified</th><th>Expected</th></tr></thead>
						<tbody>
						<?php foreach ($sources as $s): ?>
							<tr>
								<td><strong><?php echo epc_crm_h($s['source']); ?></strong></td>
								<td><?php echo (int)$s['c']; ?></td>
								<td><?php echo (int)$s['qualified_c']; ?></td>
								<td><?php echo epc_crm_money($s['expected_value'] ?? 0); ?></td>
							</tr>
						<?php endforeach; ?>
						<?php if (empty($sources)): ?>
							<tr><td colspan="4" class="text-muted">No source data yet.</td></tr>
						<?php endif; ?>
						</tbody>
					</table>
				</div>
			</div>
		</div>
	</div>

<?php elseif ($tab === 'accounts'):
	$accounts = isset($crmAccounts) && is_array($crmAccounts) ? $crmAccounts : array();
	$contactsUrl = $erpUrl . ((strpos($erpUrl, '?') !== false) ? '&' : '?') . 'tab=contacts';
?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-building"></i> Accounts &amp; Customer 360</h4>
		<p class="text-muted" style="margin-top:-4px;">
			Account rollup from leads and opportunities.
			Master address book:
			<a href="<?php echo epc_crm_h($contactsUrl); ?>">ERP Contacts</a>.
		</p>

		<div class="epc-crm-form-card">
			<form id="epc_crm_360_form" class="form-inline epc-crm-form-inline">
				<label style="font-weight:700;margin-right:8px;">Customer 360</label>
				<input type="number" name="user_id" id="epc_crm_360_user" class="form-control input-sm" placeholder="Shop user ID" min="1" required>
				<button type="submit" class="btn btn-sm btn-primary"><i class="fa fa-search"></i> Load 360</button>
			</form>
			<div id="epc_crm_360_result" style="margin-top:12px;display:none;"></div>
		</div>

		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead>
				<tr>
					<th>Account</th>
					<th>Contact</th>
					<th>Leads</th>
					<th>Opps</th>
					<th>Open pipeline</th>
					<th>Won</th>
					<th>Expected</th>
					<th>Linked user</th>
					<th>Last touch</th>
					<th></th>
				</tr>
			</thead>
			<tbody>
			<?php foreach ($accounts as $acc): ?>
				<tr>
					<td><strong><?php echo epc_crm_h($acc['name']); ?></strong></td>
					<td>
						<?php echo epc_crm_h($acc['email'] ?: '—'); ?>
						<?php if (!empty($acc['phone'])): ?><div class="text-muted" style="font-size:11px;"><?php echo epc_crm_h($acc['phone']); ?></div><?php endif; ?>
					</td>
					<td><?php echo (int)$acc['leads']; ?></td>
					<td><?php echo (int)$acc['opportunities']; ?></td>
					<td class="text-primary"><strong><?php echo epc_crm_money($acc['open_pipeline']); ?></strong></td>
					<td class="text-success"><?php echo epc_crm_money($acc['won_value']); ?></td>
					<td><?php echo epc_crm_money($acc['expected_value']); ?></td>
					<td>
						<?php if ((int)$acc['linked_user_id'] > 0): ?>
							<button type="button" class="btn btn-xs btn-default epc-crm-load-360" data-user-id="<?php echo (int)$acc['linked_user_id']; ?>">#<?php echo (int)$acc['linked_user_id']; ?> 360</button>
						<?php else: ?>
							—
						<?php endif; ?>
					</td>
					<td><?php echo (int)$acc['last_touch'] ? epc_crm_h(date('Y-m-d', (int)$acc['last_touch'])) : '—'; ?></td>
					<td>
						<?php if (!empty($acc['lead_ids'])): ?>
							<a class="btn btn-xs btn-default" href="<?php echo epc_crm_h(epc_crm_tab_url($crmUrl, 'leads')); ?>">Leads</a>
						<?php endif; ?>
					</td>
				</tr>
			<?php endforeach; ?>
			<?php if (empty($accounts)): ?>
				<tr><td colspan="10" class="text-muted">No accounts yet — add leads to build the account book.</td></tr>
			<?php endif; ?>
			</tbody>
		</table>
	</div>
<?php endif; ?>
