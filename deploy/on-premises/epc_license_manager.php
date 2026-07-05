<?php
/**
 * ecomae ERP — License Manager
 *
 * Handles license validation, activation (online + offline), enforcement,
 * and periodic health reporting back to BOS (when connected).
 *
 * License format: LIC-YYYY-XXXX-XXXX-XXXX
 *   Encoded: year, tier (Standard/Professional/Enterprise), module set, user count
 *
 * Activation modes:
 *   - Online: POST to BOS /api/licenses/activate with server fingerprint
 *   - Offline: Generate activation request file → upload to BOS portal → download activation cert
 *
 * Enforcement:
 *   - Checked on every admin page load (cached 1hr in Redis/file)
 *   - Grace period: 14 days after expiry before lockout
 *   - Lockout: read-only mode (no new transactions), admin can still export data
 */
defined('_ASTEXE_') or die('No access');

class EpcLicenseManager
{
    private string $licenseKey;
    private string $storagePath;
    private ?array $licenseData = null;

    private const GRACE_PERIOD_DAYS = 14;
    private const CACHE_TTL = 3600;
    private const BOS_API_URL = 'https://www.ecomae.com/api/v1/licenses';

    public function __construct(string $licenseKey = '', string $storagePath = '')
    {
        $this->licenseKey = $licenseKey ?: (getenv('LICENSE_KEY') ?: '');
        $this->storagePath = $storagePath ?: ($_SERVER['DOCUMENT_ROOT'] . '/storage/license');

        if (!is_dir($this->storagePath)) {
            @mkdir($this->storagePath, 0700, true);
        }
    }

    /**
     * Validate current license status.
     * Returns: ['valid' => bool, 'status' => string, 'tier' => string, 'modules' => [], 'users_max' => int, 'expires' => string]
     */
    public function validate(): array
    {
        $cached = $this->getCachedValidation();
        if ($cached !== null) {
            return $cached;
        }

        $result = $this->performValidation();
        $this->cacheValidation($result);
        return $result;
    }

