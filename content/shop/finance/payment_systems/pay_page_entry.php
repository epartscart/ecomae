<?php
/**
 * Demo pay page entry — forwards handler from POST.
 */
$EPC_PAY_HANDLER = isset($_POST['EPC_PAY_HANDLER']) ? preg_replace('/[^a-z0-9_]/', '', (string)$_POST['EPC_PAY_HANDLER']) : '';
require __DIR__ . '/pay_page.php';
