<?php
/**
 * CP panel: manage accessories categories / sub-categories and filter terms.
 * Included from accessories_listings.php when tab=taxonomy.
 */
defined('_ASTEXE_') or die('No access');

$adminTree = epc_acc_admin_category_tree($db_link);
$termTypes = array(
	'make' => 'Make',
	'model' => 'Model',
	'year' => 'Year',
	'city' => 'City',
	'condition' => 'Condition',
);
$activeTermType = preg_replace('/[^a-z_]/', '', strtolower((string) ($_GET['term'] ?? 'make')));
if (!isset($termTypes[$activeTermType])) {
	$activeTermType = 'make';
}
$makesForModel = epc_acc_get_terms($db_link, 'make', true);
$terms = epc_acc_get_terms($db_link, $activeTermType, true);
$csrf = htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8');
$taxBase = htmlspecialchars($baseUrl . '?tab=taxonomy', ENT_QUOTES, 'UTF-8');
?>
<div class="col-lg-12 epc-acc-cp">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Categories &amp; sub-categories
			<span class="pull-right muted">Add / rename / deactivate / remove</span>
		</div>
		<div class="panel-body">
			<form method="post" class="filters" style="margin-bottom:14px;">
				<input type="hidden" name="action" value="save_category" />
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
				<input class="form-control w-q" name="label" required placeholder="New category label" />
				<select class="form-control" name="parent_id">
					<option value="0">Top-level category</option>
					<?php foreach ($adminTree as $p) { ?>
						<option value="<?php echo (int) $p['id']; ?>">Sub of: <?php echo htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8'); ?></option>
					<?php } ?>
				</select>
				<input class="form-control" type="number" name="sort_order" min="0" placeholder="Sort" style="width:90px;" />
				<button type="submit" class="btn btn-primary btn-sm">Add category / sub</button>
			</form>

			<table class="table table-striped table-condensed">
				<thead>
					<tr>
						<th>Category</th>
						<th>Slug</th>
						<th>Sort</th>
						<th>Status</th>
						<th>Sub-categories</th>
						<th style="min-width:220px;">Actions</th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($adminTree as $p) { ?>
					<tr>
						<td><strong><?php echo htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8'); ?></strong></td>
						<td class="muted"><?php echo htmlspecialchars($p['slug'], ENT_QUOTES, 'UTF-8'); ?></td>
						<td><?php echo (int) $p['sort_order']; ?></td>
						<td>
							<?php if (!empty($p['active'])) { ?>
								<span class="label badge-pub">Active</span>
							<?php } else { ?>
								<span class="label badge-unpub">Off</span>
							<?php } ?>
						</td>
						<td><?php echo count($p['children']); ?></td>
						<td>
							<form method="post" style="display:inline-block;margin:0 4px 4px 0;">
								<input type="hidden" name="action" value="save_category" />
								<input type="hidden" name="id" value="<?php echo (int) $p['id']; ?>" />
								<input type="hidden" name="parent_id" value="0" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
								<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
								<input class="form-control input-sm" name="label" value="<?php echo htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8'); ?>" style="width:140px;display:inline-block;" />
								<input class="form-control input-sm" type="number" name="sort_order" value="<?php echo (int) $p['sort_order']; ?>" style="width:70px;display:inline-block;" />
								<button class="btn btn-xs btn-default" type="submit">Save</button>
							</form>
							<form method="post" style="display:inline;">
								<input type="hidden" name="action" value="set_category_active" />
								<input type="hidden" name="id" value="<?php echo (int) $p['id']; ?>" />
								<input type="hidden" name="active" value="<?php echo !empty($p['active']) ? '0' : '1'; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
								<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
								<button class="btn btn-xs btn-warning" type="submit"><?php echo !empty($p['active']) ? 'Deactivate' : 'Activate'; ?></button>
							</form>
							<form method="post" style="display:inline;" onsubmit="return confirm('Delete category and its empty sub-categories?');">
								<input type="hidden" name="action" value="delete_category" />
								<input type="hidden" name="id" value="<?php echo (int) $p['id']; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
								<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
								<button class="btn btn-xs btn-danger" type="submit">Delete</button>
							</form>
						</td>
					</tr>
					<?php foreach ($p['children'] as $c) { ?>
						<tr>
							<td class="muted" style="padding-left:28px;">↳ <?php echo htmlspecialchars($c['label'], ENT_QUOTES, 'UTF-8'); ?></td>
							<td class="muted"><?php echo htmlspecialchars($c['slug'], ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo (int) $c['sort_order']; ?></td>
							<td>
								<?php if (!empty($c['active'])) { ?>
									<span class="label badge-pub">Active</span>
								<?php } else { ?>
									<span class="label badge-unpub">Off</span>
								<?php } ?>
							</td>
							<td class="muted">sub</td>
							<td>
								<form method="post" style="display:inline-block;margin:0 4px 4px 0;">
									<input type="hidden" name="action" value="save_category" />
									<input type="hidden" name="id" value="<?php echo (int) $c['id']; ?>" />
									<input type="hidden" name="parent_id" value="<?php echo (int) $p['id']; ?>" />
									<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
									<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
									<input class="form-control input-sm" name="label" value="<?php echo htmlspecialchars($c['label'], ENT_QUOTES, 'UTF-8'); ?>" style="width:140px;display:inline-block;" />
									<input class="form-control input-sm" type="number" name="sort_order" value="<?php echo (int) $c['sort_order']; ?>" style="width:70px;display:inline-block;" />
									<button class="btn btn-xs btn-default" type="submit">Save</button>
								</form>
								<form method="post" style="display:inline;">
									<input type="hidden" name="action" value="set_category_active" />
									<input type="hidden" name="id" value="<?php echo (int) $c['id']; ?>" />
									<input type="hidden" name="active" value="<?php echo !empty($c['active']) ? '0' : '1'; ?>" />
									<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
									<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
									<button class="btn btn-xs btn-warning" type="submit"><?php echo !empty($c['active']) ? 'Deactivate' : 'Activate'; ?></button>
								</form>
								<form method="post" style="display:inline;" onsubmit="return confirm('Delete this sub-category?');">
									<input type="hidden" name="action" value="delete_category" />
									<input type="hidden" name="id" value="<?php echo (int) $c['id']; ?>" />
									<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
									<input type="hidden" name="back" value="<?php echo $taxBase; ?>" />
									<button class="btn btn-xs btn-danger" type="submit">Delete</button>
								</form>
							</td>
						</tr>
					<?php } ?>
				<?php } ?>
				</tbody>
			</table>
		</div>
	</div>

	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Filter lists — Make · Model · Year · City · Condition
		</div>
		<div class="panel-body">
			<ul class="nav nav-pills" style="margin-bottom:14px;">
				<?php foreach ($termTypes as $tk => $tl) {
					$href = $baseUrl . '?tab=taxonomy&term=' . rawurlencode($tk);
					$cls = ($tk === $activeTermType) ? ' class="active"' : '';
					echo '<li' . $cls . '><a href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '">' . htmlspecialchars($tl, ENT_QUOTES, 'UTF-8') . '</a></li>';
				} ?>
			</ul>

			<form method="post" class="filters" style="margin-bottom:14px;">
				<input type="hidden" name="action" value="save_term" />
				<input type="hidden" name="term_type" value="<?php echo htmlspecialchars($activeTermType, ENT_QUOTES, 'UTF-8'); ?>" />
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<input type="hidden" name="back" value="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy&term=' . $activeTermType, ENT_QUOTES, 'UTF-8'); ?>" />
				<input class="form-control w-q" name="label" required placeholder="Add <?php echo htmlspecialchars($termTypes[$activeTermType], ENT_QUOTES, 'UTF-8'); ?>" />
				<?php if ($activeTermType === 'model') { ?>
					<select class="form-control" name="parent_id">
						<option value="0">Any make (general)</option>
						<?php foreach ($makesForModel as $mk) { ?>
							<option value="<?php echo (int) $mk['id']; ?>"><?php echo htmlspecialchars($mk['label'], ENT_QUOTES, 'UTF-8'); ?></option>
						<?php } ?>
					</select>
				<?php } else { ?>
					<input type="hidden" name="parent_id" value="0" />
				<?php } ?>
				<input class="form-control" type="number" name="sort_order" min="0" placeholder="Sort" style="width:90px;" />
				<button type="submit" class="btn btn-primary btn-sm">Add <?php echo htmlspecialchars($termTypes[$activeTermType], ENT_QUOTES, 'UTF-8'); ?></button>
			</form>

			<table class="table table-striped table-condensed">
				<thead>
					<tr>
						<th>Label</th>
						<th>Value</th>
						<?php if ($activeTermType === 'model') { ?><th>Make</th><?php } ?>
						<th>Sort</th>
						<th>Status</th>
						<th style="min-width:220px;">Actions</th>
					</tr>
				</thead>
				<tbody>
				<?php
				$makeMap = array();
				foreach ($makesForModel as $mk) {
					$makeMap[(int) $mk['id']] = $mk['label'];
				}
				if (!$terms) {
					echo '<tr><td colspan="6" class="muted">No terms yet — add above (or seed from taxonomy JSON on first load).</td></tr>';
				}
				foreach ($terms as $term) {
					?>
					<tr>
						<td><strong><?php echo htmlspecialchars($term['label'], ENT_QUOTES, 'UTF-8'); ?></strong></td>
						<td class="muted"><?php echo htmlspecialchars($term['value'], ENT_QUOTES, 'UTF-8'); ?></td>
						<?php if ($activeTermType === 'model') { ?>
							<td class="muted"><?php echo htmlspecialchars($makeMap[$term['parent_id']] ?? '—', ENT_QUOTES, 'UTF-8'); ?></td>
						<?php } ?>
						<td><?php echo (int) $term['sort_order']; ?></td>
						<td>
							<?php if (!empty($term['active'])) { ?>
								<span class="label badge-pub">Active</span>
							<?php } else { ?>
								<span class="label badge-unpub">Off</span>
							<?php } ?>
						</td>
						<td>
							<form method="post" style="display:inline-block;margin:0 4px 4px 0;">
								<input type="hidden" name="action" value="save_term" />
								<input type="hidden" name="id" value="<?php echo (int) $term['id']; ?>" />
								<input type="hidden" name="term_type" value="<?php echo htmlspecialchars($activeTermType, ENT_QUOTES, 'UTF-8'); ?>" />
								<input type="hidden" name="parent_id" value="<?php echo (int) $term['parent_id']; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
								<input type="hidden" name="back" value="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy&term=' . $activeTermType, ENT_QUOTES, 'UTF-8'); ?>" />
								<input class="form-control input-sm" name="label" value="<?php echo htmlspecialchars($term['label'], ENT_QUOTES, 'UTF-8'); ?>" style="width:140px;display:inline-block;" />
								<input class="form-control input-sm" type="number" name="sort_order" value="<?php echo (int) $term['sort_order']; ?>" style="width:70px;display:inline-block;" />
								<button class="btn btn-xs btn-default" type="submit">Save</button>
							</form>
							<form method="post" style="display:inline;">
								<input type="hidden" name="action" value="set_term_active" />
								<input type="hidden" name="id" value="<?php echo (int) $term['id']; ?>" />
								<input type="hidden" name="active" value="<?php echo !empty($term['active']) ? '0' : '1'; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
								<input type="hidden" name="back" value="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy&term=' . $activeTermType, ENT_QUOTES, 'UTF-8'); ?>" />
								<button class="btn btn-xs btn-warning" type="submit"><?php echo !empty($term['active']) ? 'Deactivate' : 'Activate'; ?></button>
							</form>
							<form method="post" style="display:inline;" onsubmit="return confirm('Remove this term?');">
								<input type="hidden" name="action" value="delete_term" />
								<input type="hidden" name="id" value="<?php echo (int) $term['id']; ?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
								<input type="hidden" name="back" value="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy&term=' . $activeTermType, ENT_QUOTES, 'UTF-8'); ?>" />
								<button class="btn btn-xs btn-danger" type="submit">Delete</button>
							</form>
						</td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
			<p class="muted">These lists power storefront filters and the listing form dropdowns on Accessories Marketplace.</p>
		</div>
	</div>
</div>
