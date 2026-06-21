<?php
/**
 * PHP-served brand mark for ECOM AE.
 *
 * The static PNG asset (/content/files/images/ecomae-logo.png) is not present in
 * the docroot, so every <img> that referenced it rendered broken. Static assets
 * under some paths are also not reliably served on the marketing host, whereas
 * .php endpoints always are — so this emits a crisp, self-contained SVG monogram
 * with the correct image/svg+xml content type. epc_ecomae_platform_logo_url()
 * points here, fixing the logo across the marketing site, CP login and ERP.
 */

header('Content-Type: image/svg+xml; charset=utf-8');
header('Cache-Control: public, max-age=86400');

echo '<?xml version="1.0" encoding="UTF-8"?>' . "\n";
?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" width="96" height="96" role="img" aria-label="ECOM AE">
  <defs>
    <linearGradient id="eaG" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#2a6df4"/>
      <stop offset="1" stop-color="#1a56db"/>
    </linearGradient>
  </defs>
  <rect x="2" y="2" width="92" height="92" rx="22" fill="url(#eaG)"/>
  <rect x="2" y="2" width="92" height="92" rx="22" fill="none" stroke="#ffffff" stroke-opacity="0.18" stroke-width="2"/>
  <!-- stylised "e" / commerce-cloud arc -->
  <path d="M67 38c-4.5-7.2-12.6-12-21.8-12C31.3 26 20 37.3 20 51.2 20 65 31.3 76.3 45.2 76.3c9.2 0 17.3-4.9 21.8-12.2"
        fill="none" stroke="#ffffff" stroke-width="8" stroke-linecap="round"/>
  <path d="M45 51.2h22.5" fill="none" stroke="#ffffff" stroke-width="8" stroke-linecap="round"/>
  <circle cx="70" cy="30" r="6.5" fill="#bfe0ff"/>
</svg>
