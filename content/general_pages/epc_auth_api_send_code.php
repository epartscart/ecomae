<?php
/**
 * Storefront/CP OTP send — content path (not blocked by root epc- lockdown).
 * POST JSON: { "email", "tenant_key", "context", "return_url"? }
 */
require dirname(__DIR__, 2) . '/epc-auth-send-code.php';
