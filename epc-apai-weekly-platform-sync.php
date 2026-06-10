<?php
/**
 * Auto Price AI — weekly platform pack + sell marketplace sync (all live tenants).
 * Same idempotent upsert as daily expand; use when operators prefer a weekly cadence.
 *
 * Cron: 0 4 * * 0 curl -s "https://www.ecomae.com/epc-apai-weekly-platform-sync.php?token=epartscart-deploy-2026"
 */
declare(strict_types=1);

$_GET['weekly'] = '1';
require __DIR__ . '/epc-apai-daily-source-expand.php';
