<?php
/**
 * Organization engine — assembles the enterprise structure tree from the
 * existing per-tenant masters so the whole hierarchy can be viewed in one place:
 *
 *   Group / Legal entity  ->  Business unit (+ nested sub-units via parent_id)
 *                         ->  Departments (config)  ->  Teams / staff
 *
 * plus the approval hierarchy (workflow rules) and financial dimensions / cost
 * centres. Read-only assembler — the underlying records are still maintained in
 * the Business Unit and Staff modules; this gives the bird's-eye org chart that
 * was previously only a placeholder.
 */

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_pdf_modules.php';

if (!function_exists('epc_bos_org_tree')) {

	/** @return array<string,mixed> */
	function epc_bos_org_tree(PDO $db): array
	{
		$entities = array();
		$bus = array();
		$dims = array();
		try { $entities = epc_erp_pm_list($db, 'epc_erp_pm_legal_entities', true); } catch (Throwable $e) {}
		try { $bus = epc_erp_pm_list($db, 'epc_erp_pm_business_units', true); } catch (Throwable $e) {}
		try { $dims = epc_erp_pm_list($db, 'epc_erp_pm_dimensions', true); } catch (Throwable $e) {}

		// Group business units by legal entity, then nest by parent_id.
		$buByEntity = array();
		foreach ($bus as $b) {
			$le = (int) ($b['legal_entity_id'] ?? 0);
			$buByEntity[$le][] = $b;
		}

		$nest = function (array $list) {
			$byParent = array();
			foreach ($list as $row) {
				$byParent[(int) ($row['parent_id'] ?? 0)][] = $row;
			}
			$build = function ($pid) use (&$build, $byParent) {
				$out = array();
				foreach ($byParent[$pid] ?? array() as $row) {
					$row['children'] = $build((int) $row['id']);
					$out[] = $row;
				}
				return $out;
			};
			return $build(0);
		};

		$entityNodes = array();
		foreach ($entities as $e) {
			$list = $buByEntity[(int) $e['id']] ?? array();
			$entityNodes[] = array(
				'entity' => $e,
				'units' => $nest($list),
				'unit_count' => count($list),
			);
		}
		// Business units not attached to any legal entity.
		$orphanUnits = $nest($buByEntity[0] ?? array());

		return array(
			'entities' => $entityNodes,
			'orphan_units' => $orphanUnits,
			'departments' => epc_bos_org_departments($db),
			'dimensions' => $dims,
			'approval_hierarchy' => epc_bos_org_approval_hierarchy($db),
			'counts' => array(
				'entities' => count($entities),
				'business_units' => count($bus),
				'dimensions' => count($dims),
			),
		);
	}

	/** Departments from config with live staff counts. @return array<int,array<string,mixed>> */
	function epc_bos_org_departments(PDO $db): array
	{
		if (!function_exists('epc_erp_departments_config')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
		}
		$cfg = function_exists('epc_erp_departments_config') ? epc_erp_departments_config() : array();
		$staff = array();
		if (function_exists('epc_erp_staff_list')) {
			try { $staff = epc_erp_staff_list($db); } catch (Throwable $e) { $staff = array(); }
		}
		$countByDept = array();
		foreach ($staff as $s) {
			$d = (string) ($s['department'] ?? $s['dept'] ?? '');
			if ($d !== '') { $countByDept[$d] = ($countByDept[$d] ?? 0) + 1; }
		}
		$out = array();
		foreach ($cfg as $key => $d) {
			$out[] = array(
				'key' => $key,
				'name' => (string) ($d['name'] ?? $key),
				'icon' => (string) ($d['icon'] ?? 'fa-users'),
				'color' => (string) ($d['color'] ?? '#64748b'),
				'staff' => (int) ($countByDept[$key] ?? 0),
				'workflows' => is_array($d['workflows'] ?? null) ? $d['workflows'] : array(),
			);
		}
		return $out;
	}

	/**
	 * Approval hierarchy — summarise the workflow rules per document type so the
	 * chain of approvers is visible alongside the org chart.
	 *
	 * @return array<int,array<string,mixed>>
	 */
	function epc_bos_org_approval_hierarchy(PDO $db): array
	{
		$out = array();
		if (!is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php')) {
			return $out;
		}
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_workflow.php';
		$types = function_exists('epc_bos_wf_entity_types') ? epc_bos_wf_entity_types() : array();
		foreach ($types as $key => $label) {
			$rules = array();
			if (function_exists('epc_bos_wf_rules')) {
				try { $rules = epc_bos_wf_rules($db, (string) $key); } catch (Throwable $e) { $rules = array(); }
			}
			$out[] = array(
				'key' => (string) $key,
				'label' => is_string($label) ? $label : (string) $key,
				'rule_count' => is_array($rules) ? count($rules) : 0,
				'rules' => is_array($rules) ? $rules : array(),
			);
		}
		return $out;
	}
}
