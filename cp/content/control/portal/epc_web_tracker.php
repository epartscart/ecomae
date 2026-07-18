<?php
/**
 * CP — Website traffic tracker (all tenants on Super CP; own site on tenant CP).
 * Route: /cp/control/portal/epc_web_tracker
 */
if (!defined('_ASTEXE_')) {
	$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== ''
		? ('?' . $_SERVER['QUERY_STRING'])
		: '';
	header('Location: /cp/control/portal/epc_web_tracker' . $qs, true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
if (!$isSuper && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	$isSuper = true;
}
$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');

$pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
if (!$pdo instanceof PDO && isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
	$pdo = $GLOBALS['db_link'];
}
if ($pdo instanceof PDO) {
	epc_web_tracker_ensure_schema($pdo);
}

$tenants = ($pdo instanceof PDO && function_exists('epc_portal_list_tenants'))
	? epc_portal_list_tenants($pdo)
	: array();

$ownKey = epc_web_tracker_resolve_site_key();
$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
if (!$isSuper) {
	$siteKey = $ownKey;
} elseif ($siteKey === '') {
	$siteKey = '_all';
}

$from = date('Y-m-d', time() - 7 * 86400);
$to = date('Y-m-d');
if (!empty($_GET['from'])) {
	$from = preg_replace('/[^0-9\-]/', '', (string) $_GET['from']);
}
if (!empty($_GET['to'])) {
	$to = preg_replace('/[^0-9\-]/', '', (string) $_GET['to']);
}

$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_epc_web_tracker.php';

epc_cp_page_frame_open(array(
	'class' => 'epc-web-tracker',
	'hero' => array(
		'badge' => $isSuper ? 'Super CP · all tenants' : 'Tenant traffic',
		'title' => 'Website tracker',
		'sub' => 'Pageviews, clicks, search, geography, devices, and full session timelines for guests and registered users.',
	),
));
?>
<style>
.epc-wt { --wt-ink:#0f172a; --wt-muted:#64748b; --wt-line:#e2e8f0; --wt-bg:#f8fafc; --wt-card:#fff; --wt-accent:#0ea5e9; --wt-ok:#059669; --wt-warn:#d97706; }
.epc-wt .wt-filters { display:flex; flex-wrap:wrap; gap:10px; align-items:end; margin:0 0 16px; padding:14px; background:var(--wt-card); border:1px solid var(--wt-line); border-radius:10px; }
.epc-wt .wt-filters label { display:block; font-size:11px; text-transform:uppercase; letter-spacing:.04em; color:var(--wt-muted); margin:0 0 4px; }
.epc-wt .wt-filters .form-control { min-width:140px; }
.epc-wt .wt-kpis { display:grid; grid-template-columns:repeat(auto-fill,minmax(140px,1fr)); gap:10px; margin-bottom:16px; }
.epc-wt .wt-kpi { background:linear-gradient(160deg,#fff 0%,#f1f5f9 100%); border:1px solid var(--wt-line); border-radius:10px; padding:12px 14px; }
.epc-wt .wt-kpi b { display:block; font-size:22px; color:var(--wt-ink); line-height:1.2; }
.epc-wt .wt-kpi span { font-size:12px; color:var(--wt-muted); }
.epc-wt .wt-grid { display:grid; grid-template-columns:1.2fr 1fr; gap:14px; margin-bottom:14px; }
@media (max-width:991px){ .epc-wt .wt-grid { grid-template-columns:1fr; } }
.epc-wt .wt-panel { background:var(--wt-card); border:1px solid var(--wt-line); border-radius:10px; overflow:hidden; }
.epc-wt .wt-panel h4 { margin:0; padding:12px 14px; font-size:14px; border-bottom:1px solid var(--wt-line); background:var(--wt-bg); }
.epc-wt .wt-panel .wt-body { padding:10px 14px; max-height:420px; overflow:auto; }
.epc-wt table.wt-table { width:100%; font-size:12px; }
.epc-wt table.wt-table th { color:var(--wt-muted); font-weight:600; border-bottom:1px solid var(--wt-line); padding:6px 4px; white-space:nowrap; }
.epc-wt table.wt-table td { border-bottom:1px solid #f1f5f9; padding:7px 4px; vertical-align:top; word-break:break-word; }
.epc-wt .wt-pill { display:inline-block; padding:2px 7px; border-radius:999px; font-size:11px; background:#e2e8f0; color:#334155; }
.epc-wt .wt-pill.reg { background:#d1fae5; color:#065f46; }
.epc-wt .wt-pill.guest { background:#e0f2fe; color:#075985; }
.epc-wt .wt-bars { display:flex; align-items:flex-end; gap:3px; height:90px; padding:8px 4px 0; }
.epc-wt .wt-bars i { flex:1; background:linear-gradient(180deg,#38bdf8,#0284c7); border-radius:3px 3px 0 0; min-width:4px; opacity:.85; }
.epc-wt .wt-muted { color:var(--wt-muted); font-size:12px; }
.epc-wt .wt-link { color:#0369a1; cursor:pointer; text-decoration:underline; }
.epc-wt #wt_session_modal .modal-body { max-height:70vh; overflow:auto; }
.epc-wt .wt-timeline { border-left:2px solid #bae6fd; margin:0 0 0 8px; padding:0 0 0 14px; }
.epc-wt .wt-timeline li { list-style:none; margin:0 0 10px; font-size:12px; position:relative; }
.epc-wt .wt-timeline li:before { content:''; position:absolute; left:-19px; top:4px; width:8px; height:8px; border-radius:50%; background:#0ea5e9; }
.epc-wt .wt-status { margin:0 0 12px; }
</style>

<div class="epc-wt">
	<div class="wt-filters">
		<?php if ($isSuper) { ?>
		<div>
			<label>Site / tenant</label>
			<select id="wt_site" class="form-control">
				<option value="_all"<?php echo $siteKey === '_all' ? ' selected' : ''; ?>>All sites (Super)</option>
				<option value="ecomae"<?php echo $siteKey === 'ecomae' ? ' selected' : ''; ?>>ecomae (marketing)</option>
				<?php foreach ($tenants as $t) {
					$sk = (string) ($t['site_key'] ?? '');
					if ($sk === '' || $sk === 'ecomae') continue;
					$lab = $sk . ' — ' . (string) ($t['hostname'] ?? '');
					echo '<option value="' . epc_web_tracker_h($sk) . '"' . ($siteKey === $sk ? ' selected' : '') . '>'
						. epc_web_tracker_h($lab) . '</option>';
				} ?>
			</select>
		</div>
		<?php } else { ?>
		<input type="hidden" id="wt_site" value="<?php echo epc_web_tracker_h($siteKey); ?>" />
		<div>
			<label>Site</label>
			<div class="form-control" style="background:#f8fafc;"><?php echo epc_web_tracker_h($siteKey); ?></div>
		</div>
		<?php } ?>
		<div>
			<label>From</label>
			<input type="date" id="wt_from" class="form-control" value="<?php echo epc_web_tracker_h($from); ?>" />
		</div>
		<div>
			<label>To</label>
			<input type="date" id="wt_to" class="form-control" value="<?php echo epc_web_tracker_h($to); ?>" />
		</div>
		<div>
			<button type="button" class="btn btn-primary" id="wt_reload"><i class="fa fa-refresh"></i> Refresh</button>
		</div>
		<div class="wt-muted" style="align-self:center;">
			Beacon: <code>/epc-web-tracker-collect.php</code> · storefront + ecomae marketing
		</div>
	</div>

	<div class="wt-status alert alert-info" id="wt_status">Loading traffic…</div>
	<div class="wt-kpis" id="wt_kpis"></div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Traffic by day</h4>
			<div class="wt-body" id="wt_daily"></div>
		</div>
		<div class="wt-panel">
			<h4><?php echo $isSuper ? 'By tenant / hostname' : 'Devices & browsers'; ?></h4>
			<div class="wt-body" id="wt_side_a"></div>
		</div>
	</div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Top pages (experience)</h4>
			<div class="wt-body" id="wt_pages"></div>
		</div>
		<div class="wt-panel">
			<h4>Geography</h4>
			<div class="wt-body" id="wt_geo"></div>
		</div>
	</div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Search terms</h4>
			<div class="wt-body" id="wt_search"></div>
		</div>
		<div class="wt-panel">
			<h4>Top clicks</h4>
			<div class="wt-body" id="wt_clicks"></div>
		</div>
	</div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Referrers &amp; UTM</h4>
			<div class="wt-body" id="wt_refs"></div>
		</div>
		<div class="wt-panel">
			<h4><?php echo $isSuper ? 'Devices & browsers' : 'Recent note'; ?></h4>
			<div class="wt-body" id="wt_side_b"></div>
		</div>
	</div>

	<div class="wt-panel" style="margin-bottom:20px;">
		<h4>Recent sessions — click a row for full timeline (pages + every click)</h4>
		<div class="wt-body" id="wt_sessions" style="max-height:520px;"></div>
	</div>
</div>

<div class="modal fade" id="wt_session_modal" tabindex="-1" role="dialog">
	<div class="modal-dialog modal-lg" role="document">
		<div class="modal-content">
			<div class="modal-header">
				<button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
				<h4 class="modal-title">Session timeline</h4>
			</div>
			<div class="modal-body" id="wt_session_body">Loading…</div>
		</div>
	</div>
</div>

<script>
(function(){
	var AJAX = <?php echo json_encode($ajaxUrl); ?>;
	var IS_SUPER = <?php echo $isSuper ? 'true' : 'false'; ?>;

	function $(id){ return document.getElementById(id); }
	function esc(s){
		return String(s==null?'':s).replace(/[&<>"']/g,function(c){
			return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'})[c];
		});
	}
	function dur(ms){
		ms = parseInt(ms,10)||0;
		if(ms<1000) return ms+' ms';
		var s=Math.round(ms/1000);
		if(s<60) return s+'s';
		var m=Math.floor(s/60), r=s%60;
		if(m<60) return m+'m '+r+'s';
		return Math.floor(m/60)+'h '+(m%60)+'m';
	}
	function fmtTs(ts){
		ts=parseInt(ts,10)||0;
		if(!ts) return '—';
		var d=new Date(ts*1000);
		return d.toLocaleString();
	}
	function table(headers, rowsHtml){
		return '<table class="wt-table"><thead><tr>'+headers.map(function(h){return '<th>'+esc(h)+'</th>';}).join('')+'</tr></thead><tbody>'+rowsHtml+'</tbody></table>';
	}

	function load(){
		var site=$('wt_site').value;
		var from=$('wt_from').value;
		var to=$('wt_to').value;
		$('wt_status').className='wt-status alert alert-info';
		$('wt_status').textContent='Loading traffic…';
		var url=AJAX+'?action=dashboard&site_key='+encodeURIComponent(site)+'&from='+encodeURIComponent(from)+'&to='+encodeURIComponent(to);
		fetch(url,{credentials:'same-origin'}).then(function(r){return r.json();}).then(function(j){
			if(!j||!j.ok){
				$('wt_status').className='wt-status alert alert-danger';
				$('wt_status').textContent=(j&&j.error)||'Failed to load';
				return;
			}
			render(j);
		}).catch(function(e){
			$('wt_status').className='wt-status alert alert-danger';
			$('wt_status').textContent='Network error';
		});
	}

	function render(j){
		var d=j.data||{};
		var s=d.summary||{};
		$('wt_status').className='wt-status alert alert-success';
		$('wt_status').textContent='Updated · site '+(j.site_key||'')+' · '+fmtTs(j.from)+' → '+fmtTs(j.to);

		$('wt_kpis').innerHTML=[
			['Sessions', s.sessions],
			['Visitors', s.visitors],
			['Pageviews', s.pageviews],
			['Clicks', s.clicks],
			['Searches', s.searches],
			['Guests', s.guest_sessions],
			['Registered', s.registered_sessions],
			['Avg time', dur(s.avg_duration_ms)],
			['Avg pages', s.avg_pages],
			['Bounce %', s.bounce_rate]
		].map(function(x){return '<div class="wt-kpi"><b>'+esc(x[1])+'</b><span>'+esc(x[0])+'</span></div>';}).join('');

		var daily=d.daily||[];
		var max=1;
		daily.forEach(function(x){ if((+x.pageviews)>max) max=+x.pageviews; });
		var bars='<div class="wt-bars" title="Pageviews by day">'+daily.map(function(x){
			var h=Math.max(4, Math.round(((+x.pageviews)/max)*80));
			return '<i style="height:'+h+'px" title="'+esc(x.date)+': '+esc(x.pageviews)+' views / '+esc(x.sessions)+' sessions"></i>';
		}).join('')+'</div>';
		var dailyRows=daily.map(function(x){
			return '<tr><td>'+esc(x.date)+'</td><td>'+esc(x.sessions)+'</td><td>'+esc(x.pageviews)+'</td></tr>';
		}).join('')||'<tr><td colspan="3" class="wt-muted">No data yet — browse the storefront to generate traffic.</td></tr>';
		$('wt_daily').innerHTML=bars+table(['Date','Sessions','Pageviews'], dailyRows);

		var byTenant=d.by_tenant||[];
		var devices=d.devices||[];
		if(IS_SUPER && (j.site_key==='_all' || byTenant.length)){
			$('wt_side_a').innerHTML=table(['Site','Host','Sessions','Views','Visitors'],
				byTenant.map(function(x){
					return '<tr><td><span class="wt-link" data-site="'+esc(x.site_key)+'">'+esc(x.site_key)+'</span></td><td>'+esc(x.hostname)+'</td><td>'+esc(x.sessions)+'</td><td>'+esc(x.pageviews)+'</td><td>'+esc(x.visitors)+'</td></tr>';
				}).join('')||'<tr><td colspan="5" class="wt-muted">No tenant traffic yet.</td></tr>'
			);
			$('wt_side_b').innerHTML=table(['Device','Browser','OS','Sessions'],
				devices.map(function(x){return '<tr><td>'+esc(x.device_type)+'</td><td>'+esc(x.browser)+'</td><td>'+esc(x.os)+'</td><td>'+esc(x.sessions)+'</td></tr>';}).join('')||'<tr><td colspan="4" class="wt-muted">—</td></tr>'
			);
		} else {
			$('wt_side_a').innerHTML=table(['Device','Browser','OS','Sessions'],
				devices.map(function(x){return '<tr><td>'+esc(x.device_type)+'</td><td>'+esc(x.browser)+'</td><td>'+esc(x.os)+'</td><td>'+esc(x.sessions)+'</td></tr>';}).join('')||'<tr><td colspan="4" class="wt-muted">—</td></tr>'
			);
			$('wt_side_b').innerHTML='<p class="wt-muted">Open a recent session below for the full click-by-click timeline, page experience (time on page, scroll), geography, and search events.</p>';
		}

		$('wt_pages').innerHTML=table(['Path','Views','Sessions','Avg time','Scroll %'],
			(d.top_pages||[]).map(function(x){
				return '<tr><td>'+esc(x.path)+'</td><td>'+esc(x.views)+'</td><td>'+esc(x.sessions)+'</td><td>'+esc(dur(x.avg_time_ms))+'</td><td>'+esc(x.avg_scroll)+'</td></tr>';
			}).join('')||'<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_geo').innerHTML=table(['Country','City','Sessions'],
			(d.geo||[]).map(function(x){
				var c=(x.country_name||x.country_code||'Unknown');
				if(x.country_code) c+=' ('+x.country_code+')';
				return '<tr><td>'+esc(c)+'</td><td>'+esc(x.city||'—')+'</td><td>'+esc(x.sessions)+'</td></tr>';
			}).join('')||'<tr><td colspan="3" class="wt-muted">—</td></tr>'
		);

		$('wt_search').innerHTML=table(['Query','Context','Hits','Sessions'],
			(d.searches||[]).map(function(x){
				return '<tr><td><strong>'+esc(x.search_query)+'</strong></td><td>'+esc(x.search_context)+'</td><td>'+esc(x.hits)+'</td><td>'+esc(x.sessions)+'</td></tr>';
			}).join('')||'<tr><td colspan="4" class="wt-muted">No searches captured yet.</td></tr>'
		);

		$('wt_clicks').innerHTML=table(['Path','Element','Text / href','Hits'],
			(d.top_clicks||[]).map(function(x){
				var el=(x.element_tag||'')+(x.element_id?'#'+x.element_id:'');
				var tx=(x.element_text||x.element_href||'—');
				return '<tr><td>'+esc(x.path)+'</td><td>'+esc(el)+'</td><td>'+esc(tx)+'</td><td>'+esc(x.hits)+'</td></tr>';
			}).join('')||'<tr><td colspan="4" class="wt-muted">—</td></tr>'
		);

		$('wt_refs').innerHTML=table(['Referrer','UTM source','Medium','Campaign','Sessions'],
			(d.referrers||[]).map(function(x){
				return '<tr><td>'+esc(x.host)+'</td><td>'+esc(x.utm_source||'—')+'</td><td>'+esc(x.utm_medium||'—')+'</td><td>'+esc(x.utm_campaign||'—')+'</td><td>'+esc(x.sessions)+'</td></tr>';
			}).join('')||'<tr><td colspan="5" class="wt-muted">—</td></tr>'
		);

		$('wt_sessions').innerHTML=table(['When','Who','Geo','Device','Land → Exit','Pages','Clicks','Time',''],
			(d.recent_sessions||[]).map(function(x){
				var who=x.is_registered=='1'||x.is_registered==1
					? '<span class="wt-pill reg">User #'+esc(x.user_id)+'</span>'
					: '<span class="wt-pill guest">Guest</span>';
				if(IS_SUPER) who+=' <span class="wt-pill">'+esc(x.site_key)+'</span>';
				var geo=[x.city,x.country_code].filter(Boolean).join(', ')||'—';
				var path=esc(x.landing_path||'/')+' → '+esc(x.exit_path||'—');
				return '<tr><td>'+esc(fmtTs(x.last_seen_at))+'</td><td>'+who+'</td><td>'+esc(geo)+'</td><td>'+esc((x.device_type||'')+' / '+(x.browser||''))+'</td><td>'+path+'</td><td>'+esc(x.pageview_count)+'</td><td>'+esc(x.event_count)+'</td><td>'+esc(dur(x.duration_ms))+'</td><td><a class="wt-link wt-open" data-id="'+esc(x.id)+'">Timeline</a></td></tr>';
			}).join('')||'<tr><td colspan="9" class="wt-muted">No sessions yet.</td></tr>'
		);

		Array.prototype.forEach.call(document.querySelectorAll('#wt_side_a [data-site]'), function(a){
			a.addEventListener('click', function(){
				$('wt_site').value=a.getAttribute('data-site');
				load();
			});
		});
		Array.prototype.forEach.call(document.querySelectorAll('.wt-open'), function(a){
			a.addEventListener('click', function(ev){
				ev.preventDefault();
				openSession(a.getAttribute('data-id'));
			});
		});
	}

	function openSession(id){
		$('wt_session_body').innerHTML='Loading…';
		if(window.jQuery){ jQuery('#wt_session_modal').modal('show'); }
		else { $('wt_session_modal').style.display='block'; }
		var site=$('wt_site').value;
		fetch(AJAX+'?action=session&id='+encodeURIComponent(id)+'&site_key='+encodeURIComponent(site),{credentials:'same-origin'})
			.then(function(r){return r.json();})
			.then(function(j){
				if(!j||!j.ok||!j.detail||!j.detail.session){
					$('wt_session_body').innerHTML='<p class="text-danger">Session not found.</p>';
					return;
				}
				var s=j.detail.session;
				var pvs=j.detail.pageviews||[];
				var evs=j.detail.events||[];
				var html='';
				html+='<p><strong>'+esc(s.site_key)+'</strong> · '+esc(s.hostname)+' · IP '+esc(s.ip)
					+' · '+(s.is_registered==1||s.is_registered=='1'?'Registered user #'+esc(s.user_id):'Guest')
					+' · '+esc(s.city||'')+' '+esc(s.region||'')+' '+esc(s.country_name||s.country_code||'')
					+' · '+esc(s.device_type)+' / '+esc(s.browser)+' / '+esc(s.os)+'</p>';
				html+='<p class="wt-muted">Landed '+esc(fmtTs(s.first_seen_at))+' · Last '+esc(fmtTs(s.last_seen_at))
					+' · Duration '+esc(dur(s.duration_ms))
					+' · Referrer '+esc(s.referrer_host||'(direct)')
					+(s.utm_source?' · UTM '+esc(s.utm_source)+'/'+esc(s.utm_medium)+'/'+esc(s.utm_campaign):'')
					+'</p>';
				html+='<h5>Page experience</h5><ul class="wt-timeline">';
				pvs.forEach(function(p){
					html+='<li><strong>'+esc(fmtTs(p.ts))+'</strong> '+esc(p.path)
						+(p.query?'?'+esc(p.query):'')
						+' <span class="wt-muted">· '+esc(p.title)+' · on-page '+esc(dur(p.time_on_page_ms))
						+' · scroll '+esc(p.scroll_max_pct)+'% · load '+esc(p.load_time_ms)+'ms</span></li>';
				});
				html+='</ul><h5>Clicks &amp; events</h5><ul class="wt-timeline">';
				evs.forEach(function(e){
					var line='<strong>'+esc(fmtTs(e.ts))+'</strong> <span class="wt-pill">'+esc(e.event_type)+'</span> ';
					if(e.event_type==='search'){
						line+='search “'+esc(e.search_query)+'” <span class="wt-muted">('+esc(e.search_context)+')</span>';
					} else if(e.event_type==='click'||e.event_type==='outbound'){
						line+=esc(e.element_tag)+(e.element_id?'#'+esc(e.element_id):'')
							+' “'+esc(e.element_text)+'” '
							+(e.element_href?'→ '+esc(e.element_href):'')
							+' <span class="wt-muted">@ '+esc(e.x)+','+esc(e.y)+' on '+esc(e.path)+'</span>';
					} else {
						line+=esc(e.path||'')+' <span class="wt-muted">'+(e.meta_json?esc(e.meta_json):'')+'</span>';
					}
					html+='<li>'+line+'</li>';
				});
				if(!evs.length) html+='<li class="wt-muted">No click/search events.</li>';
				html+='</ul>';
				$('wt_session_body').innerHTML=html;
			});
	}

	$('wt_reload').addEventListener('click', load);
	if($('wt_site').tagName==='SELECT'){
		$('wt_site').addEventListener('change', load);
	}
	load();
})();
</script>
<?php
epc_cp_page_frame_close();
