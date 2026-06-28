<?php
/**
 * World-class storefront enhancements shared across all tenant storefronts.
 * Provides: JSON-LD structured data, newsletter signup, trust badges, cookie consent.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

function epc_storefront_json_ld_organization(): string
{
	$site = epc_portal_site_profile();
	$settings = epc_portal_load_site_settings();
	$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
	$name = !empty($contact['company_name']) ? (string) $contact['company_name'] : (!empty($site['system_name']) ? (string) $site['system_name'] : 'Business');
	$domain = !empty($site['domain_path']) ? rtrim((string) $site['domain_path'], '/') : '';
	$phone = !empty($contact['phone']) ? (string) $contact['phone'] : '';
	$email = !empty($contact['email']) ? (string) $contact['email'] : '';

	$org = array(
		'@context' => 'https://schema.org',
		'@type' => 'Organization',
		'name' => $name,
		'url' => $domain,
	);
	if ($domain !== '') {
		$org['logo'] = $domain . '/favicon.svg';
	}
	if ($phone !== '') {
		$org['telephone'] = $phone;
	}
	if ($email !== '') {
		$org['email'] = $email;
	}
	$address = array();
	if (!empty($contact['address_line1'])) {
		$address['streetAddress'] = (string) $contact['address_line1'];
	}
	if (!empty($contact['city'])) {
		$address['addressLocality'] = (string) $contact['city'];
	}
	if (!empty($contact['country'])) {
		$address['addressCountry'] = (string) $contact['country'];
	}
	if ($address) {
		$address['@type'] = 'PostalAddress';
		$org['address'] = $address;
	}
	$social = epc_storefront_social_links_data();
	if ($social) {
		$org['sameAs'] = array_values(array_map(function ($s) { return $s['url']; }, $social));
	}
	return '<script type="application/ld+json">' . json_encode($org, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . '</script>';
}

function epc_storefront_json_ld_website(): string
{
	$site = epc_portal_site_profile();
	$domain = !empty($site['domain_path']) ? rtrim((string) $site['domain_path'], '/') : '';
	$name = !empty($site['system_name']) ? (string) $site['system_name'] : 'Store';

	$ws = array(
		'@context' => 'https://schema.org',
		'@type' => 'WebSite',
		'name' => $name,
		'url' => $domain,
		'potentialAction' => array(
			'@type' => 'SearchAction',
			'target' => $domain . '/en/shop/search?search_string={search_term_string}',
			'query-input' => 'required name=search_term_string',
		),
	);
	return '<script type="application/ld+json">' . json_encode($ws, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . '</script>';
}

function epc_storefront_social_links_data(): array
{
	$settings = epc_portal_load_site_settings();
	$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
	$links = array();
	$platforms = array(
		'facebook' => array('icon' => 'fa-facebook', 'label' => 'Facebook'),
		'instagram' => array('icon' => 'fa-instagram', 'label' => 'Instagram'),
		'twitter' => array('icon' => 'fa-twitter', 'label' => 'Twitter'),
		'linkedin' => array('icon' => 'fa-linkedin', 'label' => 'LinkedIn'),
		'youtube' => array('icon' => 'fa-youtube-play', 'label' => 'YouTube'),
		'tiktok' => array('icon' => 'fa-music', 'label' => 'TikTok'),
	);
	foreach ($platforms as $key => $meta) {
		$url = '';
		if (!empty($contact['social_' . $key])) {
			$url = (string) $contact['social_' . $key];
		} elseif (!empty($contact[$key . '_url'])) {
			$url = (string) $contact[$key . '_url'];
		}
		if ($url !== '') {
			$links[] = array('platform' => $key, 'url' => $url, 'icon' => $meta['icon'], 'label' => $meta['label']);
		}
	}
	return $links;
}

function epc_storefront_newsletter_section(string $accentColor = '#0ea5e9', string $bgColor = '#f8fafc', string $industry = ''): string
{
	$site = epc_portal_site_profile();
	$domain = !empty($site['domain_path']) ? rtrim((string) $site['domain_path'], '/') : '';
	$headlines = array(
		'electronics' => array('Stay plugged in', 'Get the latest tech deals, launches and exclusive offers delivered to your inbox.'),
		'fashion' => array('Be the first to know', 'New arrivals, style tips, and exclusive offers — straight to your inbox.'),
		'jewellery' => array('Join our inner circle', 'Exclusive previews, new collections, and special offers for our members.'),
		'tax_advisory' => array('Stay compliant', 'Tax updates, regulatory changes, and advisory insights delivered monthly.'),
		'consultancy' => array('Stay compliant', 'Tax updates, regulatory changes, and advisory insights delivered monthly.'),
	);
	$h = isset($headlines[$industry]) ? $headlines[$industry] : array('Stay updated', 'Subscribe for the latest updates and exclusive offers.');
	$id = 'epc_wc_newsletter_' . preg_replace('/[^a-z0-9]/', '', $industry);

	$html = '<section class="epc-wc-newsletter" id="' . $id . '" style="background:' . htmlspecialchars($bgColor) . ';padding:48px 0;text-align:center;">';
	$html .= '<div class="container">';
	$html .= '<h2 style="font-size:24px;font-weight:700;margin:0 0 8px;color:#1e293b;">' . htmlspecialchars($h[0]) . '</h2>';
	$html .= '<p style="color:#64748b;margin:0 0 20px;font-size:15px;">' . htmlspecialchars($h[1]) . '</p>';
	$html .= '<form class="epc-wc-newsletter__form" style="max-width:480px;margin:0 auto;display:flex;gap:8px;" onsubmit="return epcWcNewsletterSubmit(this)">';
	$html .= '<input type="email" name="email" required placeholder="Enter your email" style="flex:1;padding:12px 16px;border:1px solid #cbd5e1;border-radius:6px;font-size:15px;outline:none;" />';
	$html .= '<button type="submit" style="padding:12px 24px;background:' . htmlspecialchars($accentColor) . ';color:#fff;border:none;border-radius:6px;font-weight:600;font-size:15px;cursor:pointer;white-space:nowrap;">Subscribe</button>';
	$html .= '</form>';
	$html .= '<p class="epc-wc-newsletter__note" style="color:#94a3b8;font-size:12px;margin:12px 0 0;">No spam. Unsubscribe anytime.</p>';
	$html .= '</div></section>';
	return $html;
}

function epc_storefront_trust_badges(string $industry = ''): string
{
	$badges = array(
		array('icon' => 'fa-shield', 'text' => 'Secure Checkout'),
		array('icon' => 'fa-truck', 'text' => 'Fast Delivery'),
		array('icon' => 'fa-undo', 'text' => 'Easy Returns'),
		array('icon' => 'fa-certificate', 'text' => 'UAE Registered'),
	);
	if ($industry === 'jewellery') {
		$badges = array(
			array('icon' => 'fa-gem', 'text' => 'Certified Authentic'),
			array('icon' => 'fa-shield', 'text' => 'Secure Payment'),
			array('icon' => 'fa-gift', 'text' => 'Gift Wrapping'),
			array('icon' => 'fa-certificate', 'text' => 'Hallmarked Gold'),
		);
	} elseif ($industry === 'electronics') {
		$badges = array(
			array('icon' => 'fa-shield', 'text' => 'Genuine Products'),
			array('icon' => 'fa-truck', 'text' => 'Same-Day Delivery'),
			array('icon' => 'fa-refresh', 'text' => '14-Day Returns'),
			array('icon' => 'fa-lock', 'text' => 'Secure Checkout'),
		);
	} elseif ($industry === 'fashion') {
		$badges = array(
			array('icon' => 'fa-check-circle', 'text' => '100% Authentic'),
			array('icon' => 'fa-truck', 'text' => 'Free Shipping 200+'),
			array('icon' => 'fa-undo', 'text' => '30-Day Returns'),
			array('icon' => 'fa-lock', 'text' => 'Secure Checkout'),
		);
	} elseif (in_array($industry, array('tax_advisory', 'consultancy'), true)) {
		$badges = array(
			array('icon' => 'fa-university', 'text' => 'FTA Registered'),
			array('icon' => 'fa-shield', 'text' => 'Data Protected'),
			array('icon' => 'fa-users', 'text' => 'Expert Team'),
			array('icon' => 'fa-certificate', 'text' => 'Licensed Practice'),
		);
	}

	$html = '<div class="epc-wc-trust" style="padding:24px 0;background:#fff;border-top:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;">';
	$html .= '<div class="container"><div style="display:flex;justify-content:center;flex-wrap:wrap;gap:32px;">';
	foreach ($badges as $b) {
		$html .= '<div style="display:flex;align-items:center;gap:8px;color:#475569;font-size:14px;font-weight:500;">';
		$html .= '<i class="fa ' . htmlspecialchars($b['icon']) . '" style="font-size:20px;color:#0ea5e9;"></i>';
		$html .= '<span>' . htmlspecialchars($b['text']) . '</span>';
		$html .= '</div>';
	}
	$html .= '</div></div></div>';
	return $html;
}

function epc_storefront_cookie_consent(): string
{
	$html = '<div id="epc_wc_cookie" style="display:none;position:fixed;bottom:0;left:0;right:0;background:#1e293b;color:#e2e8f0;padding:14px 20px;z-index:99999;font-size:14px;">';
	$html .= '<div class="container" style="display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;">';
	$html .= '<p style="margin:0;flex:1;min-width:200px;">We use cookies to enhance your experience. By continuing to browse, you agree to our use of cookies.</p>';
	$html .= '<div style="display:flex;gap:8px;">';
	$html .= '<button onclick="epcWcCookieAccept()" style="padding:8px 20px;background:#0ea5e9;color:#fff;border:none;border-radius:4px;font-weight:600;cursor:pointer;">Accept</button>';
	$html .= '<button onclick="epcWcCookieDecline()" style="padding:8px 20px;background:transparent;color:#94a3b8;border:1px solid #475569;border-radius:4px;cursor:pointer;">Decline</button>';
	$html .= '</div></div></div>';
	$html .= '<script>';
	$html .= '(function(){var c=document.getElementById("epc_wc_cookie");if(!c)return;if(!document.cookie.match(/(?:^|; )epc_cookie_consent=/)){c.style.display="block";}})();';
	$html .= 'function epcWcCookieAccept(){document.cookie="epc_cookie_consent=accepted;max-age="+(365*86400)+";path=/;SameSite=Lax";var c=document.getElementById("epc_wc_cookie");if(c)c.style.display="none";}';
	$html .= 'function epcWcCookieDecline(){document.cookie="epc_cookie_consent=declined;max-age="+(365*86400)+";path=/;SameSite=Lax";var c=document.getElementById("epc_wc_cookie");if(c)c.style.display="none";}';
	$html .= '</script>';
	return $html;
}

function epc_storefront_newsletter_js(): string
{
	return '<script>
function epcWcNewsletterSubmit(form){
	var email=form.querySelector("input[name=email]");
	if(!email||!email.value)return false;
	var btn=form.querySelector("button[type=submit]");
	if(btn){btn.textContent="Subscribing...";btn.disabled=true;}
	var xhr=new XMLHttpRequest();
	xhr.open("POST","/ajax_newsletter_subscribe.php",true);
	xhr.setRequestHeader("Content-Type","application/x-www-form-urlencoded");
	xhr.onload=function(){
		if(btn){btn.textContent="Subscribed!";btn.style.background="#22c55e";}
		email.value="";email.placeholder="Thank you!";
		setTimeout(function(){if(btn){btn.textContent="Subscribe";btn.disabled=false;btn.style.background="";}email.placeholder="Enter your email";},4000);
	};
	xhr.onerror=function(){if(btn){btn.textContent="Subscribe";btn.disabled=false;}};
	xhr.send("email="+encodeURIComponent(email.value)+"&action=subscribe");
	return false;
}
</script>';
}
