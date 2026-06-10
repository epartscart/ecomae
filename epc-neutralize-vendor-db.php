<?php
/**
 * Neutralize user-visible CP menu / config labels (UMAPI → Epart catalog).
 * Run once: https://www.epartscart.com/epc-neutralize-vendor-db.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$updates = array();

$stmt = $pdo->prepare(
	'UPDATE `config_groups` SET `caption` = ? WHERE `id` = ? OR `caption` LIKE ? OR `caption` LIKE ?'
);
$stmt->execute(array('Epart catalog settings', 13, '%UMAPI%', '%umapi%'));
$updates['config_groups'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `config_items` SET `caption` = REPLACE(`caption`, 'UMAPI', 'Epart catalog')
	 WHERE `caption` LIKE '%UMAPI%' OR `caption` LIKE '%umapi%'"
);
$stmt->execute();
$updates['config_items'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `config_items` SET `hint` = REPLACE(`hint`, 'UMAPI', 'Epart catalog')
	 WHERE `hint` LIKE '%UMAPI%' OR `hint` LIKE '%umapi%'"
);
$stmt->execute();
$updates['config_items_hint'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'UMAPI', 'Epart catalog')
	 WHERE `value` LIKE '%UMAPI%'"
);
$stmt->execute();
$updates['lang_umapi'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'CrossBase', 'epartscross')
	 WHERE `value` LIKE '%CrossBase%'"
);
$stmt->execute();
$updates['lang_crossbase'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'Crossbase', 'epartscross')
	 WHERE `value` LIKE '%Crossbase%'"
);
$stmt->execute();
$updates['lang_crossbase_lower'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'crossbase', 'epartscross')
	 WHERE `value` LIKE '%crossbase%'"
);
$stmt->execute();
$updates['lang_crossbase_lc'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'UMAPI catalog', 'eparts catalog')
	 WHERE `value` LIKE '%UMAPI catalog%'"
);
$stmt->execute();
$updates['lang_umapi_catalog'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'umapi catalog', 'eparts catalog')
	 WHERE `value` LIKE '%umapi catalog%'"
);
$stmt->execute();
$updates['lang_umapi_catalog_lc'] = $stmt->rowCount();

$stmt = $pdo->prepare(
	"UPDATE `lang_text_strings_translation` SET `value` = REPLACE(`value`, 'Docpart', 'ECOM AE')
	 WHERE `value` LIKE '%Docpart%' AND `value` NOT LIKE '%DocpartMailer%'"
);
$stmt->execute();
$updates['lang_docpart'] = $stmt->rowCount();

echo "Neutralized vendor strings in database:\n";
foreach ($updates as $key => $count) {
	echo "  {$key}: {$count} row(s)\n";
}
echo "Done.\n";
