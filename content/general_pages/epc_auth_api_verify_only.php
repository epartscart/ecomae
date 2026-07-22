<?php
/**
 * Registration email OTP verify (no login session) — content path.
 * POST JSON: { "email", "code", "tenant_key", "context" }
 */
require dirname(__DIR__, 2) . '/epc-auth-otp-verify-only.php';
