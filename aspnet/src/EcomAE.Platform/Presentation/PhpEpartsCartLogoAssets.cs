namespace EcomAE.Platform.Presentation;

/// <summary>Inline CSS/SVG for PHP eparts animated logo (from epc_animated_epartscart_logo.php).</summary>
public static class PhpEpartsCartLogoAssets
{
    public const string Css = """
.epc-animated-logo{align-items:center;display:inline-flex;gap:8px;line-height:1;max-width:100%;white-space:nowrap;vertical-align:middle}
.epc-animated-logo__mark{display:inline-flex;flex:0 0 auto;height:40px;width:90px}
.epc-animated-logo__mark svg{display:block;height:100%;overflow:visible;width:100%}
.epc-animated-logo__text{color:#dc2626!important;font-family:Arial,Helvetica,sans-serif;font-size:28px;font-style:italic;font-weight:900;letter-spacing:-.055em;text-transform:lowercase;transform:skewX(-8deg)}
.epc-logo-speed,.epc-logo-cart,.epc-logo-handle,.epc-logo-basket{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-linejoin:round}
.epc-logo-cart{stroke-width:12}
.epc-logo-handle,.epc-logo-basket{stroke-width:9}
.epc-logo-speed{stroke-width:8;animation:epcLogoSpeed 1.4s ease-in-out infinite}
.epc-logo-road{fill:none;stroke:#dc2626;stroke-dasharray:14 12;stroke-linecap:round;stroke-width:5;animation:epcLogoRoadMove .9s linear infinite;opacity:.55}
.epc-logo-speed--two{animation-delay:.15s}
.epc-logo-speed--three{animation-delay:.3s}
.epc-logo-cart-motion{animation:epcLogoCartDrive 1.2s ease-in-out infinite;transform-box:fill-box;transform-origin:center}
.epc-logo-gear{animation:epcLogoGearSpin 2.4s linear infinite;transform-box:fill-box;transform-origin:center}
.epc-logo-gear path,.epc-logo-gear circle{fill:#dc2626}
.epc-logo-gear .epc-logo-gear-hole{fill:#fff}
.epc-logo-parts{animation:epcLogoPartsBounce 1.6s ease-in-out infinite}
.epc-logo-piston rect,.epc-logo-box{fill:#dc2626}
.epc-logo-piston path{fill:none;stroke:#fff;stroke-linecap:round;stroke-width:3}
.epc-logo-ring circle,.epc-logo-ring path{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-width:5}
.epc-logo-wheel{filter:drop-shadow(0 2px 0 rgba(0,0,0,.08))}
.epc-logo-wheel-spin{animation:epcLogoWheelRoll .45s linear infinite;transform-box:fill-box;transform-origin:center}
.epc-logo-tyre{fill:#dc2626}
.epc-logo-wheel-rim{fill:#fff}
.epc-logo-wheel .epc-logo-wheel-hole{fill:#dc2626}
.epc-logo-wheel-spokes,.epc-logo-wheel-tread{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-width:2.4}
.epc-logo-wheel-tread{stroke:#fff;stroke-width:2.8}
@keyframes epcLogoSpeed{0%,100%{opacity:.42;transform:translateX(0)}50%{opacity:1;transform:translateX(-10px)}}
@keyframes epcLogoGearSpin{to{transform:rotate(360deg)}}
@keyframes epcLogoCartDrive{0%,100%{transform:translateX(-4px) translateY(0)}50%{transform:translateX(8px) translateY(2px)}}
@keyframes epcLogoWheelRoll{to{transform:rotate(360deg)}}
@keyframes epcLogoRoadMove{to{stroke-dashoffset:-26}}
@keyframes epcLogoPartsBounce{0%,100%{transform:translateY(0)}50%{transform:translateY(-3px)}}
.epc-cp-header-logo .epc-animated-logo__mark{height:36px;width:80px}
.epc-cp-header-logo .epc-animated-logo__text{font-size:24px}
.epc-cp-dash-brand .epc-animated-logo__mark{height:44px;width:98px}
.epc-cp-dash-brand .epc-animated-logo__text{font-size:30px}
.epc-animated-logo--dash .epc-animated-logo__mark{height:44px;width:98px}
.epc-animated-logo--dash .epc-animated-logo__text{font-size:30px}
.epc-erp-topbar__brand .epc-animated-logo__mark{height:34px;width:76px}
.epc-erp-topbar__brand .epc-animated-logo__text{font-size:22px}
@media (prefers-reduced-motion:reduce){
  .epc-logo-cart-motion,.epc-logo-wheel-spin,.epc-logo-speed,.epc-logo-road,.epc-logo-gear,.epc-logo-parts{animation:none!important}
}
""";

    public const string EcomaeMarkSvg = """
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="ECOM AE">
  <rect width="64" height="64" rx="14" fill="#0c4a6e"/>
  <text x="32" y="40" text-anchor="middle" font-family="Arial,Helvetica,sans-serif" font-size="28" font-weight="800" fill="#fff">e</text>
</svg>
""";
}
