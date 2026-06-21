<?php
/**
 * Centered card layout for storefront login & registration pages.
 */
defined('_ASTEXE_') or die('No access');

function epc_storefront_auth_layout_css(): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	echo '<link rel="stylesheet" href="/content/users/epc_storefront_auth.css?v=20260606b" />';
}

function epc_storefront_auth_layout_open(string $variant = 'default'): void
{
	epc_storefront_auth_layout_css();
	$class = 'epc-auth-page';
	if ($variant === 'wide') {
		$class .= ' epc-auth-page--wide';
	}
	echo '<div class="' . htmlspecialchars($class, ENT_QUOTES, 'UTF-8') . '">';
}

function epc_storefront_auth_layout_close(): void
{
	echo '</div>';
}
