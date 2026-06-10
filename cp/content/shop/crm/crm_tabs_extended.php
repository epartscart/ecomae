<?php
/** CRM sub-tabs: quotes, tickets, projects, contracts, expenses */
defined('_ASTEXE_') or die('No access');

if ($tab === 'quotes'): ?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-file-text-o"></i> Quotes</h4>
		<form id="epc_crm_quote_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
			<input type="number" name="opportunity_id" class="form-control input-sm" placeholder="Opp ID" value="0">
			<select name="status" class="form-control input-sm"><?php foreach (epc_crm_quote_statuses() as $k => $lbl): ?><option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option><?php endforeach; ?></select>
			<input type="text" name="line_description" class="form-control input-sm" placeholder="Line description" required>
			<input type="number" step="0.01" name="line_qty" class="form-control input-sm" placeholder="Qty" value="1">
			<input type="number" step="0.01" name="line_unit_price" class="form-control input-sm" placeholder="Unit AED" required>
			<button type="submit" class="btn btn-sm btn-primary">Create quote</button>
		</form>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>#</th><th>Number</th><th>Opp</th><th>Status</th><th>Total</th><th>Order</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($quotes as $q): ?>
				<tr>
					<td><?php echo (int)$q['id']; ?></td>
					<td><?php echo epc_crm_h($q['quote_number']); ?></td>
					<td><?php echo epc_crm_h($q['opp_title'] ?: ('#' . (int)$q['opportunity_id'])); ?></td>
					<td><span class="label label-info"><?php echo epc_crm_h($q['status']); ?></span></td>
					<td><?php echo epc_crm_money($q['subtotal']); ?> AED</td>
					<td><?php echo (int)$q['shop_order_id'] ? ('<a href="' . epc_crm_h($ordersUrl) . '?order_id=' . (int)$q['shop_order_id'] . '">#' . (int)$q['shop_order_id'] . '</a>') : '—'; ?></td>
					<td>
						<button type="button" class="btn btn-xs btn-default epc-crm-quote-preview" data-id="<?php echo (int)$q['id']; ?>">Preview</button>
						<button type="button" class="btn btn-xs btn-info epc-crm-quote-email" data-id="<?php echo (int)$q['id']; ?>">Email</button>
						<?php if ($q['status'] !== 'accepted'): ?><button type="button" class="btn btn-xs btn-success epc-crm-accept-quote" data-id="<?php echo (int)$q['id']; ?>">Accept → order</button><?php endif; ?>
					</td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>

<?php elseif ($tab === 'tickets'): ?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-life-ring"></i> Support tickets</h4>
		<form id="epc_crm_ticket_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
			<input type="text" name="subject" class="form-control input-sm" placeholder="Subject" required>
			<input type="number" name="customer_user_id" class="form-control input-sm" placeholder="User ID">
			<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID">
			<select name="priority" class="form-control input-sm"><?php foreach (epc_crm_ticket_priorities() as $k => $lbl): ?><option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option><?php endforeach; ?></select>
			<input type="text" name="message" class="form-control input-sm" placeholder="First message">
			<button type="submit" class="btn btn-sm btn-primary">Open ticket</button>
		</form>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Subject</th><th>Customer</th><th>Order</th><th>Status</th><th>Priority</th><th>Updated</th></tr></thead>
			<tbody>
			<?php foreach ($tickets as $t): ?>
				<tr>
					<td><?php echo epc_crm_h($t['subject']); ?></td>
					<td><?php echo (int)$t['customer_user_id'] ? ('#' . (int)$t['customer_user_id'] . ' ' . epc_crm_h($t['customer_email'] ?? '')) : '—'; ?></td>
					<td><?php echo (int)$t['order_id'] ? ('#' . (int)$t['order_id']) : '—'; ?></td>
					<td><span class="label label-warning"><?php echo epc_crm_h($t['status']); ?></span></td>
					<td><?php echo epc_crm_h($t['priority']); ?></td>
					<td><?php echo epc_crm_h(date('Y-m-d', (int)$t['time_updated'])); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>

<?php elseif ($tab === 'projects'): ?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-tasks"></i> Projects (Gantt-lite)</h4>
		<form id="epc_crm_project_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
			<input type="text" name="name" class="form-control input-sm" placeholder="Project name" required>
			<input type="number" name="opportunity_id" class="form-control input-sm" placeholder="Opp ID" value="0">
			<input type="date" name="start_date" class="form-control input-sm" value="<?php echo epc_crm_h(date('Y-m-d')); ?>">
			<input type="date" name="end_date" class="form-control input-sm" value="<?php echo epc_crm_h(date('Y-m-d', time() + 86400 * 60)); ?>">
			<select name="status" class="form-control input-sm"><?php foreach (epc_crm_project_statuses() as $k => $lbl): ?><option value="<?php echo epc_crm_h($k); ?>"><?php echo epc_crm_h($lbl); ?></option><?php endforeach; ?></select>
			<button type="submit" class="btn btn-sm btn-primary">Add project</button>
		</form>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Project</th><th>Status</th><th>Progress</th><th>Timeline</th><th>Linked</th></tr></thead>
			<tbody>
			<?php foreach ($projects as $p):
				$prog = (int)$p['progress_pct'];
				$start = (int)$p['start_date'];
				$end = (int)$p['end_date'];
				$span = max(1, $end - $start);
				$nowPos = $start > 0 ? min(100, max(0, (int)round((time() - $start) / $span * 100))) : 0;
			?>
				<tr>
					<td><strong><?php echo epc_crm_h($p['name']); ?></strong></td>
					<td><span class="label label-primary"><?php echo epc_crm_h($p['status']); ?></span></td>
					<td style="min-width:140px;">
						<div class="progress" style="margin:0;height:18px;">
							<div class="progress-bar progress-bar-success" style="width:<?php echo $prog; ?>%"><?php echo $prog; ?>%</div>
						</div>
					</td>
					<td>
						<div style="background:#e2e8f0;height:8px;border-radius:4px;position:relative;max-width:200px;">
							<div style="background:#6366f1;height:8px;border-radius:4px;width:<?php echo $prog; ?>%;"></div>
							<?php if ($start && $end): ?><span style="font-size:10px;color:#64748b;"><?php echo epc_crm_h(date('d M', $start)); ?> → <?php echo epc_crm_h(date('d M', $end)); ?></span><?php endif; ?>
						</div>
					</td>
					<td><?php echo epc_crm_h($p['opp_title'] ?: ('Order #' . (int)$p['order_id'])); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>

