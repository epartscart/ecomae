<?php
/**
 * Industry Frontend Base Template
 *
 * Shared layout for all 28 industry groups. Each industry template sets its
 * $industryData array and then includes this file for rendering.
 *
 * Expected $industryData keys:
 *   - name, tagline, description, icon, color_primary, color_accent, bg_from, bg_to
 *   - hero_image (CSS gradient/image), hero_animation (CSS keyframe name)
 *   - features[] (array of feature cards)
 *   - sample_products[] (array of product/service samples)
 *   - sub_industries[] (array of sub-industry names)
 *   - stats[] (array of stat cards)
 *   - testimonial (quote + author)
 *   - cta_cp_text, cta_erp_text
 */
defined('_ASTEXE_') or die('No access');

$name = $industryData['name'] ?? 'Industry';
$tagline = $industryData['tagline'] ?? 'Enterprise solutions';
$desc = $industryData['description'] ?? '';
$icon = $industryData['icon'] ?? 'fa-industry';
$primary = $industryData['color_primary'] ?? '#3b82f6';
$accent = $industryData['color_accent'] ?? '#60a5fa';
$bgFrom = $industryData['bg_from'] ?? '#0f172a';
$bgTo = $industryData['bg_to'] ?? '#1e3a5f';
$heroAnim = $industryData['hero_animation'] ?? 'fadeInUp';
$features = $industryData['features'] ?? [];
$products = $industryData['sample_products'] ?? [];
$subIndustries = $industryData['sub_industries'] ?? [];
$stats = $industryData['stats'] ?? [];
$testimonial = $industryData['testimonial'] ?? null;
$ctaCp = $industryData['cta_cp_text'] ?? 'Open Control Panel';
$ctaErp = $industryData['cta_erp_text'] ?? 'Launch ERP';
$particles = $industryData['particles'] ?? 'dots';
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title><?php echo htmlspecialchars($name); ?> — ecomae Platform</title>
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">
<style>
*{margin:0;padding:0;box-sizing:border-box}
:root{--primary:<?php echo $primary;?>;--accent:<?php echo $accent;?>;--bg-from:<?php echo $bgFrom;?>;--bg-to:<?php echo $bgTo;?>;}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#e2e8f0;background:var(--bg-from);overflow-x:hidden}
a{color:var(--accent);text-decoration:none}
a:hover{text-decoration:underline}

