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

    /// <summary>
    /// Standalone SVG for &lt;img src="/content/general_pages/epc_animated_epartscart_logo.php"&gt;
    /// (PHP library dies with "No access" outside _ASTEXE_; ASP.NET bridge serves this instead).
    /// </summary>
    public const string AnimatedCartMarkSvg = """
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 220 100" role="img" aria-label="EpartsCart">
  <style>
    .s,.c,.h,.b{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-linejoin:round}
    .c{stroke-width:12}.h,.b{stroke-width:9}.s{stroke-width:8}
    .r{fill:none;stroke:#dc2626;stroke-dasharray:14 12;stroke-linecap:round;stroke-width:5;opacity:.55}
    .g path,.g circle{fill:#dc2626}.g .hole{fill:#fff}
    .p rect,.box{fill:#dc2626}.p path{fill:none;stroke:#fff;stroke-linecap:round;stroke-width:3}
    .ring circle,.ring path{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-width:5}
    .tyre{fill:#dc2626}.rim{fill:#fff}.wh{fill:#dc2626}
    .spokes,.tread{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-width:2.4}
    .tread{stroke:#fff;stroke-width:2.8}
  </style>
  <path class="s" d="M10 28 H72"/><path class="s" d="M0 48 H68"/><path class="s" d="M20 68 H76"/>
  <path class="r" d="M70 96 H190"/>
  <g>
    <path class="c" d="M66 18 H178 C186 18 192 25 190 33 L177 70 H83 L66 18 Z"/>
    <path class="h" d="M64 18 L52 18 L43 10"/>
    <path class="b" d="M82 32 H172 L163 58 H92 Z"/>
    <g>
      <g class="g" transform="translate(126 48)">
        <path d="M0 -18 L4 -13 L10 -15 L12 -9 L18 -7 L15 -1 L18 5 L12 8 L10 15 L3 13 L-2 18 L-7 13 L-14 15 L-15 8 L-20 5 L-17 -1 L-20 -7 L-15 -9 L-14 -15 L-7 -13 Z"/>
        <circle r="12"/><circle r="5" class="hole"/>
      </g>
      <g class="p" transform="translate(98 39)">
        <rect x="0" y="0" width="24" height="18" rx="4"/>
        <path d="M3 5 H21 M3 10 H21"/><path d="M12 18 V31"/>
      </g>
      <g class="ring" transform="translate(152 48)"><circle r="12"/><path d="M8 -8 L16 -15"/></g>
      <rect class="box" x="137" y="31" width="24" height="17" rx="4"/>
    </g>
    <g transform="translate(86 88)">
      <circle class="tyre" r="16"/><circle class="rim" r="10"/><circle class="wh" r="4"/>
      <path class="spokes" d="M0 -10 V10 M-10 0 H10 M-7 -7 L7 7 M7 -7 L-7 7"/>
      <path class="tread" d="M-5 -15 L-2 -11 M5 -15 L2 -11 M15 -5 L11 -2 M15 5 L11 2 M5 15 L2 11 M-5 15 L-2 11 M-15 5 L-11 2 M-15 -5 L-11 -2"/>
    </g>
    <g transform="translate(166 88)">
      <circle class="tyre" r="16"/><circle class="rim" r="10"/><circle class="wh" r="4"/>
      <path class="spokes" d="M0 -10 V10 M-10 0 H10 M-7 -7 L7 7 M7 -7 L-7 7"/>
      <path class="tread" d="M-5 -15 L-2 -11 M5 -15 L2 -11 M15 -5 L11 -2 M15 5 L11 2 M5 15 L2 11 M-5 15 L-2 11 M-15 5 L-11 2 M-15 -5 L-11 -2"/>
    </g>
  </g>
</svg>
""";

    /// <summary>HTML fragment for animated_epartscart_logo.php (inline mark + text).</summary>
    public static readonly string AnimatedCartFragmentHtml =
        "<span class=\"epc-animated-logo epc-animated-logo--header\" aria-label=\"EpartsCart\">"
        + "<span class=\"epc-animated-logo__text\">eparts</span>"
        + "<span class=\"epc-animated-logo__mark\" aria-hidden=\"true\">"
        + AnimatedCartMarkSvg
        + "</span></span>";
}
