<?php
/**
 * ECOM AE marketing FAQ page — render, search, FAQPage schema.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_faq_data.php';

function epc_ecomae_faq_format_answer(string $answer): string
{
	$base = epc_ecomae_platform_base_url();
	$html = epc_ecomae_h($answer);
	$replacements = array(
		'Catalog API' => $base . 'platform/api-services',
		'Auto Price AI page' => $base . 'platform/auto-price-ai',
		'Auto Price AI' => $base . 'platform/auto-price-ai',
		'demo page' => $base . 'platform/demo',
		'pricing page' => $base . 'platform/pricing',
		'business continuity page' => $base . 'platform/business-continuity',
		'platform overview' => $base . 'platform',
		'vehicle catalogue capability pages' => $base . 'platform/capabilities?highlight=vehicle-catalogue',
		'vehicle catalogue capability page' => $base . 'platform/capabilities?highlight=vehicle-catalogue',
	);
	uksort($replacements, static function ($a, $b) {
		return strlen($b) - strlen($a);
	});
	foreach ($replacements as $label => $href) {
		$link = '<a href="' . epc_ecomae_h($href) . '">' . epc_ecomae_h($label) . '</a>';
		$html = str_replace(epc_ecomae_h($label), $link, $html);
	}
	return $html;
}

function epc_ecomae_faq_status_class(string $status): string
{
	$map = array(
		'Yes' => 'epm-faq__status--yes',
		'Partial' => 'epm-faq__status--partial',
		'Planned' => 'epm-faq__status--planned',
		'No' => 'epm-faq__status--no',
	);
	return isset($map[$status]) ? $map[$status] : 'epm-faq__status--partial';
}

function epc_ecomae_faq_schema_json(): string
{
	$entities = array();
	foreach (epc_ecomae_faq_modules() as $mod) {
		foreach ($mod['items'] as $item) {
			$entities[] = array(
				'@type' => 'Question',
				'name' => (string) $item['q'],
				'acceptedAnswer' => array(
					'@type' => 'Answer',
					'text' => strip_tags(epc_ecomae_faq_format_answer((string) $item['a'])),
				),
			);
		}
	}
	return json_encode(array(
		'@context' => 'https://schema.org',
		'@type' => 'FAQPage',
		'mainEntity' => $entities,
	), JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
}

function epc_ecomae_faq_styles(): string
{
	return <<<'CSS'
<style>
.epm-faq-hero{padding:48px 24px 32px;text-align:center}
.epm-faq-search{max-width:640px;margin:24px auto 0;position:relative}
.epm-faq-search input{width:100%;padding:14px 48px 14px 18px;border-radius:12px;border:1px solid rgba(14,165,233,.35);background:rgba(10,10,10,.75);color:#e2e8f0;font-size:16px}
.epm-faq-search input:focus{outline:none;border-color:#0284c7;box-shadow:0 0 0 3px rgba(14,165,233,.2)}
.epm-faq-search i{position:absolute;right:18px;top:50%;transform:translateY(-50%);color:#64748b}
.epm-faq-stats{display:flex;flex-wrap:wrap;gap:10px;justify-content:center;margin:20px 0 8px}
.epm-faq-stat{font-size:13px;padding:6px 12px;border-radius:999px;background:rgba(23,23,23,.8);border:1px solid rgba(148,163,184,.2);color:#94a3b8}
.epm-faq-stat strong{color:#e2e8f0}
.epm-faq-tabs{display:flex;flex-wrap:wrap;gap:8px;justify-content:center;padding:0 16px 24px;border-bottom:1px solid rgba(148,163,184,.15)}
.epm-faq-tab{padding:10px 16px;border-radius:10px;border:1px solid rgba(14,165,233,.25);background:rgba(10,10,10,.5);color:#94a3b8;font-size:13px;cursor:pointer;transition:.15s}
.epm-faq-tab:hover,.epm-faq-tab.is-active{background:linear-gradient(135deg,#5a0f16,#8a131c);color:#fff;border-color:transparent}
.epm-faq-tab i{margin-right:6px}
.epm-faq-body{max-width:920px;margin:0 auto;padding:24px 16px 48px}
.epm-faq-module{display:none}
.epm-faq-module.is-active{display:block}
.epm-faq-module__head{margin-bottom:20px}
.epm-faq-module__head h2{font-size:1.35rem;margin:0 0 6px;color:#f1f5f9}
.epm-faq-module__head p{color:#94a3b8;margin:0;font-size:14px}
.epm-faq-item{border:1px solid rgba(148,163,184,.15);border-radius:12px;margin-bottom:10px;background:rgba(23,23,23,.55);overflow:hidden}
.epm-faq-item.is-hidden{display:none}
.epm-faq-item__q{width:100%;text-align:left;padding:16px 18px;background:transparent;border:0;color:#e2e8f0;font-size:15px;font-weight:600;cursor:pointer;display:flex;align-items:flex-start;gap:12px;line-height:1.45}
.epm-faq-item__q:hover{background:rgba(14,165,233,.06)}
.epm-faq-item__num{flex:0 0 auto;font-size:12px;color:#64748b;min-width:28px;padding-top:2px}
.epm-faq-item__chev{margin-left:auto;flex:0 0 auto;color:#64748b;transition:transform .2s}
.epm-faq-item.is-open .epm-faq-item__chev{transform:rotate(180deg)}
.epm-faq-item__a{display:none;padding:0 18px 18px 58px;color:#cbd5e1;font-size:14px;line-height:1.65}
.epm-faq-item.is-open .epm-faq-item__a{display:block}
.epm-faq-item__a a{color:#0284c7;text-decoration:underline}
.epm-faq__status{display:inline-block;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.04em;padding:3px 8px;border-radius:6px;margin-bottom:8px}
.epm-faq__status--yes{background:rgba(34,197,94,.15);color:#4ade80}
.epm-faq__status--partial{background:rgba(251,191,36,.12);color:#fbbf24}
.epm-faq__status--planned{background:rgba(14,165,233,.12);color:#0284c7}
.epm-faq__status--no{background:rgba(248,113,113,.12);color:#f87171}
.epm-faq-empty{text-align:center;padding:32px;color:#64748b;display:none}
.epm-faq-empty.is-visible{display:block}
.epm-faq-cta{text-align:center;padding:32px 16px 48px;border-top:1px solid rgba(148,163,184,.12)}
@media(max-width:640px){.epm-faq-item__a{padding-left:18px}.epm-faq-tabs{justify-content:flex-start;overflow-x:auto;flex-wrap:nowrap;padding-bottom:16px}}
</style>
CSS;
}

function epc_ecomae_faq_render_page(): string
{
	$modules = epc_ecomae_faq_modules();
	$counts = epc_ecomae_faq_status_counts();
	$total = array_sum($counts);
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	ob_start();
	echo epc_ecomae_faq_styles();
	?>
<script type="application/ld+json"><?php echo epc_ecomae_faq_schema_json(); ?></script>
<div class="epm-wrap">
	<div class="epm-hero epm-faq-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407 0%,#5a0f16 42%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-question-circle"></i> <?php echo (int) $total; ?> answers</div>
			<h1>Frequently Asked Questions</h1>
			<p class="lead">Honest answers from the ECOM AE platform — automotive catalog, B2B, supply chain, UAE ERP, AI, infrastructure, and licensing. Status badges show what is live today vs roadmap.</p>
			<div class="epm-faq-search">
				<input type="search" id="epm_faq_search" placeholder="Search questions and answers…" autocomplete="off" aria-label="Search FAQ" />
				<i class="fa fa-search"></i>
			</div>
			<div class="epm-faq-stats" aria-label="Answer status summary">
				<span class="epm-faq-stat"><strong><?php echo (int) $counts['Yes']; ?></strong> Yes</span>
				<span class="epm-faq-stat"><strong><?php echo (int) $counts['Partial']; ?></strong> Partial</span>
				<span class="epm-faq-stat"><strong><?php echo (int) $counts['Planned']; ?></strong> Planned</span>
				<span class="epm-faq-stat"><strong><?php echo (int) $counts['No']; ?></strong> No / contact us</span>
			</div>
		</div>
	</div>

	<nav class="epm-faq-tabs" id="epm_faq_tabs" aria-label="FAQ modules">
		<?php foreach ($modules as $i => $mod) { ?>
		<button type="button" class="epm-faq-tab<?php echo $i === 0 ? ' is-active' : ''; ?>" data-module="<?php echo epc_ecomae_h($mod['id']); ?>">
			<i class="fa <?php echo epc_ecomae_h($mod['icon']); ?>"></i><?php echo epc_ecomae_h($mod['title']); ?>
		</button>
		<?php } ?>
	</nav>

	<div class="epm-faq-body">
		<p class="epm-faq-empty" id="epm_faq_empty">No matching questions — try another keyword or <a href="<?php echo epc_ecomae_h($base); ?>platform/contact">contact us</a>.</p>
		<?php foreach ($modules as $i => $mod) { ?>
		<section class="epm-faq-module<?php echo $i === 0 ? ' is-active' : ''; ?>" id="epm_faq_<?php echo epc_ecomae_h($mod['id']); ?>" data-module="<?php echo epc_ecomae_h($mod['id']); ?>">
			<div class="epm-faq-module__head">
				<h2><?php echo epc_ecomae_h($mod['title']); ?></h2>
				<p>Questions <?php echo epc_ecomae_h($mod['range']); ?> · <?php echo count($mod['items']); ?> topics</p>
			</div>
			<?php foreach ($mod['items'] as $item) {
				$status = (string) $item['status'];
				?>
			<article class="epm-faq-item" data-search="<?php echo epc_ecomae_h(strtolower($item['q'] . ' ' . $item['a'])); ?>">
				<button type="button" class="epm-faq-item__q" aria-expanded="false">
					<span class="epm-faq-item__num">Q<?php echo (int) $item['num']; ?></span>
					<span><?php echo epc_ecomae_h($item['q']); ?></span>
					<i class="fa fa-chevron-down epm-faq-item__chev" aria-hidden="true"></i>
				</button>
				<div class="epm-faq-item__a">
					<span class="epm-faq__status <?php echo epc_ecomae_h(epc_ecomae_faq_status_class($status)); ?>"><?php echo epc_ecomae_h($status); ?></span>
					<div><?php echo epc_ecomae_faq_format_answer((string) $item['a']); ?></div>
				</div>
			</article>
			<?php } ?>
		</section>
		<?php } ?>
	</div>

	<div class="epm-faq-cta">
		<p style="color:var(--epm-muted);margin-bottom:16px">Need a scoped answer for your tenant? Request a demo or talk to our team.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demo['days']; ?>-day demo</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/capabilities"><i class="fa fa-th"></i> Capabilities catalog</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/contact"><i class="fa fa-envelope"></i> Contact</a>
		</div>
	</div>
</div>
<script defer>
(function(){
	var tabs=document.querySelectorAll('.epm-faq-tab');
	var modules=document.querySelectorAll('.epm-faq-module');
	var search=document.getElementById('epm_faq_search');
	var empty=document.getElementById('epm_faq_empty');
	function showModule(id){
		for(var i=0;i<tabs.length;i++){tabs[i].classList.toggle('is-active',tabs[i].getAttribute('data-module')===id);}
		for(var j=0;j<modules.length;j++){modules[j].classList.toggle('is-active',modules[j].getAttribute('data-module')===id);}
	}
	for(var t=0;t<tabs.length;t++){
		tabs[t].addEventListener('click',function(){showModule(this.getAttribute('data-module'));});
	}
	document.querySelectorAll('.epm-faq-item__q').forEach(function(btn){
		btn.addEventListener('click',function(){
			var item=btn.closest('.epm-faq-item');
			var open=item.classList.toggle('is-open');
			btn.setAttribute('aria-expanded',open?'true':'false');
		});
	});
	function filterFaq(){
		var q=(search&&search.value||'').toLowerCase().trim();
		var anyVisible=false;
		document.querySelectorAll('.epm-faq-item').forEach(function(item){
			var hay=(item.getAttribute('data-search')||'');
			var match=!q||hay.indexOf(q)!==-1;
			item.classList.toggle('is-hidden',!match);
			if(match)anyVisible=true;
		});
		if(empty){empty.classList.toggle('is-visible',q&&!anyVisible);}
		if(q){
			for(var m=0;m<modules.length;m++){modules[m].classList.add('is-active');}
			for(var x=0;x<tabs.length;x++){tabs[x].classList.remove('is-active');}
		}
	}
	if(search){search.addEventListener('input',filterFaq);}
})();
</script>
	<?php
	return ob_get_clean();
}
