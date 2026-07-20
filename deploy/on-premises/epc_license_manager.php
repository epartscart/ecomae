<?php
/**
 * ecomae ERP — License Manager
 *
 * Handles license validation, activation (online + offline), enforcement,
 * and periodic health reporting back to BOS (when connected).
 *
 * License format: LIC-YYYY-XXXX-XXXX
 *   Encoded: year, tier (Standard/Professional/Enterprise), module set, user count
 *
 * Activation modes:
 *   - Online: POST to BOS /api/v1/licenses/activate.php with server fingerprint.
 *     On success, the response also carries the core-engine bundle
 *     (core/dp_core.php, dp_content.php, dp_module.php, dp_template.php) that
 *     this app cannot run without — those files are proprietary and are never
 *     checked into this repository, so this activation call is what actually
 *     makes an on-prem install bootable.
 *   - Offline: Generate activation request file -> upload to BOS portal -> download
 *     activation cert (core bundle must be delivered separately by support for
 *     air-gapped installs, since there is no online call to carry it).
 *
 * Enforcement:
 *   - Checked on every admin page load (cached 1hr in Redis/file)
 *   - Grace period: 14 days after expiry before lockout
 *   - Lockout: read-only mode (no new transactions), admin can still export data
 *
 * Every activation certificate is RSA-SHA256 signed by the license server.
 * This client only ever holds the PUBLIC key (license_public_key.pem, shipped
 * alongside this file) — it can verify a cert is genuine without being able to
 * forge one, which closes the old gap where any locally-edited cert file was
 * accepted at face value.
 */
defined('_ASTEXE_') or die('No access');

class EpcLicenseManager
{
	private string $licenseKey;
	private string $storagePath;
	private ?array $licenseData = null;

	private const GRACE_PERIOD_DAYS = 14;
	private const CACHE_TTL = 3600;
	private const BOS_API_URL = 'https://www.ecomae.com/api/v1';

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
	 * Activate license online: posts server fingerprint to BOS, receives a
	 * signed activation cert AND the core-engine bundle, and installs both.
	 */
	public function activateOnline(): array
	{
		if (empty($this->licenseKey)) {
			return array('success' => false, 'error' => 'No license key configured');
		}

		$fingerprint = $this->getServerFingerprint();

		$payload = json_encode(array(
			'license_key' => $this->licenseKey,
			'fingerprint' => $fingerprint,
			'hostname' => gethostname(),
			'ip' => $this->getServerIP(),
			'php_version' => PHP_VERSION,
			'os' => PHP_OS,
			'timestamp' => time(),
		));

		$ch = curl_init(self::BOS_API_URL . '/licenses/activate.php');
		curl_setopt_array($ch, array(
			CURLOPT_POST => true,
			CURLOPT_POSTFIELDS => $payload,
			CURLOPT_HTTPHEADER => array('Content-Type: application/json', 'Accept: application/json'),
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_TIMEOUT => 60,
			CURLOPT_SSL_VERIFYPEER => true,
		));
		$response = curl_exec($ch);
		$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);

		if ($httpCode === 200 && $response) {
			$data = json_decode($response, true);
			if (!empty($data['success']) && isset($data['activation_cert'])) {
				if (!$this->verifySignature($data['activation_cert'])) {
					return array('success' => false, 'error' => 'Activation certificate failed signature verification.');
				}
				$this->saveActivationCert($data['activation_cert']);
				$this->clearCache();

				if (!empty($data['core_bundle'])) {
					$installed = $this->installCoreBundle($data['core_bundle']);
					if (!$installed) {
						return array(
							'success' => true,
							'message' => 'License activated, but the core engine bundle could not be installed automatically. See storage/license for the raw bundle and install it manually.',
						);
					}
				}

				return array('success' => true, 'message' => 'License activated successfully');
			}
			if (isset($data['message'])) {
				return array('success' => false, 'error' => $data['message']);
			}
		}