/* Hero */
.ind-hero{min-height:90vh;display:flex;align-items:center;justify-content:center;text-align:center;position:relative;background:linear-gradient(135deg,var(--bg-from),var(--bg-to));overflow:hidden}
.ind-hero::before{content:'';position:absolute;inset:0;background:radial-gradient(ellipse at 30% 50%,rgba(255,255,255,.03) 0%,transparent 70%)}
.ind-hero-content{position:relative;z-index:2;max-width:800px;padding:40px 20px;animation:heroFadeIn 1s ease-out}
.ind-hero h1{font-size:clamp(2.5rem,6vw,4.5rem);font-weight:800;background:linear-gradient(135deg,#fff,var(--accent));-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;margin-bottom:16px;line-height:1.1}
.ind-hero .tagline{font-size:1.4rem;color:rgba(255,255,255,.8);margin-bottom:24px;font-weight:300}
.ind-hero .desc{font-size:1rem;color:rgba(255,255,255,.6);max-width:600px;margin:0 auto 32px;line-height:1.6}
.ind-hero .hero-icon{font-size:5rem;color:var(--accent);margin-bottom:20px;animation:pulse 2s infinite}

/* Particles */
.particles{position:absolute;inset:0;overflow:hidden;z-index:1}
.particle{position:absolute;border-radius:50%;background:var(--accent);opacity:.15;animation:float 8s infinite}
.particle:nth-child(2n){animation-duration:12s;animation-delay:-3s}
.particle:nth-child(3n){animation-duration:10s;animation-delay:-5s}

/* Stats bar */
.ind-stats{display:flex;justify-content:center;gap:40px;padding:60px 20px;background:rgba(0,0,0,.3);flex-wrap:wrap}
.ind-stat{text-align:center;animation:fadeInUp .6s ease-out both}
.ind-stat:nth-child(2){animation-delay:.1s}
.ind-stat:nth-child(3){animation-delay:.2s}
.ind-stat:nth-child(4){animation-delay:.3s}
.ind-stat h3{font-size:2.5rem;font-weight:800;color:var(--accent)}
.ind-stat p{font-size:.85rem;color:rgba(255,255,255,.6);margin-top:4px}

/* Features */
.ind-features{padding:80px 20px;max-width:1200px;margin:0 auto}
.ind-features h2{text-align:center;font-size:2.2rem;margin-bottom:50px;color:#fff}
.ind-features-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:24px}
.ind-feature-card{background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.08);border-radius:16px;padding:30px;transition:transform .3s,box-shadow .3s,border-color .3s}
.ind-feature-card:hover{transform:translateY(-6px);box-shadow:0 20px 40px rgba(0,0,0,.3);border-color:var(--accent)}
.ind-feature-card .feat-icon{font-size:2rem;color:var(--accent);margin-bottom:12px}
.ind-feature-card h4{font-size:1.1rem;margin-bottom:8px;color:#fff}
.ind-feature-card p{font-size:.9rem;color:rgba(255,255,255,.6);line-height:1.5}

/* Products/Services */
.ind-products{padding:80px 20px;background:rgba(0,0,0,.2)}
.ind-products h2{text-align:center;font-size:2.2rem;margin-bottom:50px;color:#fff}
.ind-products-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:20px;max-width:1200px;margin:0 auto}
.ind-product{background:rgba(255,255,255,.06);border-radius:12px;padding:20px;border:1px solid rgba(255,255,255,.06);transition:all .3s}
.ind-product:hover{background:rgba(255,255,255,.1);border-color:var(--accent)}
.ind-product .prod-price{font-size:1.3rem;font-weight:700;color:var(--accent);margin-bottom:6px}
.ind-product .prod-name{font-size:1rem;color:#fff;margin-bottom:4px}
.ind-product .prod-cat{font-size:.8rem;color:rgba(255,255,255,.5)}
.ind-product .prod-icon{font-size:2.5rem;color:var(--primary);margin-bottom:10px;opacity:.7}

/* Sub-industries */
.ind-subs{padding:80px 20px;max-width:1200px;margin:0 auto}
.ind-subs h2{text-align:center;font-size:2.2rem;margin-bottom:40px;color:#fff}
.ind-subs-list{display:flex;flex-wrap:wrap;gap:12px;justify-content:center}
.ind-sub-tag{background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.1);border-radius:100px;padding:10px 20px;font-size:.9rem;color:rgba(255,255,255,.8);transition:all .3s;cursor:default}
.ind-sub-tag:hover{background:var(--primary);border-color:var(--primary);color:#fff;transform:scale(1.05)}

/* Testimonial */
.ind-testimonial{padding:80px 40px;text-align:center;background:rgba(0,0,0,.3)}
.ind-testimonial blockquote{font-size:1.3rem;font-style:italic;color:rgba(255,255,255,.8);max-width:700px;margin:0 auto 16px;line-height:1.6}
.ind-testimonial cite{color:var(--accent);font-size:.9rem;font-style:normal}

/* CTA */
.ind-cta{padding:80px 20px;text-align:center;background:linear-gradient(135deg,var(--bg-to),var(--bg-from))}
.ind-cta h2{font-size:2.2rem;margin-bottom:16px;color:#fff}
.ind-cta p{color:rgba(255,255,255,.6);margin-bottom:32px;font-size:1.1rem}
.ind-cta-buttons{display:flex;gap:16px;justify-content:center;flex-wrap:wrap}
.btn-cta{display:inline-flex;align-items:center;gap:8px;padding:14px 28px;border-radius:8px;font-size:1rem;font-weight:600;text-decoration:none!important;transition:all .3s}
.btn-cta-primary{background:var(--primary);color:#fff;box-shadow:0 4px 20px rgba(0,0,0,.3)}
.btn-cta-primary:hover{transform:translateY(-2px);box-shadow:0 8px 30px rgba(0,0,0,.4)}
.btn-cta-secondary{background:transparent;color:#fff;border:2px solid rgba(255,255,255,.3)}
.btn-cta-secondary:hover{border-color:var(--accent);color:var(--accent)}
.btn-cta-demo{background:rgba(255,255,255,.1);color:var(--accent);border:1px solid var(--accent)}
.btn-cta-demo:hover{background:var(--accent);color:#000}

/* Footer */
.ind-footer{padding:30px 20px;text-align:center;color:rgba(255,255,255,.4);font-size:.85rem;border-top:1px solid rgba(255,255,255,.05)}
.ind-footer a{color:var(--accent)}

/* Animations */
@keyframes heroFadeIn{from{opacity:0;transform:translateY(30px)}to{opacity:1;transform:translateY(0)}}
@keyframes fadeInUp{from{opacity:0;transform:translateY(20px)}to{opacity:1;transform:translateY(0)}}
@keyframes pulse{0%,100%{transform:scale(1)}50%{transform:scale(1.05)}}
@keyframes float{0%,100%{transform:translateY(0) translateX(0)}25%{transform:translateY(-20px) translateX(10px)}50%{transform:translateY(-10px) translateX(-10px)}75%{transform:translateY(-30px) translateX(5px)}}
@keyframes shimmer{0%{background-position:-200% 0}100%{background-position:200% 0}}

/* Responsive */
@media(max-width:768px){
.ind-stats{gap:20px;padding:40px 20px}
.ind-stat h3{font-size:1.8rem}
.ind-features,.ind-subs,.ind-products{padding:50px 16px}
.ind-hero h1{font-size:2.2rem}
}
</style>
</head>
<body>

<!-- Hero -->
<section class="ind-hero">
<div class="particles">
<?php for($i=0;$i<20;$i++): $size=rand(3,12); ?>
<div class="particle" style="width:<?php echo $size;?>px;height:<?php echo $size;?>px;left:<?php echo rand(0,100);?>%;top:<?php echo rand(0,100);?>%;animation-delay:<?php echo rand(-10,0);?>s"></div>
<?php endfor; ?>
</div>
<div class="ind-hero-content">
<div class="hero-icon"><i class="fa <?php echo htmlspecialchars($icon);?>"></i></div>
<h1><?php echo htmlspecialchars($name);?></h1>
<p class="tagline"><?php echo htmlspecialchars($tagline);?></p>
<p class="desc"><?php echo htmlspecialchars($desc);?></p>
<div class="ind-cta-buttons">
<a href="/cp/" class="btn-cta btn-cta-primary"><i class="fa fa-dashboard"></i> <?php echo htmlspecialchars($ctaCp);?></a>
<a href="/cp/demo/<?php echo htmlspecialchars($industryData['demo_key'] ?? 'demo');?>/shop/finance/erp" class="btn-cta btn-cta-secondary"><i class="fa fa-cogs"></i> <?php echo htmlspecialchars($ctaErp);?></a>
</div>
</div>
</section>

<!-- Stats -->
<?php if(!empty($stats)): ?>
<section class="ind-stats">
<?php foreach($stats as $stat): ?>
<div class="ind-stat">
<h3><?php echo htmlspecialchars($stat['value']);?></h3>
<p><?php echo htmlspecialchars($stat['label']);?></p>
</div>
<?php endforeach; ?>
</section>
<?php endif; ?>

<!-- Features -->
<?php if(!empty($features)): ?>
<section class="ind-features">
<h2>Industry-Specific ERP Features</h2>
<div class="ind-features-grid">
<?php foreach($features as $feat): ?>
<div class="ind-feature-card">
<div class="feat-icon"><i class="fa <?php echo htmlspecialchars($feat['icon'] ?? 'fa-check');?>"></i></div>
<h4><?php echo htmlspecialchars($feat['title']);?></h4>
<p><?php echo htmlspecialchars($feat['desc']);?></p>
</div>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- Sample Products/Services -->
<?php if(!empty($products)): ?>
<section class="ind-products">
<h2>Sample <?php echo (($industryData['product_label'] ?? '') ?: 'Products & Services');?></h2>
<div class="ind-products-grid">
<?php foreach($products as $prod): ?>
<div class="ind-product">
<div class="prod-icon"><i class="fa <?php echo htmlspecialchars($prod['icon'] ?? 'fa-cube');?>"></i></div>
<div class="prod-name"><?php echo htmlspecialchars($prod['name']);?></div>
<div class="prod-price"><?php echo htmlspecialchars($prod['price'] ?? '');?></div>
<div class="prod-cat"><?php echo htmlspecialchars($prod['category'] ?? '');?></div>
</div>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- Sub-industries -->
<?php if(!empty($subIndustries)): ?>
<section class="ind-subs">
<h2>Supported Sub-Industries</h2>
<div class="ind-subs-list">
<?php foreach($subIndustries as $sub): ?>
<span class="ind-sub-tag"><?php echo htmlspecialchars($sub);?></span>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- Testimonial -->
<?php if($testimonial): ?>
<section class="ind-testimonial">
<blockquote>&ldquo;<?php echo htmlspecialchars($testimonial['quote']);?>&rdquo;</blockquote>
<cite>— <?php echo htmlspecialchars($testimonial['author']);?></cite>
</section>
<?php endif; ?>

<!-- CTA -->
<section class="ind-cta">
<h2>Ready to Transform Your <?php echo htmlspecialchars($name);?> Business?</h2>
<p>Start with a free demo — no credit card required. Full ERP + CP access.</p>
<div class="ind-cta-buttons">
<a href="/platform/industries" class="btn-cta btn-cta-primary"><i class="fa fa-rocket"></i> Start Free Demo</a>
<a href="/cp/" class="btn-cta btn-cta-secondary"><i class="fa fa-sign-in"></i> Login to CP</a>
<a href="/platform/industries" class="btn-cta btn-cta-demo"><i class="fa fa-eye"></i> View All Industries</a>
</div>
</section>

<!-- Footer -->
<footer class="ind-footer">
<p>&copy; <?php echo date('Y');?> <a href="https://www.ecomae.com">ecomae</a> — Enterprise Business Operating System. All rights reserved.</p>
<p style="margin-top:8px;">Demo credentials: <strong>demo@ecomae.com</strong> / <strong>demo2026</strong></p>
</footer>

</body>
</html>