    /**
     * Activate license online (posts server fingerprint to BOS).
     */
    public function activateOnline(): array
    {
        if (empty($this->licenseKey)) {
            return ['success' => false, 'error' => 'No license key configured'];
        }

        $fingerprint = $this->getServerFingerprint();

        $payload = json_encode([
            'license_key' => $this->licenseKey,
            'fingerprint' => $fingerprint,
            'hostname' => gethostname(),
            'ip' => $this->getServerIP(),
            'php_version' => PHP_VERSION,
            'os' => PHP_OS,
            'timestamp' => time(),
        ]);

        $ch = curl_init(self::BOS_API_URL . '/activate');
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $payload,
            CURLOPT_HTTPHEADER => ['Content-Type: application/json', 'Accept: application/json'],
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 30,
            CURLOPT_SSL_VERIFYPEER => true,
        ]);
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        if ($httpCode === 200 && $response) {
            $data = json_decode($response, true);
            if (isset($data['activation_cert'])) {
                $this->saveActivationCert($data['activation_cert']);
                $this->clearCache();
                return ['success' => true, 'message' => 'License activated successfully'];
            }
        }

        return ['success' => false, 'error' => 'Activation failed (HTTP ' . $httpCode . '). Use offline activation.'];
    }

    /**
     * Generate offline activation request (for air-gapped servers).
     */
    public function generateOfflineRequest(): string
    {
        $request = [
            'license_key' => $this->licenseKey,
            'fingerprint' => $this->getServerFingerprint(),
            'hostname' => gethostname(),
            'ip' => $this->getServerIP(),
            'generated_at' => date('Y-m-d H:i:s'),
            'version' => '1.0',
        ];

        $encoded = base64_encode(json_encode($request));
        $filename = 'ecomae-activation-request-' . date('Ymd') . '.txt';
        $filepath = $this->storagePath . '/' . $filename;
        file_put_contents($filepath, $encoded);

        return $filepath;
    }

    /**
     * Import offline activation certificate (downloaded from BOS portal).
     */
    public function importOfflineCert(string $certContent): array
    {
        $decoded = json_decode(base64_decode($certContent), true);
        if (!$decoded || !isset($decoded['license_key']) || !isset($decoded['signature'])) {
            return ['success' => false, 'error' => 'Invalid activation certificate format'];
        }

        if ($decoded['license_key'] !== $this->licenseKey) {
            return ['success' => false, 'error' => 'Certificate does not match current license key'];
        }

        $fingerprint = $this->getServerFingerprint();
        if (isset($decoded['fingerprint']) && $decoded['fingerprint'] !== $fingerprint) {
            return ['success' => false, 'error' => 'Certificate was generated for a different server'];
        }

        $this->saveActivationCert($decoded);
        $this->clearCache();
        return ['success' => true, 'message' => 'Offline activation successful'];
    }

    /**
     * Check if a specific module is licensed.
     */
    public function isModuleLicensed(string $module): bool
    {
        $validation = $this->validate();
        if (!$validation['valid']) {
            return false;
        }
        if (in_array('all', $validation['modules'], true)) {
            return true;
        }
        return in_array(strtolower($module), array_map('strtolower', $validation['modules']), true);
    }

    /**
     * Check if user count is within license limit.
     */
    public function isUserCountAllowed(int $currentUsers): bool
    {
        $validation = $this->validate();
        if (!$validation['valid']) {
            return false;
        }
        return $currentUsers <= $validation['users_max'];
    }

    /**
     * Get license tier (Standard, Professional, Enterprise).
     */
    public function getTier(): string
    {
        $validation = $this->validate();
        return $validation['tier'] ?? 'Unknown';
    }

    /**
     * Report health to BOS (if connector enabled).
     */
    public function reportHealth(): array
    {
        $bosUrl = getenv('BOS_CONNECTOR_URL');
        $bosToken = getenv('BOS_CONNECTOR_TOKEN');
        $syncMode = getenv('BOS_SYNC_MODE') ?: 'disabled';

        if ($syncMode === 'disabled' || empty($bosUrl) || empty($bosToken)) {
            return ['reported' => false, 'reason' => 'BOS connector disabled'];
        }

        $health = [
            'license_key' => $this->licenseKey,
            'status' => $this->validate()['status'],
            'uptime' => $this->getUptime(),
            'disk_free_gb' => round(disk_free_space('/') / (1024 ** 3), 1),
            'memory_usage_mb' => round(memory_get_usage(true) / (1024 ** 2), 1),
            'php_version' => PHP_VERSION,
            'db_size_mb' => $this->getDatabaseSizeMB(),
            'last_backup' => $this->getLastBackupTime(),
            'reported_at' => date('c'),
        ];

        $ch = curl_init($bosUrl . '/api/v1/on-premises/health');
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => json_encode($health),
            CURLOPT_HTTPHEADER => [
                'Content-Type: application/json',
                'Authorization: Bearer ' . $bosToken,
            ],
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 15,
        ]);
        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);

        return ['reported' => $httpCode === 200, 'http_code' => $httpCode];
    }

    // ─── Private helpers ───────────────────────────────────────────

    private function performValidation(): array
    {
        $default = [
            'valid' => false,
            'status' => 'no_license',
            'tier' => 'None',
            'modules' => [],
            'users_max' => 0,
            'expires' => '',
            'grace_remaining' => 0,
        ];

        if (empty($this->licenseKey)) {
            return $default;
        }

        // Parse license key format: LIC-YYYY-XXXX-XXXX
        if (!preg_match('/^LIC-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$/', $this->licenseKey, $m)) {
            $default['status'] = 'invalid_format';
            return $default;
        }

        // Check activation certificate
        $cert = $this->loadActivationCert();
        if ($cert === null) {
            $default['status'] = 'not_activated';
            return $default;
        }

        // Verify fingerprint
        $currentFingerprint = $this->getServerFingerprint();
        if (isset($cert['fingerprint']) && $cert['fingerprint'] !== $currentFingerprint) {
            $default['status'] = 'server_mismatch';
            return $default;
        }

        // Check expiry
        $expiresAt = $cert['expires_at'] ?? '';
        if (!empty($expiresAt)) {
            $expiryTime = strtotime($expiresAt);
            $now = time();
            if ($now > $expiryTime) {
                $daysExpired = (int)(($now - $expiryTime) / 86400);
                if ($daysExpired > self::GRACE_PERIOD_DAYS) {
                    $default['status'] = 'expired_locked';
                    $default['expires'] = $expiresAt;
                    return $default;
                }
                // In grace period
                return [
                    'valid' => true,
                    'status' => 'grace_period',
                    'tier' => $cert['tier'] ?? 'Standard',
                    'modules' => $cert['modules'] ?? ['all'],
                    'users_max' => $cert['users_max'] ?? 25,
                    'expires' => $expiresAt,
                    'grace_remaining' => self::GRACE_PERIOD_DAYS - $daysExpired,
                ];
            }
        }

        // Valid license
        return [
            'valid' => true,
            'status' => 'active',
            'tier' => $cert['tier'] ?? 'Standard',
            'modules' => $cert['modules'] ?? ['all'],
            'users_max' => $cert['users_max'] ?? 25,
            'expires' => $expiresAt ?: 'Perpetual',
            'grace_remaining' => 0,
        ];
    }

    private function getServerFingerprint(): string
    {
        $components = [
            php_uname('n'),
            php_uname('m'),
        ];

        // Try to get MAC address
        if (PHP_OS_FAMILY === 'Linux') {
            $mac = @shell_exec("cat /sys/class/net/$(ip route show default | awk '/default/ {print $5}')/address 2>/dev/null");
            if ($mac) {
                $components[] = trim($mac);
            }
        }

        // CPU info
        if (is_readable('/proc/cpuinfo')) {
            $cpu = @file_get_contents('/proc/cpuinfo');
            if (preg_match('/model name\s*:\s*(.+)/i', $cpu, $m)) {
                $components[] = trim($m[1]);
            }
        }

        return hash('sha256', implode('|', $components));
    }

    private function getServerIP(): string
    {
        if (PHP_OS_FAMILY === 'Linux') {
            $ip = @shell_exec("hostname -I 2>/dev/null | awk '{print $1}'");
            if ($ip) {
                return trim($ip);
            }
        }
        return gethostbyname(gethostname()) ?: '0.0.0.0';
    }

    private function saveActivationCert(array $cert): void
    {
        $path = $this->storagePath . '/activation.cert';
        file_put_contents($path, json_encode($cert, JSON_PRETTY_PRINT));
        chmod($path, 0600);
    }

    private function loadActivationCert(): ?array
    {
        $path = $this->storagePath . '/activation.cert';
        if (!is_file($path)) {
            return null;
        }
        $data = json_decode(file_get_contents($path), true);
        return is_array($data) ? $data : null;
    }

    private function getCachedValidation(): ?array
    {
        $cacheFile = $this->storagePath . '/validation_cache.json';
        if (!is_file($cacheFile)) {
            return null;
        }
        $data = json_decode(file_get_contents($cacheFile), true);
        if (!$data || !isset($data['cached_at'])) {
            return null;
        }
        if (time() - $data['cached_at'] > self::CACHE_TTL) {
            return null;
        }
        unset($data['cached_at']);
        return $data;
    }

    private function cacheValidation(array $result): void
    {
        $result['cached_at'] = time();
        $cacheFile = $this->storagePath . '/validation_cache.json';
        @file_put_contents($cacheFile, json_encode($result));
    }

    private function clearCache(): void
    {
        $cacheFile = $this->storagePath . '/validation_cache.json';
        if (is_file($cacheFile)) {
            @unlink($cacheFile);
        }
    }

    private function getUptime(): string
    {
        if (is_readable('/proc/uptime')) {
            $uptime = (float)trim(file_get_contents('/proc/uptime'));
            $days = (int)($uptime / 86400);
            $hours = (int)(($uptime % 86400) / 3600);
            return "{$days}d {$hours}h";
        }
        return 'unknown';
    }

    private function getDatabaseSizeMB(): float
    {
        $dbName = getenv('DB_DATABASE') ?: 'ecomae_erp';
        // This would query the database in production
        return 0.0;
    }

    private function getLastBackupTime(): string
    {
        $backupDir = dirname($this->storagePath) . '/../backups';
        if (!is_dir($backupDir)) {
            return 'never';
        }
        $files = glob($backupDir . '/*.sql.gz');
        if (empty($files)) {
            return 'never';
        }
        $latest = max(array_map('filemtime', $files));
        return date('Y-m-d H:i', $latest);
    }
}

/**
 * Helper: quick license check for use in ERP pages.
 */
function epc_license_valid(): bool
{
    static $manager = null;
    if ($manager === null) {
        $manager = new EpcLicenseManager();
    }
    return $manager->validate()['valid'];
}

/**
 * Helper: check module access.
 */
function epc_license_module_allowed(string $module): bool
{
    static $manager = null;
    if ($manager === null) {
        $manager = new EpcLicenseManager();
    }
    return $manager->isModuleLicensed($module);
}

/**
 * Helper: get license info array for display.
 */
function epc_license_info(): array
{
    $manager = new EpcLicenseManager();
    return $manager->validate();
}
