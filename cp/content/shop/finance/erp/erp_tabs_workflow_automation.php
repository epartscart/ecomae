<?php
/**
 * ERP tab — Workflow Automation (DIAGNOSTIC — minimal version).
 * If this renders, the issue was in the previous file content.
 * If this still 500s, the issue is upstream of the tab include.
 */
defined('_ASTEXE_') or die('No access');
?>
<div style="padding:30px;background:#e3f2fd;border:2px solid #1565c0;border-radius:8px;margin:20px;">
	<h3 style="color:#0d47a1;margin-top:0;">Workflow Automation — Diagnostic</h3>
	<p><strong>If you see this message, the tab file is loading correctly.</strong></p>
	<p>PHP version: <?php echo phpversion(); ?></p>
	<p>Tab file: <?php echo __FILE__; ?></p>
	<p>Timestamp: <?php echo date('Y-m-d H:i:s'); ?></p>
	<p>db_link type: <?php echo isset($db_link) ? get_class($db_link) : 'NOT SET'; ?></p>
</div>
