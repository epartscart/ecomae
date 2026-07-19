<?php
/**
 * PHPMailer wrapper — loads SMTP settings from config.php.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/lib/PHPMailer/PHPMailerAutoload.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';

class DocpartMailer extends PHPMailer
{
	public $priority = 3;

	public function __construct($exceptions = false)
	{
		parent::__construct($exceptions);
		$this->applySiteMailConfig();
	}

	/** @deprecated PHP4 alias — kept for legacy includes */
	public function DocpartMailer($exceptions = false)
	{
		$this->__construct($exceptions);
	}

	protected function applySiteMailConfig()
	{
		$DP_Config = new DP_Config();

		if ((int)$DP_Config->smtp_mode === 1 || $DP_Config->smtp_mode === true || $DP_Config->smtp_mode === '1') {
			$encryption = strtolower(trim((string)$DP_Config->smtp_encryption));
			$this->Host = (string)$DP_Config->smtp_host;
			$this->Port = (int)$DP_Config->smtp_port;
			if ($encryption === 'ssl') {
				$this->SMTPSecure = 'ssl';
			} elseif ($encryption === 'tls') {
				$this->SMTPSecure = 'tls';
			}
			if ((string)$DP_Config->smtp_username !== '') {
				$this->SMTPAuth = true;
				$this->Username = (string)$DP_Config->smtp_username;
				$this->Password = (string)$DP_Config->smtp_password;
			}
			$this->Mailer = 'smtp';
		}

		// Always prefer site config over PHPMailer defaults (root@localhost).
		$fromEmail = trim((string) $DP_Config->from_email);
		$fromName = trim((string) $DP_Config->from_name);
		if ($fromEmail !== '') {
			$this->From = $fromEmail;
			$this->Sender = $fromEmail;
		}
		if ($fromName !== '') {
			$this->FromName = $fromName;
		}
		$this->Priority = $this->priority;
	}
}
