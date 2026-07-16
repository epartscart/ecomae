<?php
/**
 * ECOM AE public legal & security policies catalog.
 * Rendered at /legal and /legal/<slug> (plus top-level aliases for key policies).
 */
defined('_ASTEXE_') or die('No access');

/**
 * Effective / last-updated date shown on all legal pages.
 */
function epc_ecomae_legal_effective_date(): string
{
	return '16 July 2026';
}

/**
 * @return array<string, array{
 *   title:string,
 *   summary:string,
 *   icon:string,
 *   sections:list<array{h:string,p?:list<string>,bullets?:list<string>}>
 * }>
 */
function epc_ecomae_legal_catalog(): array
{
	$co = 'Electronic World Group (trading as ECOM AE)';
	$product = 'ECOM AE Blockchain BOS Enterprise System';
	$contact = 'legal@ecomae.com';
	$security = 'security@ecomae.com';

	return array(
		'privacy' => array(
			'title' => 'Privacy Policy',
			'summary' => 'How ECOM AE collects, uses, stores, and protects personal and business data across the platform, Super CP, and tenant workspaces.',
			'icon' => 'fa-user-secret',
			'sections' => array(
				array(
					'h' => '1. Who we are',
					'p' => array(
						$co . ' operates the ' . $product . ' and related websites, demos, Super CP, and tenant storefronts (together, the “Services”). This Privacy Policy explains how we process personal data when you visit ecomae.com, request a demo, create an account, or use a tenant workspace.',
						'Controller contact: ' . $contact . ' · Dubai, United Arab Emirates.',
					),
				),
				array(
					'h' => '2. Data we collect',
					'p' => array('Depending on how you use the Services, we may process:'),
					'bullets' => array(
						'Account and contact details (name, email, phone, company, role).',
						'Billing and commercial information needed for subscriptions and invoices.',
						'Technical data (IP address, device/browser type, logs, approximate location from IP).',
						'Tenant business records you choose to enter (orders, invoices, inventory, CRM, HR, etc.) — processed as a processor for the tenant controller where applicable.',
						'Support communications and demo-request content.',
						'Cookies and similar technologies as described in our Cookie Policy.',
					),
				),
				array(
					'h' => '3. Why we process data',
					'bullets' => array(
						'Provide, host, secure, and improve the Services.',
						'Authenticate users, enforce access control, and prevent abuse.',
						'Respond to demos, sales, and support requests.',
						'Meet legal, tax, and regulatory obligations (including UAE and applicable cross-border rules).',
						'Operate Blockchain BOS proof records where a tenant enables proof mode (hashes and metadata — not full operational databases on-chain).',
					),
				),
				array(
					'h' => '4. Legal bases',
					'p' => array(
						'Where GDPR or similar laws apply, we rely on contract performance, legitimate interests (security, product improvement, B2B communications), legal obligation, and consent where required (for example certain cookies or marketing emails).',
					),
				),
				array(
					'h' => '5. Tenant data & isolation',
					'p' => array(
						'Tenant operational data is hosted in a database-per-tenant model. Platform operators access tenant environments only for authorised support, onboarding, security, or legal compliance purposes under internal controls.',
						'Customers remain responsible for the lawfulness of data they upload and for configuring user access inside their tenant.',
					),
				),
				array(
					'h' => '6. Sharing',
					'p' => array(
						'We do not sell personal data. We may share data with infrastructure and subprocessors (hosting, email, payment, monitoring) under contractual safeguards; with professional advisors; or when required by law or to protect rights, safety, and the integrity of the Services.',
					),
				),
				array(
					'h' => '7. International transfers',
					'p' => array(
						'Data may be processed in the UAE and in other regions where our subprocessors operate. Where required, we use appropriate transfer safeguards.',
					),
				),
				array(
					'h' => '8. Retention',
					'p' => array(
						'We retain data only as long as needed for the purposes above, subscription lifecycle, backup retention windows, dispute resolution, and legal retention duties. Blockchain proof hashes that a tenant has anchored may persist as integrity records even after related operational edits, consistent with the proof model.',
					),
				),
				array(
					'h' => '9. Your rights',
					'p' => array(
						'Subject to applicable law, you may request access, correction, deletion, restriction, portability, or objection. Tenant end-users should contact their organisation first for workspace data. Platform privacy requests: ' . $contact . '.',
					),
				),
				array(
					'h' => '10. Children',
					'p' => array('The Services are designed for business use and are not directed to children under 16.'),
				),
				array(
					'h' => '11. Changes',
					'p' => array('We may update this Policy. Material changes will be posted on this page with an updated effective date.'),
				),
			),
		),

		'terms' => array(
			'title' => 'Terms of Service',
			'summary' => 'Contract terms for accessing and using ECOM AE websites, demos, Super CP, and the Blockchain BOS Enterprise System.',
			'icon' => 'fa-file-text-o',
			'sections' => array(
				array(
					'h' => '1. Agreement',
					'p' => array(
						'By accessing ecomae.com or using the Services, you agree to these Terms of Service, the Acceptable Use Policy, and other policies linked from our Legal hub. If you use the Services on behalf of an organisation, you represent that you have authority to bind that organisation.',
					),
				),
				array(
					'h' => '2. Services',
					'p' => array(
						'ECOM AE provides multi-tenant cloud software for commerce, ERP, compliance, workflows, Super CP fleet operations, and optional Blockchain BOS proof anchoring. Features depend on plan, industry pack, and configuration. Previews, betas, and demos may change without notice.',
					),
				),
				array(
					'h' => '3. Accounts & security',
					'bullets' => array(
						'Keep credentials confidential and use strong authentication where offered.',
						'Notify us promptly of suspected unauthorised access.',
						'You are responsible for activity under your accounts and for tenant user administration.',
					),
				),
				array(
					'h' => '4. Customer content',
					'p' => array(
						'You retain ownership of content you upload. You grant ' . $co . ' a limited licence to host, process, back up, and display that content solely to provide the Services. You represent that you have rights to the content and that it does not violate law or third-party rights.',
					),
				),
				array(
					'h' => '5. Fees',
					'p' => array(
						'Paid plans are billed per commercial agreement or published plan. Fees are exclusive of taxes unless stated. Late or failed payment may result in suspension. Demo and free-tool usage may be rate-limited or revoked for abuse.',
					),
				),
				array(
					'h' => '6. Intellectual property',
					'p' => array(
						'The platform software, branding, documentation, and design are owned by ' . $co . ' or its licensors. No rights are granted except as expressly stated in the Right to Use Policy and your subscription agreement.',
					),
				),
				array(
					'h' => '7. Blockchain proofs',
					'p' => array(
						'When enabled, Blockchain BOS records cryptographic proofs of selected business facts. MySQL remains the operational system of record. Proofs do not replace legal documents, tax filings, or contractual originals unless separately agreed in writing.',
					),
				),
				array(
					'h' => '8. Disclaimers',
					'p' => array(
						'Except as required by law or a signed SLA, the Services are provided “as is”. We do not warrant uninterrupted or error-free operation. You are responsible for validating outputs for your regulatory and commercial use.',
					),
				),
				array(
					'h' => '9. Liability',
					'p' => array(
						'To the maximum extent permitted by applicable UAE and other governing law, ' . $co . ' is not liable for indirect, incidental, special, consequential, or punitive damages, or loss of profits, data, or goodwill. Aggregate liability for claims arising from the Services in any twelve-month period is limited to fees paid for the affected Service in that period (or AED 1,000 if none).',
					),
				),
				array(
					'h' => '10. Suspension & termination',
					'p' => array(
						'We may suspend or terminate access for breach, non-payment, legal risk, or security threat. You may stop using the Services at any time; paid subscriptions end per your agreement. Upon termination, export windows (if any) follow your plan or contract.',
					),
				),
				array(
					'h' => '11. Governing law',
					'p' => array(
						'These Terms are governed by the laws of the United Arab Emirates as applied in the Emirate of Dubai, without regard to conflict-of-law rules. Courts of Dubai have exclusive jurisdiction, subject to mandatory consumer protections where they apply.',
					),
				),
				array(
					'h' => '12. Contact',
					'p' => array('Questions: ' . $contact . '.'),
				),
			),
		),

		'cookie-policy' => array(
			'title' => 'Cookie Policy',
			'summary' => 'How ECOM AE uses cookies and similar technologies on marketing sites and authenticated product surfaces.',
			'icon' => 'fa-hdd-o',
			'sections' => array(
				array(
					'h' => '1. What are cookies',
					'p' => array(
						'Cookies are small text files stored on your device. We also use local storage and similar technologies for sessions, preferences, and security.',
					),
				),
				array(
					'h' => '2. Types we use',
					'bullets' => array(
						'Strictly necessary — login sessions, CSRF protection, load balancing, security.',
						'Functional — language/region preferences and UI state.',
						'Analytics (if enabled) — aggregated traffic and performance to improve the site.',
						'Marketing (only with consent where required) — campaign measurement.',
					),
				),
				array(
					'h' => '3. Control',
					'p' => array(
						'You can block or delete cookies in your browser. Blocking strictly necessary cookies may break login or checkout. Where a consent banner is shown, non-essential cookies wait for your choice.',
					),
				),
				array(
					'h' => '4. More information',
					'p' => array('See also our Privacy Policy. Contact: ' . $contact . '.'),
				),
			),
		),

		'security-policy' => array(
			'title' => 'Security Policy',
			'summary' => 'Security commitments, controls, and responsible disclosure for the ECOM AE Blockchain BOS platform.',
			'icon' => 'fa-shield',
			'sections' => array(
				array(
					'h' => '1. Purpose',
					'p' => array(
						'This Security Policy describes how ' . $co . ' protects the confidentiality, integrity, and availability of the ' . $product . ', customer tenants, and platform operations.',
					),
				),
				array(
					'h' => '2. Core controls',
					'bullets' => array(
						'Tenant isolation via database-per-tenant architecture and access scoping.',
						'TLS encryption in transit for public endpoints; encryption at rest where provided by infrastructure.',
						'Role-based access in Super CP and tenant CP/ERP; least-privilege operator practices.',
						'Authentication protections, session controls, and CSRF guards on state-changing actions.',
						'Backup and recovery processes aligned with business continuity practices.',
						'Logging and monitoring of platform health, anomalies, and privileged actions.',
						'Secure development practices, dependency awareness, and change control for production.',
					),
				),
				array(
					'h' => '3. Customer shared responsibility',
					'bullets' => array(
						'Configure users, roles, and approvals appropriately inside your tenant.',
						'Protect end-user credentials and devices; enable MFA when available.',
						'Validate integrations, API keys, and third-party connectors you enable.',
						'Classify and minimise sensitive data you store; follow your industry regulations.',
					),
				),
				array(
					'h' => '4. Prohibited testing without authorisation',
					'p' => array(
						'Do not perform penetration testing, scanning, or exploitation against production systems without prior written authorisation from ' . $co . '. Unauthorised testing may be treated as an attack and reported.',
					),
				),
				array(
					'h' => '5. Responsible disclosure',
					'p' => array(
						'If you believe you found a vulnerability, email ' . $security . ' with reproduction details. Do not access other tenants’ data, destroy data, or disrupt service. We will acknowledge good-faith reports and work to remediate.',
					),
				),
				array(
					'h' => '6. Incident response',
					'p' => array(
						'We investigate suspected security incidents affecting the platform and, where legally required or contractually agreed, notify affected customers without undue delay with known facts and recommended actions.',
					),
				),
				array(
					'h' => '7. Blockchain proof integrity',
					'p' => array(
						'Blockchain BOS hashing and Merkle anchoring strengthen integrity verification for selected documents. They complement — and do not replace — access control, backups, and operational security.',
					),
				),
			),
		),

		'right-to-use' => array(
			'title' => 'Right to Use Policy',
			'summary' => 'Licence grant and limits for using ECOM AE software, demos, APIs, documentation, and Blockchain BOS components.',
			'icon' => 'fa-key',
			'sections' => array(
				array(
					'h' => '1. Licence grant',
					'p' => array(
						'Subject to a valid subscription or authorised demo and these policies, ' . $co . ' grants you a non-exclusive, non-transferable, non-sublicensable, revocable right to access and use the Services for your internal business operations during the term.',
					),
				),
				array(
					'h' => '2. What you may do',
					'bullets' => array(
						'Use storefront, CP, ERP, Super CP (as entitled), APIs, and Blockchain BOS features included in your plan.',
						'Generate documents, reports, and proofs for legitimate business purposes.',
						'Train your authorised users on the platform.',
					),
				),
				array(
					'h' => '3. What you may not do',
					'bullets' => array(
						'Copy, reverse engineer, decompile, or create derivative works of the platform except to the limited extent mandatory law allows.',
						'Resell, rent, white-label, or provide the Services to third parties as a competing hosted offering without a written partner agreement.',
						'Remove proprietary notices, circumvent licence limits, or share access outside your organisation without authorisation.',
						'Use the Services to build a substantially similar competing product using scraped UX, schema, or documentation.',
						'Extract or republish ECOM AE trademarks, marketing assets, or documentation except as allowed in the Trademark and Copyright policies.',
					),
				),
				array(
					'h' => '4. APIs & free tools',
					'p' => array(
						'API and free-tool usage is subject to rate limits, authentication, and Acceptable Use. Keys are personal to your organisation and must not be published in public repositories.',
					),
				),
				array(
					'h' => '5. Ownership',
					'p' => array(
						'All platform IP remains with ' . $co . '. Customer data remains with the customer. Feedback you provide may be used to improve the Services without obligation to you.',
					),
				),
				array(
					'h' => '6. Termination of rights',
					'p' => array(
						'Rights end when your subscription/demo ends or if you materially breach these terms. Surviving obligations include confidentiality, IP ownership, and liability limits.',
					),
				),
			),
		),

		'trademark' => array(
			'title' => 'Trademark Policy',
			'summary' => 'Rules for using ECOM AE, Blockchain BOS, and related marks, logos, and brand assets.',
			'icon' => 'fa-registered',
			'sections' => array(
				array(
					'h' => '1. Our marks',
					'p' => array(
						'“ECOM AE”, “ecomae”, “Blockchain BOS”, “Blockchain BOS Enterprise System”, “Electronic World Group”, associated logos, product names, and distinctive brand elements (the “Marks”) are trademarks or trade dress of ' . $co . ' or its affiliates.',
					),
				),
				array(
					'h' => '2. Permitted referential use',
					'p' => array(
						'You may use the word Marks in plain text to refer accurately to our products (for example “works with ECOM AE”) provided the use is truthful, not misleading, and does not imply sponsorship or partnership without written approval.',
					),
				),
				array(
					'h' => '3. Prohibited uses',
					'bullets' => array(
						'Using logos or stylised Marks without prior written permission.',
						'Incorporating Marks into your company name, domain, social handle, or app name in a way that causes confusion.',
						'Using Marks in a disparaging, illegal, or deceptive context.',
						'Registering Marks or confusingly similar marks as trademarks, company names, or domains.',
						'Altering Marks or combining them with other logos to create a composite brand.',
					),
				),
				array(
					'h' => '4. Partner & press use',
					'p' => array(
						'Partners and press may request brand assets and written guidelines from ' . $contact . '. Approval may be withdrawn if use becomes inaccurate or damaging.',
					),
				),
				array(
					'h' => '5. Enforcement',
					'p' => array(
						'We actively protect our Marks. Unauthorised use may result in takedown requests, account suspension, and legal action.',
					),
				),
			),
		),

		'copyright' => array(
			'title' => 'Copyright Notice',
			'summary' => 'Copyright ownership for ECOM AE software, documentation, website content, and design assets.',
			'icon' => 'fa-copyright',
			'sections' => array(
				array(
					'h' => '1. Ownership',
					'p' => array(
						'© ' . date('Y') . ' ' . $co . '. All rights reserved. The ' . $product . ', websites, UI, graphics, documentation, training materials, and code (except third-party open-source components under their licences) are protected by copyright and related laws.',
					),
				),
				array(
					'h' => '2. Limited permission',
					'p' => array(
						'You may view and print pages from ecomae.com for internal evaluation. You may not republish, scrape at scale, mirror, or commercially redistribute our content without written consent, except brief quotations with attribution for commentary or review.',
					),
				),
				array(
					'h' => '3. Third-party materials',
					'p' => array(
						'Some libraries, fonts, or images may be licensed from third parties. Those materials remain subject to their own licences.',
					),
				),
				array(
					'h' => '4. Infringement notices',
					'p' => array(
						'If you believe content on our Services infringes your copyright, send a notice to ' . $contact . ' with: your contact details, description of the work, URL of the allegedly infringing material, a good-faith statement, and a statement under penalty of perjury that you are authorised to act. We will review and act as appropriate.',
					),
				),
			),
		),

		'data-protection' => array(
			'title' => 'Data Protection & Anti-Theft Policy',
			'summary' => 'How we protect customer data and prohibit theft, exfiltration, and unauthorised use of ECOM AE and tenant information.',
			'icon' => 'fa-lock',
			'sections' => array(
				array(
					'h' => '1. Commitment',
					'p' => array(
						$co . ' treats customer and platform data as a critical asset. This Policy sets expectations against data theft, unauthorised exfiltration, credential abuse, and misuse of Blockchain BOS or ERP exports.',
					),
				),
				array(
					'h' => '2. Protected data classes',
					'bullets' => array(
						'Tenant databases, backups, and configuration.',
						'Personal data of employees, customers, and suppliers.',
						'Commercial secrets, pricing files, supplier terms, and financial records.',
						'API keys, tokens, certificates, and operator credentials.',
						'Source code, schemas, internal runbooks, and non-public documentation.',
						'Blockchain proof private operational metadata beyond public verify fields.',
					),
				),
				array(
					'h' => '3. Strictly prohibited conduct',
					'bullets' => array(
						'Accessing another tenant’s data or Super CP resources without authorisation.',
						'Scraping, bulk-exporting, or downloading data beyond your legitimate role.',
						'Selling, leaking, or publishing confidential platform or customer data.',
						'Using stolen credentials, sharing accounts to evade controls, or social-engineering staff.',
						'Deploying malware, keyloggers, or unauthorised interception tools against the Services.',
						'Circumventing encryption, tenancy boundaries, or audit logging.',
					),
				),
				array(
					'h' => '4. Detection & response',
					'p' => array(
						'We may monitor for anomalous access, suspend accounts, revoke keys, preserve forensic evidence, and cooperate with law enforcement. Customers must promptly report suspected theft to ' . $security . '.',
					),
				),
				array(
					'h' => '5. Customer obligations',
					'p' => array(
						'Customers must apply least-privilege access, revoke leavers promptly, protect exports, and not instruct the platform to process stolen third-party datasets. Breach of this Policy is grounds for immediate suspension and legal remedies.',
					),
				),
				array(
					'h' => '6. Related policies',
					'p' => array(
						'Read together with the Security Policy, Privacy Policy, Acceptable Use Policy, and Confidentiality Policy.',
					),
				),
			),
		),

		'acceptable-use' => array(
			'title' => 'Acceptable Use Policy',
			'summary' => 'Rules of conduct for websites, demos, APIs, Super CP, tenant workspaces, and Blockchain BOS features.',
			'icon' => 'fa-check-square-o',
			'sections' => array(
				array(
					'h' => '1. Scope',
					'p' => array('This Acceptable Use Policy (AUP) applies to all users of ECOM AE Services.'),
				),
				array(
					'h' => '2. You must not',
					'bullets' => array(
						'Violate law, including fraud, money laundering, sanctions evasion, or IP infringement.',
						'Host or transmit malware, phishing, spam, or deceptive content.',
						'Attack, scan, or overload infrastructure without written authorisation.',
						'Interfere with other tenants or shared platform components.',
						'Misrepresent identity, forge documents, or abuse e-invoicing / tax features.',
						'Use Blockchain proofs to make false authenticity claims about documents you altered off-platform.',
						'Mine cryptocurrency or run unrelated high-load workloads on our hosts.',
						'Collect personal data unlawfully through forms, storefronts, or APIs.',
					),
				),
				array(
					'h' => '3. Enforcement',
					'p' => array(
						'Violations may result in content removal, feature limits, suspension, termination, and reporting to authorities. We may act without prior notice when necessary to protect the platform or others.',
					),
				),
				array(
					'h' => '4. Reporting abuse',
					'p' => array('Report abuse to ' . $security . ' or ' . $contact . '.'),
				),
			),
		),

		'confidentiality' => array(
			'title' => 'Confidentiality Policy',
			'summary' => 'Handling of confidential information exchanged during demos, onboarding, support, and partnerships.',
			'icon' => 'fa-handshake-o',
			'sections' => array(
				array(
					'h' => '1. Definition',
					'p' => array(
						'“Confidential Information” means non-public business, technical, or financial information disclosed by either party, including product roadmaps, pricing not public, tenant configurations, credentials, and unpublished documentation.',
					),
				),
				array(
					'h' => '2. Obligations',
					'bullets' => array(
						'Use Confidential Information only for evaluating or delivering the Services.',
						'Limit access to personnel with a need to know under confidentiality duties.',
						'Protect it with reasonable care no less than used for your own similar information.',
						'Do not publish or reverse-engineer disclosed materials.',
					),
				),
				array(
					'h' => '3. Exceptions',
					'p' => array(
						'Obligations do not apply to information that is public without breach, independently developed, rightfully received from a third party without duty, or required to be disclosed by law (with notice where legally permitted).',
					),
				),
				array(
					'h' => '4. Duration',
					'p' => array(
						'Confidentiality continues during discussions and for three (3) years after disclosure, except for trade secrets which remain protected while they qualify as such, and for personal data which follows the Privacy Policy.',
					),
				),
			),
		),

		'intellectual-property' => array(
			'title' => 'Intellectual Property Policy',
			'summary' => 'How ECOM AE protects patents, copyrights, trade secrets, and customer IP on the platform.',
			'icon' => 'fa-lightbulb-o',
			'sections' => array(
				array(
					'h' => '1. Platform IP',
					'p' => array(
						'Software architecture, Blockchain BOS proof methods as implemented in our Services, UX, documentation, and related IP belong to ' . $co . '. Open-source components remain under their licences.',
					),
				),
				array(
					'h' => '2. Customer IP',
					'p' => array(
						'Customers retain IP in their catalogues, brands, and business data. We claim no ownership of customer content. Licence to host is described in Terms of Service.',
					),
				),
				array(
					'h' => '3. Restrictions',
					'p' => array(
						'Copying our schemas, workflows, or distinctive UX to create a competing product; scraping docs for model training at scale without permission; or removing licence checks is prohibited.',
					),
				),
				array(
					'h' => '4. Claims',
					'p' => array(
						'IP complaints and licence questions: ' . $contact . '.',
					),
				),
			),
		),

		'blockchain-disclaimer' => array(
			'title' => 'Blockchain Proof Disclaimer',
			'summary' => 'Important limits of Blockchain BOS anchoring, verification, and public proof URLs.',
			'icon' => 'fa-link',
			'sections' => array(
				array(
					'h' => '1. What proofs are',
					'p' => array(
						'Blockchain BOS creates cryptographic hashes of selected business facts and may Merkle-anchor batches for later verification. Public verify pages confirm that a proof matches an anchored root when status is anchored.',
					),
				),
				array(
					'h' => '2. What proofs are not',
					'bullets' => array(
						'Not a replacement for signed contracts, notarisation, or statutory filings unless your counsel advises otherwise.',
						'Not a public ledger of full invoices, stock, or personal data.',
						'Not a guarantee that off-platform copies were never altered before hashing.',
						'Not permissioned multi-party consensus networking unless a future “network” mode is commercially enabled.',
					),
				),
				array(
					'h' => '3. Operational truth',
					'p' => array(
						'MySQL tenant databases remain the operational system of record. Disable or misconfigure proof mode and business transactions still proceed; proof hooks are best-effort and must not be the sole control for compliance.',
					),
				),
				array(
					'h' => '4. Public verify links',
					'p' => array(
						'Anyone with a proof UID may open the verify URL. Do not put secrets in document fields you choose to hash. Tenants should treat proof UIDs as integrity references, not authentication secrets.',
					),
				),
			),
		),

		'dmca' => array(
			'title' => 'IP Infringement & Notice Policy',
			'summary' => 'How to report copyright, trademark, or other IP infringement involving ECOM AE or tenant content we host.',
			'icon' => 'fa-gavel',
			'sections' => array(
				array(
					'h' => '1. Reporting',
					'p' => array(
						'Send infringement notices to ' . $contact . ' with sufficient detail for us to locate the material and assess the claim (URLs, description of rights, contact information, and a statement of good faith).',
					),
				),
				array(
					'h' => '2. Tenant content',
					'p' => array(
						'Tenant storefronts and uploads are controlled by customers. We may relay notices to the tenant, require removal, or suspend content/accounts that clearly infringe or pose legal risk.',
					),
				),
				array(
					'h' => '3. Counter-notice',
					'p' => array(
						'If your content was removed and you believe the removal was mistaken, email a counter-notice with your contact details, identification of the material, and a statement that you have a good-faith belief the material was removed by mistake. We may restore or maintain removal based on risk and law.',
					),
				),
				array(
					'h' => '4. Repeat infringers',
					'p' => array(
						'We may terminate access for repeat infringers in appropriate circumstances.',
					),
				),
			),
		),
	);
}

/**
 * Top-level URL aliases → legal catalog slug (besides /legal/<slug>).
 *
 * @return array<string,string> path => slug
 */
function epc_ecomae_legal_top_level_aliases(): array
{
	return array(
		'/privacy' => 'privacy',
		'/terms' => 'terms',
		'/cookie-policy' => 'cookie-policy',
		'/security-policy' => 'security-policy',
		'/right-to-use' => 'right-to-use',
		'/trademark' => 'trademark',
		'/copyright' => 'copyright',
		'/data-protection' => 'data-protection',
		'/acceptable-use' => 'acceptable-use',
		'/confidentiality' => 'confidentiality',
		'/intellectual-property' => 'intellectual-property',
		'/blockchain-disclaimer' => 'blockchain-disclaimer',
		'/dmca' => 'dmca',
		// OAuth / legacy expectations
		'/en/privacy' => 'privacy',
		'/en/terms' => 'terms',
	);
}