<?php elseif ($tab === 'contracts'): ?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-refresh"></i> Contracts &amp; recurring billing</h4>
		<form id="epc_crm_contract_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
			<input type="text" name="title" class="form-control input-sm" placeholder="Title" required>
			<input type="number" name="customer_user_id" class="form-control input-sm" placeholder="User ID">
			<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
			<select name="billing_interval" class="form-control input-sm">
				<option value="monthly">Monthly</option><option value="quarterly">Quarterly</option><option value="yearly">Yearly</option><option value="once">Once</option>
			</select>
			<input type="date" name="next_billing_date" class="form-control input-sm" value="<?php echo epc_crm_h(date('Y-m-d', time() + 86400 * 30)); ?>">
			<select name="status" class="form-control input-sm"><option value="active">Active</option><option value="draft">Draft</option></select>
			<button type="submit" class="btn btn-sm btn-primary">Add contract</button>
		</form>
		<h5>Upcoming billing (90 days)</h5>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Title</th><th>Amount</th><th>Interval</th><th>Next bill</th><th>Status</th></tr></thead>
			<tbody>
			<?php foreach ($contractsDue as $c): ?>
				<tr>
					<td><?php echo epc_crm_h($c['title']); ?></td>
					<td><?php echo epc_crm_money($c['amount']); ?> <?php echo epc_crm_h($c['currency_code']); ?></td>
					<td><?php echo epc_crm_h($c['billing_interval']); ?></td>
					<td><?php echo (int)$c['next_billing_date'] ? epc_crm_h(date('Y-m-d', (int)$c['next_billing_date'])) : '—'; ?></td>
					<td><?php echo epc_crm_h($c['status']); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
		<h5>All contracts</h5>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Title</th><th>Customer</th><th>Amount</th><th>Next</th><th>Status</th></tr></thead>
			<tbody>
			<?php foreach ($contracts as $c): ?>
				<tr>
					<td><?php echo epc_crm_h($c['title']); ?></td>
					<td><?php echo (int)$c['customer_user_id'] ? ('#' . (int)$c['customer_user_id']) : '—'; ?></td>
					<td><?php echo epc_crm_money($c['amount']); ?></td>
					<td><?php echo (int)$c['next_billing_date'] ? epc_crm_h(date('Y-m-d', (int)$c['next_billing_date'])) : '—'; ?></td>
					<td><?php echo epc_crm_h($c['status']); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>

<?php elseif ($tab === 'expenses'): ?>
	<div class="epc-crm-section">
		<h4><i class="fa fa-money"></i> Expense reports</h4>
		<form id="epc_crm_expense_form" class="form-inline epc-crm-form-inline" style="margin-bottom:12px;">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_crm_h($csrf); ?>">
			<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
			<input type="text" name="category" class="form-control input-sm" placeholder="Category" value="travel">
			<input type="text" name="receipt_note" class="form-control input-sm" placeholder="Receipt note">
			<select name="status" class="form-control input-sm"><option value="submitted">Submit</option><option value="draft">Draft</option></select>
			<button type="submit" class="btn btn-sm btn-primary">Add expense</button>
		</form>
		<table class="table table-striped table-bordered table-condensed epc-erp-table-compact">
			<thead><tr><th>Employee</th><th>Amount</th><th>Category</th><th>Status</th><th>Receipt</th><th></th></tr></thead>
			<tbody>
			<?php foreach ($expenses as $e): ?>
				<tr>
					<td>#<?php echo (int)$e['employee_user_id']; ?></td>
					<td><?php echo epc_crm_money($e['amount']); ?> AED</td>
					<td><?php echo epc_crm_h($e['category']); ?></td>
					<td><span class="label label-default"><?php echo epc_crm_h($e['status']); ?></span></td>
					<td><?php echo epc_crm_h(mb_substr($e['receipt_note'], 0, 60)); ?></td>
					<td><?php if ($e['status'] === 'submitted'): ?><button type="button" class="btn btn-xs btn-success epc-crm-approve-expense" data-id="<?php echo (int)$e['id']; ?>">Approve → cash</button><?php endif; ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>
<?php endif; ?>
