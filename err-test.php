<?php
ini_set('display_errors', '1');
error_reporting(E_ALL);
header('Content-Type: text/plain');
define('_ASTEXE_', true);
require __DIR__ . '/config.php';
require __DIR__ . '/core/dp_helper.php';
require __DIR__ . '/core/dp_content.php';
require __DIR__ . '/core/dp_module.php';
require __DIR__ . '/core/dp_template.php';
require __DIR__ . '/core/dp_core.php';
echo "core loaded OK\n";
