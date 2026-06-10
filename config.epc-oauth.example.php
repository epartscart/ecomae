<?php
/**
 * Google OAuth for CP modern auth — copy to config.epc-oauth.php (not in git).
 *
 * Redirect URI must match Google Cloud Console:
 *   https://www.ecomae.com/epc-auth-google-callback.php
 */
return array(
	'google' => array(
		'client_id' => 'YOUR_CLIENT_ID.apps.googleusercontent.com',
		'client_secret' => 'YOUR_CLIENT_SECRET',
		'redirect_uri' => 'https://www.ecomae.com/epc-auth-google-callback.php',
	),
);
