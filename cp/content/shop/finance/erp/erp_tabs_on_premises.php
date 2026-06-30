<?php
/**
 * On-Premises Deployment — self-hosted ERP for clients who want local installation.
 * Docker-based, license manager, offline activation, BOS connector, backup tools.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-server"></i> On-Premises Deployment',
	'Self-hosted ERP deployment — Docker-based installation, license management, offline activation, and optional BOS cloud connector.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'On-Premises'),
	),
	array(array('label' => 'Generate license', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-key'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-cloud-download"></i> Deployment packages</h4>
	<p class="text-muted">Download the on-premises installer for your client's server. Choose Docker (recommended) or bare-metal installation.</p>
	<div class="row">
		<div class="col-md-6">
			<div class="panel panel-primary">
				<div class="panel-heading"><strong><i class="fa fa-cube"></i> Docker compose (recommended)</strong></div>
				<div class="panel-body">
					<p>Complete containerized stack: PHP 8.3 + MySQL 8 + Redis + Nginx. One-command setup.</p>
					<ul style="font-size:13px;">
						<li>Requirements: Docker 24+, 4 CPU, 16GB RAM, 500GB disk</li>
						<li>Auto-configures SSL (Let's Encrypt or self-signed)</li>
						<li>Includes backup automation + log rotation</li>
						<li>Automatic health monitoring</li>
					</ul>
					<code style="display:block;background:#1e293b;color:#e2e8f0;padding:8px;border-radius:4px;font-size:12px;margin-top:8px;">
						curl -sSL https://install.ecomae.com | bash -s -- --license YOUR_KEY
					</code>
					<button class="btn btn-primary btn-sm" style="margin-top:10px;"><i class="fa fa-download"></i> Download docker-compose.yml</button>
				</div>
			</div>
		</div>
		<div class="col-md-6">
			<div class="panel panel-default">
				<div class="panel-heading"><strong><i class="fa fa-linux"></i> Bare-metal installer</strong></div>
				<div class="panel-body">
					<p>Traditional server installation. Ubuntu 22.04+ / Windows Server 2019+.</p>
					<ul style="font-size:13px;">
						<li>Requirements: PHP 8.1+, MySQL 8.0+, Nginx/Apache</li>
						<li>Manual SSL configuration required</li>
						<li>Includes systemd service files</li>
						<li>Cron jobs for backups and maintenance</li>
					</ul>
					<code style="display:block;background:#1e293b;color:#e2e8f0;padding:8px;border-radius:4px;font-size:12px;margin-top:8px;">
						wget https://releases.ecomae.com/latest.tar.gz && tar xzf latest.tar.gz && ./install.sh
					</code>
					<button class="btn btn-default btn-sm" style="margin-top:10px;"><i class="fa fa-download"></i> Download installer</button>
				</div>
			</div>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-key"></i> License management</h4>
	<p class="text-muted">Each on-premises instance requires a license key tied to the server's MAC address or domain. Supports offline activation.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="op_licenses">
		<thead><tr><th>License key</th><th>Client</th><th>Server</th><th>Type</th><th>Users</th><th>Modules</th><th>Expires</th><th>Status</th></tr></thead>
		<tbody></tbody>
	</table>
	<button class="btn btn-primary btn-sm" id="op_gen_lic"><i class="fa fa-plus"></i> Generate new license</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plug"></i> BOS connector (optional)</h4>
	<p class="text-muted">On-premises instances can optionally connect back to your BOS for health monitoring, updates, and multi-branch sync.</p>
	<div class="pm-fields">
		<div class="pm-field"><label>Connection mode</label>
			<select class="form-control input-sm">
				<option>Full cloud sync — data backup + monitoring + updates</option>
				<option>Monitoring only — health check, no data sync</option>
				<option>Updates only — pull new versions, no data sync</option>
				<option>Air-gapped — completely offline</option>
			</select>
		</div>
		<div class="pm-field"><label>Sync frequency</label>
			<select class="form-control input-sm"><option>Real-time</option><option>Hourly</option><option>Daily</option><option>Manual only</option></select>
		</div>
		<div class="pm-field"><label>Data sync scope</label>
			<select class="form-control input-sm"><option>All modules</option><option>Finance only</option><option>Inventory only</option><option>Custom selection</option></select>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Server requirements</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Component</th><th>Minimum</th><th>Recommended</th><th>Enterprise</th></tr></thead>
		<tbody>
			<tr><td><strong>CPU</strong></td><td>4 cores</td><td>8 cores</td><td>16+ cores</td></tr>
			<tr><td><strong>RAM</strong></td><td>8 GB</td><td>16 GB</td><td>32+ GB</td></tr>
			<tr><td><strong>Storage</strong></td><td>100 GB SSD</td><td>500 GB SSD</td><td>1 TB+ NVMe</td></tr>
			<tr><td><strong>OS</strong></td><td colspan="3">Ubuntu 22.04+ / Debian 12+ / RHEL 9+ / Windows Server 2019+</td></tr>
			<tr><td><strong>Database</strong></td><td colspan="3">MySQL 8.0+ or MariaDB 10.6+</td></tr>
			<tr><td><strong>PHP</strong></td><td colspan="3">PHP 8.1+ (8.3 recommended)</td></tr>
			<tr><td><strong>Network</strong></td><td colspan="3">Static IP or domain (internet optional after activation)</td></tr>
		</tbody>
	</table>
</div>
<script>
(function(){
	var licenses=[
		{key:'LIC-2026-A7B3-C9D1',client:'Indus Jewellers',server:'192.168.1.100',type:'Perpetual',users:'25',modules:'All',expires:'—',status:'Active'},
		{key:'LIC-2026-E4F2-G8H5',client:'Desert Gems LLC',server:'erp.desertgems.ae',type:'Annual',users:'10',modules:'Finance + Inventory',expires:'2027-06-01',status:'Active'},
		{key:'LIC-2025-K1L9-M3N7',client:'Gold House',server:'10.0.0.50',type:'Annual',users:'15',modules:'All',expires:'2026-01-15',status:'Expired'},
	];
	var tb=document.querySelector('#op_licenses tbody');
	licenses.forEach(function(l){
		var cls=l.status==='Active'?'success':(l.status==='Expired'?'danger':'warning');
		tb.innerHTML+='<tr><td><code>'+l.key+'</code></td><td>'+l.client+'</td><td><small>'+l.server+'</small></td><td>'+l.type+'</td><td>'+l.users+'</td><td><small>'+l.modules+'</small></td><td>'+l.expires+'</td><td><span class="label label-'+cls+'">'+l.status+'</span></td></tr>';
	});
})();
</script>
<?php
erp_section_card('On-Premises Deployment', ob_get_clean(), array('icon' => 'fa-server'));