		return array('success' => false, 'error' => 'Activation failed (HTTP ' . $httpCode . '). Use offline activation.');
	}

	/**
	 * Extract core/dp_*.php from the base64 tar.gz bundle into the app root.
	 */
	private function installCoreBundle(string $base64Bundle): bool
	{
		$docRoot = rtrim((string) $_SERVER['DOCUMENT_ROOT'], '/');
		if ($docRoot === '') {
			return false;
		}
		$bytes = base64_decode($base64Bundle, true);
		if ($bytes === false) {
			return false;
		}
		$tmpTar = tempnam(sys_get_temp_dir(), 'epc_core_bundle_') . '.tar.gz';
		file_put_contents($tmpTar, $bytes);
		exec('tar -xzf ' . escapeshellarg($tmpTar) . ' -C ' . escapeshellarg($docRoot), $out, $exitCode);
		@unlink($tmpTar);
		return $exitCode === 0 && is_file($docRoot . '/core/dp_core.php');
	}

	/**
	 * Generate offline activation request (for air-gapped servers).
	 */
	public function generateOfflineRequest(): string
	{
		$request = array(
			'license_key' => $this->licenseKey,
			'fingerprint' => $this->getServerFingerprint(),
			'hostname' => gethostname(),
			'ip' => $this->getServerIP(),
			'generated_at' => date('Y-m-d H:i:s'),
			'version' => '1.0',
		);

		$encoded = base64_encode(json_encode($request));
		$filename = 'ecomae-activation-request-' . date('Ymd') . '.txt';
		$filepath = $this->storagePath . '/' . $filename;
		file_put_contents($filepath, $encoded);

		return $filepath;
	}

	/**
	 * Import offline activation certificate (downloaded from BOS portal).
	 * The certificate's signature is verified against the embedded public
	 * key before it is trusted — a hand-edited or self-forged cert file is
	 * rejected.
	 */
	public function importOfflineCert(string $certContent): array
	{
		$decoded = json_decode(base64_decode($certContent), true);
		if (!$decoded || !isset($decoded['license_key']) || !isset($decoded['signature'])) {
			return array('success' => false, 'error' => 'Invalid activation certificate format');
		}

		if ($decoded['license_key'] !== $this->licenseKey) {
			return array('success' => false, 'error' => 'Certificate does not match current license key');
		}

		$fingerprint = $this->getServerFingerprint();
		if (isset($decoded['fingerprint']) && $decoded['fingerprint'] !== $fingerprint) {
			return array('success' => false, 'error' => 'Certificate was generated for a different server');
		}

		if (!$this->verifySignature($decoded)) {
			return array('success' => false, 'error' => 'Certificate failed signature verification — it may be corrupted or tampered with.');
		}

		$this->saveActivationCert($decoded);
		$this->clearCache();
		return array('success' => true, 'message' => 'Offline activation successful');
	}

	/**
	 * Verify an activation cert's RSA-SHA256 signature against the shipped
	 * public key. Returns false (never true) if the public key hasn't been
	 * installed yet, so an un-configured client fails closed, not open.
	 */
	private function verifySignature(array $cert): bool
	{
		$publicKeyPem = $this->loadPublicKey();
		if ($publicKeyPem === null) {
			return false;
		}
		$publicKey = openssl_pkey_get_public($publicKeyPem);
		if ($publicKey === false) {
			return false;
		}
		$signature = base64_decode((string) ($cert['signature'] ?? ''), true);
		if ($signature === false || $signature === '') {
			return false;
		}
		$dataForVerification = $cert;
		unset($dataForVerification['signature']);
		$payload = json_encode($dataForVerification, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		$result = openssl_verify($payload, $signature, $publicKey, OPENSSL_ALGO_SHA256);
		return $result === 1;
	}

	private function loadPublicKey(): ?string
	{
		$path = getenv('LICENSE_PUBLIC_KEY_PATH') ?: (__DIR__ . '/license_public_key.pem');
		if (!is_file($path)) {
			return null;
		}
		$contents = file_get_contents($path);
		if ($contents === false || strpos($contents, 'PLACEHOLDER') !== false) {
			return null;
		}
		return $contents;
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
	 * Report health to BOS (if connector enabled). Authenticated by the
	 * license key itself — no separate BOS token needed.
	 */
	public function reportHealth(): array
	{
		$syncMode = getenv('BOS_SYNC_MODE') ?: 'disabled';

		if ($syncMode === 'disabled' || empty($this->licenseKey)) {
			return array('reported' => false, 'reason' => 'BOS connector disabled');
		}

		$health = array(
			'license_key' => $this->licenseKey,
			'status' => $this->validate()['status'],
			'uptime' => $this->getUptime(),
			'disk_free_gb' => round(disk_free_space('/') / (1024 ** 3), 1),
			'memory_usage_mb' => round(memory_get_usage(true) / (1024 ** 2), 1),
			'php_version' => PHP_VERSION,
			'db_size_mb' => $this->getDatabaseSizeMB(),
			'last_backup' => $this->getLastBackupTime(),
			'reported_at' => date('c'),
		);

		$ch = curl_init(self::BOS_API_URL . '/on-premises/health.php');
		curl_setopt_array($ch, array(
			CURLOPT_POST => true,
			CURLOPT_POSTFIELDS => json_encode($health),
			CURLOPT_HTTPHEADER => array('Content-Type: application/json'),
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_TIMEOUT => 15,
		));
		$response = curl_exec($ch);
		$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);

		return array('reported' => $httpCode === 200, 'http_code' => $httpCode);
	}

	// --- Private helpers ---

	private function performValidation(): array
	{
		$default = array(
			'valid' => false,
			'status' => 'no_license',
			'tier' => 'None',
			'modules' => array(),
			'users_max' => 0,
			'expires' => '',
			'grace_remaining' => 0,
		);

		if (empty($this->licenseKey)) {
			return $default;
		}

		if (!preg_match('/^LIC-(\d{4})-([A-Z0-9]{4})-([A-Z0-9]{4})$/', $this->licenseKey)) {
			$default['status'] = 'invalid_format';
			return $default;
		}

		$cert = $this->loadActivationCert();
		if ($cert === null) {
			$default['status'] = 'not_activated';
			return $default;
		}

		if (!$this->verifySignature($cert)) {
			$default['status'] = 'invalid_signature';
			return $default;
		}

		$currentFingerprint = $this->getServerFingerprint();
		if (isset($cert['fingerprint']) && $cert['fingerprint'] !== $currentFingerprint) {
			$default['status'] = 'server_mismatch';
			return $default;
		}

		$expiresAt = $cert['expires_at'] ?? '';
		if (!empty($expiresAt)) {
			$expiryTime = strtotime($expiresAt);
			$now = time();
			if ($now > $expiryTime) {
				$daysExpired = (int) (($now - $expiryTime) / 86400);
				if ($daysExpired > self::GRACE_PERIOD_DAYS) {
					$default['status'] = 'expired_locked';
					$default['expires'] = $expiresAt;
					return $default;
				}
				return array(
					'valid' => true,
					'status' => 'grace_period',
					'tier' => $cert['tier'] ?? 'Standard',
					'modules' => $cert['modules'] ?? array('all'),
					'users_max' => $cert['users_max'] ?? 25,
					'expires' => $expiresAt,
					'grace_remaining' => self::GRACE_PERIOD_DAYS - $daysExpired,
				);
			}
		}

		return array(
			'valid' => true,
			'status' => 'active',
			'tier' => $cert['tier'] ?? 'Standard',
			'modules' => $cert['modules'] ?? array('all'),
			'users_max' => $cert['users_max'] ?? 25,
			'expires' => $expiresAt ?: 'Perpetual',
			'grace_remaining' => 0,
		);
	}

	private function getServerFingerprint(): string
	{
		$components = array(
			php_uname('n'),
			php_uname('m'),
		);

		if (PHP_OS_FAMILY === 'Linux') {
			$mac = @shell_exec("cat /sys/class/net/$(ip route show default | awk '/default/ {print \$5}')/address 2>/dev/null");
			if ($mac) {
				$components[] = trim($mac);
			}
		}

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
			$ip = @shell_exec("hostname -I 2>/dev/null | awk '{print \$1}'");
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
			$uptime = (float) trim(file_get_contents('/proc/uptime'));
			$days = (int) ($uptime / 86400);
			$hours = (int) (($uptime % 86400) / 3600);
			return "{$days}d {$hours}h";
		}
		return 'unknown';
	}

	private function getDatabaseSizeMB(): float
	{
		try {
			$dbHost = getenv('DB_HOST') ?: 'db';
			$dbPort = getenv('DB_PORT') ?: '3306';
			$dbName = getenv('DB_DATABASE') ?: 'ecomae_erp';
			$dbUser = getenv('DB_USERNAME') ?: 'ecomae';
			$dbPass = getenv('DB_PASSWORD') ?: '';
			$pdo = new PDO(
				"mysql:host={$dbHost};port={$dbPort};dbname={$dbName}",
				$dbUser,
				$dbPass,
				array(PDO::ATTR_TIMEOUT => 3)
			);
			$size = $pdo->query(
				"SELECT ROUND(SUM(data_length + index_length) / 1024 / 1024, 1) FROM information_schema.tables WHERE table_schema = " . $pdo->quote($dbName)
			)->fetchColumn();
			return (float) $size;
		} catch (Exception $e) {
			return 0.0;
		}
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
