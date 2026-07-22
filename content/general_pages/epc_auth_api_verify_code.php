<?php
/**
 * OTP verify + login session — content path (not blocked by root epc- lockdown).
 * POST JSON: { "email", "code", "tenant_key", "context", "return_url"? }
 */
require dirname(__DIR__, 2) . '/epc-auth-verify-code.php';
