<?php
/**
 * Marketing Broadcast — brochure templates (email HTML + WhatsApp text).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<string, array<string, mixed>>
 */
function epc_mb_email_templates(): array
{
	return array(
		'promo_sale' => array(
			'label' => 'Seasonal sale brochure',
			'subject' => '{{shop_name}} — Limited-time offers inside',
			'preview' => 'Exclusive deals on parts & accessories — open to see your savings.',
			'html' => '<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="font-family:Arial,sans-serif;background:#f8fafc;padding:24px">'
				. '<div style="max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;border:1px solid #e2e8f0">'
				. '<div style="background:linear-gradient(135deg,#2563eb,#7c3aed);color:#fff;padding:28px 24px">'
				. '<h1 style="margin:0;font-size:24px">{{shop_name}}</h1>'
				. '<p style="margin:8px 0 0;opacity:.9">Seasonal sale — limited stock</p></div>'
				. '<div style="padding:24px;color:#334155;line-height:1.6">'
				. '<p>Hello {{customer_name}},</p>'
				. '<p>We selected top offers for you this week. Browse our catalogue and use code <strong>SALE10</strong> at checkout.</p>'
				. '<p style="text-align:center;margin:28px 0"><a href="{{shop_url}}" style="background:#2563eb;color:#fff;padding:12px 28px;border-radius:8px;text-decoration:none;font-weight:700">Shop now</a></p>'
				. '<p>Questions? Reply to this email or WhatsApp us.</p>'
				. '<p>— {{shop_name}} team</p></div>'
				. '<div style="background:#f1f5f9;padding:14px 24px;font-size:11px;color:#64748b">You received this because you are a registered customer. Unsubscribe by contacting us.</div>'
				. '</div></body></html>',
		),
		'new_arrivals' => array(
			'label' => 'New arrivals brochure',
			'subject' => '{{shop_name}} — New stock just landed',
			'preview' => 'Fresh inventory — see what\'s new in our warehouse.',
			'html' => '<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="font-family:Arial,sans-serif;background:#f0fdf4;padding:24px">'
				. '<div style="max-width:600px;margin:0 auto;background:#fff;border-radius:12px;border:1px solid #bbf7d0;padding:28px">'
				. '<h2 style="color:#166534;margin:0 0 12px">{{shop_name}} — New arrivals</h2>'
				. '<p style="color:#334155">Hi {{customer_name}},</p>'
				. '<p style="color:#334155">New parts and accessories are now in stock. Visit our shop to see the latest additions.</p>'
				. '<p><a href="{{shop_url}}" style="color:#059669;font-weight:700">Browse new arrivals →</a></p>'
				. '</div></body></html>',
		),
		'service_reminder' => array(
			'label' => 'Service reminder',
			'subject' => '{{shop_name}} — Time for your next service?',
			'preview' => 'Keep your vehicle running smoothly — book parts or service today.',
			'html' => '<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="font-family:Arial,sans-serif;background:#fff7ed;padding:24px">'
				. '<div style="max-width:600px;margin:0 auto;background:#fff;border-radius:12px;border:1px solid #fed7aa;padding:28px">'
				. '<h2 style="color:#c2410c;margin:0 0 12px">Service reminder</h2>'
				. '<p>Dear {{customer_name}},</p>'
				. '<p>Regular maintenance keeps your vehicle safe. {{shop_name}} has filters, oils, and wear parts ready to ship.</p>'
				. '<p><a href="{{shop_url}}">Order online</a> or call our team.</p></div></body></html>',
		),
		'blank' => array(
			'label' => 'Blank HTML brochure',
			'subject' => '{{shop_name}} — Message for you',
			'preview' => 'A message from {{shop_name}}.',
			'html' => '<!DOCTYPE html><html><head><meta charset="utf-8"></head><body style="font-family:Arial,sans-serif;padding:24px">'
				. '<div style="max-width:600px;margin:0 auto"><h2>{{shop_name}}</h2>'
				. '<p>Hello {{customer_name}},</p><p>Your message here…</p></div></body></html>',
		),
	);
}

/**
 * @return array<string, array<string, mixed>>
 */
function epc_mb_whatsapp_templates(): array
{
	return array(
		'promo_bilingual' => array(
			'label' => 'Promo offer (EN + AR)',
			'body' => "Hello {{customer_name}}! 🎉\n\n{{shop_name}} has special offers this week.\nVisit: {{shop_url}}\n\n"
				. "مرحباً {{customer_name}}! عروض خاصة من {{shop_name}} هذا الأسبوع.\n{{shop_url}}",
		),
		'brochure_share' => array(
			'label' => 'Brochure / catalogue share',
			'body' => "Hi {{customer_name}},\n\nHere is our latest brochure from {{shop_name}}.\nBrowse: {{shop_url}}\n\n"
				. "مرحباً، إليك أحدث كتالوج من {{shop_name}}.\n{{shop_url}}",
		),
		'follow_up' => array(
			'label' => 'Order follow-up',
			'body' => "Hello {{customer_name}},\n\nThank you for shopping with {{shop_name}}. Need anything else? Reply here or visit {{shop_url}}.\n\n"
				. "شكراً لتسوقكم مع {{shop_name}}. للمساعدة ردّوا على هذه الرسالة.",
		),
		'event_invite' => array(
			'label' => 'Event / open day invitation',
			'body' => "You're invited! {{shop_name}} open day — visit us or shop online: {{shop_url}}\n\n"
				. "دعوة من {{shop_name}} — زورونا أو تسوقوا أونلاين: {{shop_url}}",
		),
		'blank' => array(
			'label' => 'Blank WhatsApp message',
			'body' => "Hello {{customer_name}},\n\nMessage from {{shop_name}}.\n{{shop_url}}",
		),
	);
}

function epc_mb_apply_template_vars(string $text, array $vars): string
{
	foreach ($vars as $key => $val) {
		$text = str_replace('{{' . $key . '}}', (string) $val, $text);
	}
	return $text;
}
