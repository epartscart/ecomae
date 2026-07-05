<?php
/**
 * Industry Frontend Base Template — World-Class Storefront
 *
 * Full e-commerce storefront with header, cart, customer registration/login,
 * product catalog, and professional footer. Shared by all 28 industry groups.
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
$heroPhoto = $industryData['hero_photo'] ?? '';
$features = $industryData['features'] ?? [];
$products = $industryData['sample_products'] ?? [];
$subIndustries = $industryData['sub_industries'] ?? [];
$stats = $industryData['stats'] ?? [];
$testimonial = $industryData['testimonial'] ?? null;
$ctaCp = $industryData['cta_cp_text'] ?? 'Open Control Panel';
$ctaErp = $industryData['cta_erp_text'] ?? 'Launch ERP';
$galleryPhotos = $industryData['gallery_photos'] ?? [];
$demoKey = $industryData['demo_key'] ?? 'demo';
$navItems = $industryData['nav_items'] ?? array('Products','Features','About','Contact');
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title><?php echo htmlspecialchars($name); ?> — ERP, Control Panel & Storefront | ecomae Platform</title>
<meta name="description" content="<?php echo htmlspecialchars($name); ?> industry solutions: <?php echo htmlspecialchars($desc); ?> — <?php echo count($subIndustries);?> specialized sub-industries with dedicated ERP workflows, control panel, and storefront. Powered by ecomae.">
<meta name="keywords" content="<?php echo htmlspecialchars(strtolower($name));?>, ERP, control panel, storefront, <?php echo htmlspecialchars(implode(', ', array_slice($subIndustries, 0, 10)));?>, business management software, ecomae">
<meta name="robots" content="index, follow">
<link rel="canonical" href="https://<?php echo htmlspecialchars($demoKey);?>.ecomae.com/">
<!-- Open Graph -->
<meta property="og:type" content="website">
<meta property="og:title" content="<?php echo htmlspecialchars($name);?> — ecomae ERP Platform">
<meta property="og:description" content="<?php echo htmlspecialchars($desc);?> — <?php echo count($subIndustries);?> sub-industries covered.">
<meta property="og:image" content="<?php echo htmlspecialchars($heroPhoto);?>">
<meta property="og:url" content="https://<?php echo htmlspecialchars($demoKey);?>.ecomae.com/">
<meta property="og:site_name" content="ecomae">
<!-- Twitter Card -->
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="<?php echo htmlspecialchars($name);?> — ecomae Platform">
<meta name="twitter:description" content="<?php echo htmlspecialchars($desc);?>">
<meta name="twitter:image" content="<?php echo htmlspecialchars($heroPhoto);?>">
<!-- JSON-LD Structured Data -->
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "WebPage",
  "name": "<?php echo htmlspecialchars($name);?> — ecomae Platform",
  "description": "<?php echo addslashes($desc);?>",
  "url": "https://<?php echo htmlspecialchars($demoKey);?>.ecomae.com/",
  "publisher": {
    "@type": "Organization",
    "name": "ecomae",
    "url": "https://www.ecomae.com",
    "logo": "https://www.ecomae.com/images/ecomae-logo.png"
  },
  "mainEntity": {
    "@type": "ItemList",
    "name": "<?php echo htmlspecialchars($name);?> Sub-Industries",
    "numberOfItems": <?php echo count($subIndustries);?>,
    "itemListElement": [
<?php foreach(array_slice($subIndustries, 0, 15) as $idx => $si): ?>
      {"@type": "ListItem", "position": <?php echo $idx+1;?>, "name": "<?php echo htmlspecialchars($si);?>"}<?php echo $idx < min(14, count($subIndustries)-1) ? ',' : '';?>

<?php endforeach; ?>
    ]
  },
  "breadcrumb": {
    "@type": "BreadcrumbList",
    "itemListElement": [
      {"@type": "ListItem", "position": 1, "name": "ecomae", "item": "https://www.ecomae.com"},
      {"@type": "ListItem", "position": 2, "name": "Industries", "item": "https://www.ecomae.com/platform/industries"},
      {"@type": "ListItem", "position": 3, "name": "<?php echo htmlspecialchars($name);?>", "item": "https://<?php echo htmlspecialchars($demoKey);?>.ecomae.com/"}
    ]
  }
}
</script>
<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">
<style>
*{margin:0;padding:0;box-sizing:border-box}
:root{--primary:<?php echo $primary;?>;--accent:<?php echo $accent;?>;--bg-from:<?php echo $bgFrom;?>;--bg-to:<?php echo $bgTo;?>}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#1e293b;background:#fff;overflow-x:hidden}
a{color:var(--primary);text-decoration:none}
a:hover{text-decoration:underline}

/* ===== HEADER ===== */
.site-header{position:fixed;top:0;left:0;right:0;z-index:1000;background:rgba(255,255,255,.97);backdrop-filter:blur(12px);border-bottom:1px solid rgba(0,0,0,.06);transition:all .3s}
.site-header.scrolled{box-shadow:0 2px 20px rgba(0,0,0,.08)}
.header-inner{max-width:1280px;margin:0 auto;padding:0 24px;height:64px;display:flex;align-items:center;gap:20px}
.header-logo{display:flex;align-items:center;gap:10px;font-weight:700;font-size:18px;color:#0f172a;text-decoration:none!important}
.header-logo .logo-icon{width:36px;height:36px;border-radius:8px;background:linear-gradient(135deg,var(--primary),var(--accent));display:flex;align-items:center;justify-content:center;color:#fff;font-size:15px}
.header-nav{display:flex;gap:4px;margin-left:auto}
.header-nav a{padding:8px 16px;border-radius:8px;font-size:14px;font-weight:500;color:#475569;transition:all .2s;text-decoration:none!important}
.header-nav a:hover{background:#f1f5f9;color:var(--primary)}
.header-actions{display:flex;align-items:center;gap:12px;margin-left:16px}
.header-cart{position:relative;width:40px;height:40px;border-radius:10px;background:#f8fafc;display:flex;align-items:center;justify-content:center;cursor:pointer;transition:all .2s;border:1px solid #e2e8f0}
.header-cart:hover{background:var(--primary);color:#fff;border-color:var(--primary)}
.header-cart .cart-count{position:absolute;top:-4px;right:-4px;width:18px;height:18px;border-radius:50%;background:#ef4444;color:#fff;font-size:10px;font-weight:700;display:flex;align-items:center;justify-content:center;transform:scale(0);transition:transform .3s}
.header-cart .cart-count.show{transform:scale(1)}
.header-btn{padding:8px 18px;border-radius:8px;font-size:13px;font-weight:600;cursor:pointer;transition:all .2s;border:none;text-decoration:none!important}
.header-btn--login{background:#f1f5f9;color:#475569}
.header-btn--login:hover{background:#e2e8f0}
.header-btn--register{background:var(--primary);color:#fff}
.header-btn--register:hover{background:var(--accent);transform:translateY(-1px)}
.header-mobile{display:none;width:40px;height:40px;border-radius:8px;background:#f8fafc;border:1px solid #e2e8f0;align-items:center;justify-content:center;cursor:pointer;font-size:18px}
@media(max-width:768px){
.header-nav{display:none}
.header-mobile{display:flex}
}

/* ===== MINI CART DROPDOWN ===== */
.mini-cart{position:fixed;top:0;right:-400px;width:380px;max-width:90vw;height:100vh;background:#fff;box-shadow:-5px 0 30px rgba(0,0,0,.15);z-index:2000;transition:right .4s cubic-bezier(.16,1,.3,1);display:flex;flex-direction:column}
.mini-cart.open{right:0}
.mini-cart__header{padding:20px 24px;border-bottom:1px solid #e2e8f0;display:flex;align-items:center;justify-content:space-between}
.mini-cart__header h3{font-size:18px;font-weight:700;color:#0f172a}
.mini-cart__close{width:32px;height:32px;border-radius:8px;background:#f1f5f9;display:flex;align-items:center;justify-content:center;cursor:pointer;font-size:16px;color:#475569;border:none}
.mini-cart__items{flex:1;overflow-y:auto;padding:16px 24px}
.mini-cart__empty{text-align:center;padding:60px 20px;color:#94a3b8}
.mini-cart__empty i{font-size:48px;margin-bottom:12px;opacity:.4}
.mini-cart__item{display:flex;gap:12px;padding:14px 0;border-bottom:1px solid #f1f5f9;align-items:center}
.mini-cart__item-img{width:56px;height:56px;border-radius:8px;background:#f8fafc;display:flex;align-items:center;justify-content:center;font-size:20px;color:var(--primary);flex-shrink:0;overflow:hidden}
.mini-cart__item-img img{width:100%;height:100%;object-fit:cover}
.mini-cart__item-info{flex:1}
.mini-cart__item-name{font-size:13px;font-weight:600;color:#1e293b}
.mini-cart__item-price{font-size:14px;font-weight:700;color:var(--primary);margin-top:2px}
.mini-cart__item-qty{display:flex;align-items:center;gap:8px;margin-top:6px}
.mini-cart__item-qty button{width:24px;height:24px;border-radius:6px;border:1px solid #e2e8f0;background:#f8fafc;cursor:pointer;font-size:12px;display:flex;align-items:center;justify-content:center}
.mini-cart__item-qty span{font-size:13px;font-weight:600;min-width:20px;text-align:center}
.mini-cart__item-remove{color:#ef4444;cursor:pointer;font-size:14px;padding:4px}
.mini-cart__footer{padding:20px 24px;border-top:1px solid #e2e8f0;background:#f8fafc}
.mini-cart__total{display:flex;justify-content:space-between;align-items:center;margin-bottom:14px}
.mini-cart__total span{font-size:14px;color:#64748b}
.mini-cart__total strong{font-size:20px;color:#0f172a}
.mini-cart__checkout{width:100%;padding:14px;border-radius:10px;background:var(--primary);color:#fff;font-size:14px;font-weight:600;border:none;cursor:pointer;transition:all .2s}
.mini-cart__checkout:hover{background:var(--accent);transform:translateY(-1px)}
.cart-overlay{position:fixed;inset:0;background:rgba(0,0,0,.4);z-index:1999;opacity:0;pointer-events:none;transition:opacity .3s}
.cart-overlay.open{opacity:1;pointer-events:auto}

/* ===== AUTH MODAL ===== */
.auth-modal{position:fixed;inset:0;z-index:3000;display:none;align-items:center;justify-content:center;padding:20px}
.auth-modal.open{display:flex}
.auth-modal__bg{position:absolute;inset:0;background:rgba(0,0,0,.5);backdrop-filter:blur(4px)}
.auth-modal__box{position:relative;width:100%;max-width:440px;background:#fff;border-radius:20px;padding:40px;box-shadow:0 25px 60px rgba(0,0,0,.2);animation:modalIn .4s cubic-bezier(.16,1,.3,1)}
@keyframes modalIn{from{opacity:0;transform:translateY(20px) scale(.95)}to{opacity:1;transform:translateY(0) scale(1)}}
.auth-modal__close{position:absolute;top:16px;right:16px;width:32px;height:32px;border-radius:8px;background:#f1f5f9;border:none;cursor:pointer;font-size:16px;display:flex;align-items:center;justify-content:center}
.auth-modal__tabs{display:flex;gap:4px;margin-bottom:28px;background:#f1f5f9;border-radius:10px;padding:4px}
.auth-modal__tab{flex:1;padding:10px;text-align:center;border-radius:8px;font-size:14px;font-weight:600;cursor:pointer;transition:all .2s;color:#64748b}
.auth-modal__tab.active{background:#fff;color:var(--primary);box-shadow:0 2px 8px rgba(0,0,0,.06)}
.auth-modal__form{display:none}
.auth-modal__form.active{display:block}
.auth-form-group{margin-bottom:16px}
.auth-form-group label{display:block;font-size:12px;font-weight:600;color:#475569;margin-bottom:6px;text-transform:uppercase;letter-spacing:.5px}
.auth-form-group input,.auth-form-group select{width:100%;padding:12px 14px;border:2px solid #e2e8f0;border-radius:10px;font-size:14px;transition:border-color .2s;outline:none}
.auth-form-group input:focus,.auth-form-group select:focus{border-color:var(--primary)}
.auth-form-submit{width:100%;padding:14px;border-radius:10px;background:var(--primary);color:#fff;font-size:15px;font-weight:600;border:none;cursor:pointer;margin-top:8px;transition:all .2s}
.auth-form-submit:hover{background:var(--accent);transform:translateY(-1px)}
.auth-form-footer{text-align:center;margin-top:16px;font-size:13px;color:#64748b}
.auth-form-footer a{color:var(--primary);font-weight:600}
.auth-form-note{font-size:12px;color:#94a3b8;text-align:center;margin-top:12px}
.auth-industry{display:flex;align-items:center;gap:10px;padding:12px;background:#f8fafc;border-radius:10px;margin-bottom:20px;border:1px solid #e2e8f0}
.auth-industry .ai-icon{width:32px;height:32px;border-radius:6px;background:var(--primary);display:flex;align-items:center;justify-content:center;color:#fff;font-size:13px}
.auth-industry .ai-text{font-size:12px;color:#475569}
.auth-industry .ai-text strong{display:block;color:#0f172a;font-size:13px}

/* ===== HERO ===== */
.ind-hero{min-height:90vh;display:flex;align-items:center;justify-content:center;text-align:center;position:relative;overflow:hidden;padding-top:64px}
.ind-hero-video{position:absolute;inset:0;pointer-events:none;overflow:hidden;z-index:1}
.ind-hero-video iframe{position:absolute;top:50%;left:50%;width:180vw;height:180vh;transform:translate(-50%,-50%);border:0;object-fit:cover}
.ind-hero-video.video-error{display:none}
.ind-hero-bg{position:absolute;inset:0;background-size:cover;background-position:center;animation:slowZoom 20s ease-in-out infinite alternate}
.ind-hero-overlay{position:absolute;inset:0;background:linear-gradient(135deg,rgba(<?php
$r=hexdec(substr($bgFrom,1,2));$g=hexdec(substr($bgFrom,3,2));$b=hexdec(substr($bgFrom,5,2));
echo "$r,$g,$b";?>,.55),rgba(<?php
$r2=hexdec(substr($bgTo,1,2));$g2=hexdec(substr($bgTo,3,2));$b2=hexdec(substr($bgTo,5,2));
echo "$r2,$g2,$b2";?>,.45))}
.ind-hero-content{position:relative;z-index:2;max-width:800px;padding:40px 20px;animation:heroFadeIn 1.2s cubic-bezier(.16,1,.3,1)}
.ind-hero h1{font-size:clamp(2.5rem,6vw,4.5rem);font-weight:800;background:linear-gradient(135deg,#fff 30%,var(--accent));-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;margin-bottom:16px;line-height:1.1}
.ind-hero .tagline{font-size:1.3rem;color:rgba(255,255,255,.85);margin-bottom:20px;font-weight:300}
.ind-hero .desc{font-size:1rem;color:rgba(255,255,255,.7);max-width:600px;margin:0 auto 32px;line-height:1.7}
.ind-hero .hero-icon{font-size:3.5rem;color:var(--accent);margin-bottom:16px;animation:iconFloat 3s ease-in-out infinite;filter:drop-shadow(0 0 20px var(--accent))}
.particles{position:absolute;inset:0;overflow:hidden;z-index:1}
.particle{position:absolute;border-radius:50%;background:var(--accent);opacity:.12;animation:float 8s infinite}
.particle:nth-child(2n){animation-duration:12s;opacity:.08}
.particle:nth-child(3n){animation-duration:10s;opacity:.15}
.ind-hero-ctas{display:flex;gap:14px;justify-content:center;flex-wrap:wrap}
.btn-hero{display:inline-flex;align-items:center;gap:8px;padding:14px 28px;border-radius:10px;font-size:15px;font-weight:600;text-decoration:none!important;transition:all .3s}
.btn-hero--primary{background:#fff;color:var(--primary);box-shadow:0 4px 20px rgba(0,0,0,.15)}
.btn-hero--primary:hover{transform:translateY(-3px);box-shadow:0 8px 30px rgba(0,0,0,.2)}
.btn-hero--secondary{background:rgba(255,255,255,.1);color:#fff;border:2px solid rgba(255,255,255,.3)}
.btn-hero--secondary:hover{border-color:#fff;transform:translateY(-2px)}

/* ===== ABOUT SECTION ===== */
.ind-about{padding:80px 20px;background:#fff}
.ind-about-inner{max-width:1200px;margin:0 auto;display:grid;grid-template-columns:1fr 1fr;gap:48px;align-items:center}
.ind-about-text h2{font-size:2.2rem;font-weight:800;color:#0f172a;margin-bottom:16px}
.ind-about-text p{font-size:1rem;color:#475569;line-height:1.8;margin-bottom:16px}
.ind-about-text .about-highlights{list-style:none;padding:0;margin:20px 0 0}
.ind-about-text .about-highlights li{padding:8px 0 8px 28px;position:relative;font-size:14px;color:#334155}
.ind-about-text .about-highlights li::before{content:'\f00c';font-family:FontAwesome;position:absolute;left:0;top:8px;color:var(--primary);font-size:14px}
.ind-about-media{position:relative;border-radius:16px;overflow:hidden;box-shadow:0 20px 60px rgba(0,0,0,.12)}
.ind-about-media img{width:100%;height:auto;display:block;border-radius:16px}
.ind-about-media .video-wrapper{position:relative;padding-bottom:56.25%;height:0;border-radius:16px;overflow:hidden}
.ind-about-media .video-wrapper iframe{position:absolute;top:0;left:0;width:100%;height:100%;border:0;border-radius:16px}
.ind-about-media .video-play-overlay{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;background:rgba(0,0,0,.25);cursor:pointer;transition:background .3s;border-radius:16px}
.ind-about-media .video-play-overlay:hover{background:rgba(0,0,0,.15)}
.ind-about-media .play-btn{width:72px;height:72px;border-radius:50%;background:rgba(255,255,255,.95);display:flex;align-items:center;justify-content:center;box-shadow:0 4px 20px rgba(0,0,0,.2)}
.ind-about-media .play-btn i{font-size:28px;color:var(--primary);margin-left:4px}
@media(max-width:768px){
.ind-about-inner{grid-template-columns:1fr;gap:32px}
}

/* ===== STATS ===== */
.ind-stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:0;background:#fff;border-bottom:1px solid #e2e8f0}
.ind-stat{text-align:center;padding:36px 20px;border-right:1px solid #f1f5f9}
.ind-stat:last-child{border-right:0}
.ind-stat h3{font-size:2rem;font-weight:800;color:var(--primary)}
.ind-stat p{font-size:.8rem;color:#64748b;margin-top:4px;text-transform:uppercase;letter-spacing:1px}

/* ===== PRODUCT CATEGORIES ===== */
.ind-categories{padding:80px 20px;background:#fff}
.ind-categories h2{text-align:center;font-size:2rem;font-weight:800;color:#0f172a;margin-bottom:12px}
.ind-categories .section-sub{text-align:center;color:#64748b;font-size:15px;margin-bottom:40px}
.ind-categories-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:24px;max-width:1280px;margin:0 auto}
.cat-group{background:#f8fafc;border-radius:16px;border:1px solid #e2e8f0;padding:24px;transition:all .3s}
.cat-group:hover{transform:translateY(-3px);box-shadow:0 8px 24px rgba(0,0,0,.06);border-color:var(--primary)}
.cat-group__header{display:flex;align-items:center;gap:12px;margin-bottom:16px;padding-bottom:14px;border-bottom:2px solid #e2e8f0}
.cat-group__icon{width:40px;height:40px;border-radius:10px;background:linear-gradient(135deg,var(--primary),var(--accent));display:flex;align-items:center;justify-content:center;color:#fff;font-size:16px;flex-shrink:0}
.cat-group__title{font-size:15px;font-weight:700;color:#0f172a}
.cat-group__count{font-size:11px;color:#64748b;margin-top:2px}
.cat-group__list{list-style:none;padding:0;margin:0}
.cat-group__list li{padding:8px 12px;margin-bottom:4px;border-radius:8px;font-size:13px;color:#334155;cursor:pointer;transition:all .2s;display:flex;align-items:center;gap:8px}
.cat-group__list li:hover{background:var(--primary);color:#fff;transform:translateX(4px)}
.cat-group__list li::before{content:'\f105';font-family:FontAwesome;font-size:11px;color:var(--primary);transition:color .2s}
.cat-group__list li:hover::before{color:#fff}
.cat-group__list li .cat-badge{margin-left:auto;padding:2px 7px;background:#e2e8f0;border-radius:10px;font-size:10px;font-weight:600;color:#64748b}
.cat-group__list li:hover .cat-badge{background:rgba(255,255,255,.2);color:#fff}
@media(max-width:768px){
.ind-categories-grid{grid-template-columns:1fr}
}

/* ===== PRODUCTS ===== */
.ind-products{padding:80px 20px;background:#f8fafc}
.ind-products h2{text-align:center;font-size:2rem;font-weight:800;color:#0f172a;margin-bottom:12px}
.ind-products .section-sub{text-align:center;color:#64748b;font-size:15px;margin-bottom:40px}
.ind-products-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:20px;max-width:1200px;margin:0 auto}
.product-card{background:#fff;border-radius:14px;border:1px solid #e2e8f0;overflow:hidden;transition:all .3s cubic-bezier(.16,1,.3,1)}
.product-card:hover{transform:translateY(-4px);box-shadow:0 12px 30px rgba(0,0,0,.08);border-color:transparent}
.product-card__img{height:180px;background:#f1f5f9;display:flex;align-items:center;justify-content:center;overflow:hidden;position:relative}
.product-card__img img{width:100%;height:100%;object-fit:cover;transition:transform .5s}
.product-card:hover .product-card__img img{transform:scale(1.06)}
.product-card__img .prod-icon-lg{font-size:3rem;color:var(--primary);opacity:.5}
.product-card__cat{position:absolute;top:10px;left:10px;padding:4px 10px;background:rgba(255,255,255,.9);border-radius:6px;font-size:11px;font-weight:600;color:#475569}
.product-card__body{padding:18px}
.product-card__name{font-size:15px;font-weight:600;color:#1e293b;margin-bottom:6px}
.product-card__price{font-size:18px;font-weight:700;color:var(--primary);margin-bottom:12px}
.product-card__add{width:100%;padding:10px;border-radius:8px;background:var(--primary);color:#fff;font-size:13px;font-weight:600;border:none;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:6px;transition:all .2s}
.product-card__add:hover{background:var(--accent);transform:translateY(-1px)}
.product-card__add.added{background:#10b981}

/* ===== FEATURES ===== */
.ind-features{padding:80px 20px;max-width:1200px;margin:0 auto}
.ind-features h2{text-align:center;font-size:2rem;font-weight:800;color:#0f172a;margin-bottom:12px}
.ind-features .section-sub{text-align:center;color:#64748b;font-size:15px;margin-bottom:40px}
.ind-features-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}
.feat-card{padding:28px;border-radius:14px;border:1px solid #e2e8f0;background:#fff;transition:all .3s;position:relative;overflow:hidden}
.feat-card::before{content:'';position:absolute;top:0;left:0;right:0;height:3px;background:linear-gradient(90deg,var(--primary),var(--accent));transform:scaleX(0);transition:transform .3s;transform-origin:left}
.feat-card:hover{transform:translateY(-4px);box-shadow:0 10px 30px rgba(0,0,0,.06);border-color:transparent}
.feat-card:hover::before{transform:scaleX(1)}
.feat-card .feat-icon{width:44px;height:44px;border-radius:10px;background:linear-gradient(135deg,var(--primary),var(--accent));display:flex;align-items:center;justify-content:center;color:#fff;font-size:18px;margin-bottom:14px}
.feat-card h4{font-size:16px;font-weight:700;color:#0f172a;margin-bottom:8px}
.feat-card p{font-size:13px;color:#64748b;line-height:1.6}

/* ===== SUB-INDUSTRIES ===== */
.ind-subs{padding:80px 20px;background:linear-gradient(180deg,#f8fafc 0%,#fff 100%)}
.ind-subs h2{text-align:center;font-size:2rem;font-weight:800;color:#0f172a;margin-bottom:12px}
.ind-subs .section-sub{text-align:center;color:#64748b;font-size:15px;margin-bottom:40px}
.ind-subs-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:24px;max-width:1280px;margin:0 auto}
.sub-card{background:#fff;border-radius:16px;border:1px solid #e2e8f0;overflow:hidden;transition:all .4s cubic-bezier(.16,1,.3,1);cursor:pointer;position:relative}
.sub-card:hover{transform:translateY(-6px);box-shadow:0 20px 40px rgba(0,0,0,.1);border-color:transparent}
.sub-card__img{height:160px;overflow:hidden;position:relative}
.sub-card__img img{width:100%;height:100%;object-fit:cover;transition:transform .6s}
.sub-card:hover .sub-card__img img{transform:scale(1.08)}
.sub-card__img::after{content:'';position:absolute;bottom:0;left:0;right:0;height:60px;background:linear-gradient(transparent,rgba(0,0,0,.4))}
.sub-card__badge{position:absolute;top:12px;right:12px;display:flex;gap:4px}
.sub-card__badge span{padding:3px 8px;border-radius:4px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.5px}
.sub-card__badge .b-erp{background:rgba(16,185,129,.9);color:#fff}
.sub-card__badge .b-cp{background:rgba(59,130,246,.9);color:#fff}
.sub-card__badge .b-store{background:rgba(249,115,22,.9);color:#fff}
.sub-card__body{padding:20px}
.sub-card__name{font-size:16px;font-weight:700;color:#0f172a;margin-bottom:6px}
.sub-card__desc{font-size:13px;color:#64748b;margin-bottom:12px;line-height:1.6}
.sub-card__cats{display:flex;flex-wrap:wrap;gap:5px;margin-bottom:12px}
.cat-tag{display:inline-block;padding:3px 8px;background:#f1f5f9;border:1px solid #e2e8f0;border-radius:4px;font-size:11px;color:#475569;white-space:nowrap;transition:all .2s}
.cat-tag:hover{background:var(--primary);color:#fff;border-color:var(--primary)}
.sub-card__process{display:flex;align-items:center;gap:6px;flex-wrap:wrap;margin-bottom:14px;padding:10px 12px;background:#f8fafc;border-radius:8px;border:1px solid #f1f5f9}
.sub-card__process .proc-step{font-size:11px;color:#475569;font-weight:500;white-space:nowrap}
.sub-card__process .proc-arrow{color:var(--primary);font-size:10px}
.sub-card__products{display:grid;grid-template-columns:repeat(3,1fr);gap:8px}
.sub-prod{text-align:center;border-radius:8px;overflow:hidden;border:1px solid #f1f5f9;transition:all .2s}
.sub-prod:hover{border-color:var(--primary);transform:scale(1.03)}
.sub-prod img{width:100%;height:60px;object-fit:cover}
.sub-prod .sp-name{font-size:10px;font-weight:600;color:#1e293b;padding:4px 2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.sub-prod .sp-price{font-size:10px;color:var(--primary);font-weight:700;padding:0 2px 4px}
.sub-card__cta{display:flex;gap:8px;margin-top:14px}
.sub-card__cta a{flex:1;padding:8px;border-radius:6px;font-size:11px;font-weight:600;text-align:center;text-decoration:none!important;transition:all .2s}
.sub-card__cta .sc-site{background:var(--primary);color:#fff}
.sub-card__cta .sc-site:hover{background:var(--accent)}
.sub-card__cta .sc-erp{background:#f1f5f9;color:#475569}
.sub-card__cta .sc-erp:hover{background:#e2e8f0}

/* Sub-industry filter tabs */
.sub-filter{display:flex;gap:8px;justify-content:center;flex-wrap:wrap;margin-bottom:32px;max-width:900px;margin-left:auto;margin-right:auto}
.sub-filter-btn{padding:8px 16px;border-radius:100px;border:1px solid #e2e8f0;background:#fff;font-size:13px;font-weight:500;color:#475569;cursor:pointer;transition:all .2s}
.sub-filter-btn:hover,.sub-filter-btn.active{background:var(--primary);border-color:var(--primary);color:#fff;transform:scale(1.05)}

/* ===== GALLERY ===== */
.ind-gallery{padding:80px 20px;max-width:1200px;margin:0 auto}
.ind-gallery h2{text-align:center;font-size:2rem;font-weight:800;color:#0f172a;margin-bottom:40px}
.ind-gallery-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;grid-auto-rows:200px}
.ind-gallery-item{border-radius:12px;overflow:hidden;position:relative;transition:all .4s cubic-bezier(.16,1,.3,1)}
.ind-gallery-item:hover{transform:translateY(-4px);box-shadow:0 16px 40px rgba(0,0,0,.15)}
.ind-gallery-item img{width:100%;height:100%;object-fit:cover;transition:transform .6s}
.ind-gallery-item:hover img{transform:scale(1.08)}
.ind-gallery-item:first-child{grid-row:span 2}
.ind-gallery-item::after{content:'';position:absolute;inset:0;background:linear-gradient(to top,rgba(0,0,0,.3) 0%,transparent 50%);opacity:0;transition:opacity .3s}
.ind-gallery-item:hover::after{opacity:1}

/* ===== TESTIMONIAL ===== */
.ind-testimonial{padding:80px 40px;text-align:center;background:#fff;border-top:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0}
.ind-testimonial blockquote{font-size:1.2rem;font-style:italic;color:#374151;max-width:700px;margin:0 auto 16px;line-height:1.7}
.ind-testimonial cite{color:var(--primary);font-size:.9rem;font-style:normal;font-weight:600}

/* ===== FOOTER ===== */
.site-footer{background:#0f172a;color:#94a3b8;padding:60px 24px 30px}
.footer-inner{max-width:1200px;margin:0 auto;display:grid;grid-template-columns:2fr 1fr 1fr 1fr;gap:40px}
.footer-brand h3{color:#fff;font-size:18px;margin-bottom:12px;display:flex;align-items:center;gap:8px}
.footer-brand p{font-size:13px;line-height:1.7;margin-bottom:16px}
.footer-social{display:flex;gap:10px}
.footer-social a{width:36px;height:36px;border-radius:8px;background:rgba(255,255,255,.06);display:flex;align-items:center;justify-content:center;color:#94a3b8;transition:all .2s;text-decoration:none!important}
.footer-social a:hover{background:var(--primary);color:#fff;transform:translateY(-2px)}
.footer-col h4{color:#fff;font-size:14px;font-weight:700;margin-bottom:16px}
.footer-col ul{list-style:none;padding:0}
.footer-col li{margin-bottom:10px}
.footer-col a{color:#94a3b8;font-size:13px;transition:color .2s;text-decoration:none!important}
.footer-col a:hover{color:var(--accent)}
.footer-newsletter{margin-top:12px}
.footer-newsletter input{width:100%;padding:10px 12px;border:1px solid rgba(255,255,255,.12);border-radius:8px;background:rgba(255,255,255,.04);color:#fff;font-size:13px;outline:none;margin-bottom:8px}
.footer-newsletter button{width:100%;padding:10px;border-radius:8px;background:var(--primary);color:#fff;font-size:13px;font-weight:600;border:none;cursor:pointer;transition:all .2s}
.footer-newsletter button:hover{background:var(--accent)}
.footer-bottom{max-width:1200px;margin:30px auto 0;padding-top:20px;border-top:1px solid rgba(255,255,255,.06);display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:12px}
.footer-bottom p{font-size:12px;color:#64748b}
.footer-bottom .demo-link{padding:6px 14px;background:rgba(255,255,255,.06);border-radius:6px;font-size:11px;color:#94a3b8;border:1px solid rgba(255,255,255,.08)}
.footer-bottom .demo-link code{color:var(--accent)}

/* ===== CTA SECTION ===== */
.ind-cta{padding:80px 20px;text-align:center;background:linear-gradient(135deg,var(--bg-from),var(--bg-to));position:relative;overflow:hidden}
.ind-cta::before{content:'';position:absolute;inset:0;background:radial-gradient(ellipse at center,rgba(255,255,255,.03),transparent 70%)}
.ind-cta h2{font-size:2rem;color:#fff;margin-bottom:12px;position:relative}
.ind-cta p{color:rgba(255,255,255,.7);margin-bottom:32px;font-size:1rem;position:relative}
.ind-cta-buttons{display:flex;gap:14px;justify-content:center;flex-wrap:wrap;position:relative}
.btn-cta{display:inline-flex;align-items:center;gap:8px;padding:14px 28px;border-radius:10px;font-size:15px;font-weight:600;text-decoration:none!important;transition:all .3s}
.btn-cta-primary{background:#fff;color:var(--primary)}
.btn-cta-primary:hover{transform:translateY(-3px);box-shadow:0 10px 30px rgba(0,0,0,.3)}
.btn-cta-secondary{background:transparent;color:#fff;border:2px solid rgba(255,255,255,.3)}
.btn-cta-secondary:hover{border-color:#fff;transform:translateY(-2px)}

/* ===== REVEAL ===== */
.reveal{opacity:0;transform:translateY(24px);transition:all .7s cubic-bezier(.16,1,.3,1)}
.reveal.visible{opacity:1;transform:translateY(0)}

/* ===== ANIMATIONS ===== */
@keyframes heroFadeIn{from{opacity:0;transform:translateY(30px)}to{opacity:1;transform:translateY(0)}}
@keyframes float{0%,100%{transform:translateY(0) translateX(0)}25%{transform:translateY(-20px) translateX(10px)}75%{transform:translateY(-30px) translateX(5px)}}
@keyframes slowZoom{from{transform:scale(1)}to{transform:scale(1.08)}}
@keyframes iconFloat{0%,100%{transform:translateY(0)}50%{transform:translateY(-8px)}}
@keyframes cartBounce{0%{transform:scale(1)}50%{transform:scale(1.3)}100%{transform:scale(1)}}

/* ===== RESPONSIVE ===== */
@media(max-width:768px){
.footer-inner{grid-template-columns:1fr}
.ind-gallery-grid{grid-template-columns:1fr 1fr;grid-auto-rows:150px}
.ind-gallery-item:first-child{grid-row:span 1}
.ind-stats{grid-template-columns:repeat(2,1fr)}
.ind-stat{border-right:0;border-bottom:1px solid #f1f5f9}
.ind-hero{min-height:80vh}
}
</style>
</head>
<body>

<!-- ===== HEADER ===== -->
<header class="site-header" id="siteHeader">
<div class="header-inner">
<a href="/" class="header-logo">
<div class="logo-icon"><i class="fa <?php echo htmlspecialchars($icon);?>"></i></div>
<?php echo htmlspecialchars($name);?>
</a>
<nav class="header-nav">
<a href="#products">Products</a>
<a href="#features">Features</a>
<a href="#industries">Industries</a>
<a href="#about">About</a>
</nav>
<div class="header-actions">
<div class="header-cart" id="cartBtn" onclick="openCart()">
<i class="fa fa-shopping-cart"></i>
<span class="cart-count" id="cartCount">0</span>
</div>
<button class="header-btn header-btn--login" onclick="openAuth('login')">Login</button>
<button class="header-btn header-btn--register" onclick="openAuth('register')">Register</button>
</div>
<div class="header-mobile" onclick="toggleMobileNav()"><i class="fa fa-bars"></i></div>
</div>
</header>

<!-- ===== MINI CART ===== -->
<div class="cart-overlay" id="cartOverlay" onclick="closeCart()"></div>
<div class="mini-cart" id="miniCart">
<div class="mini-cart__header">
<h3><i class="fa fa-shopping-cart"></i> Your Cart</h3>
<button class="mini-cart__close" onclick="closeCart()"><i class="fa fa-times"></i></button>
</div>
<div class="mini-cart__items" id="cartItems">
<div class="mini-cart__empty">
<i class="fa fa-shopping-bag"></i>
<p>Your cart is empty</p>
<p style="font-size:12px;margin-top:8px">Browse products below and add items to get started</p>
</div>
</div>
<div class="mini-cart__footer" id="cartFooter" style="display:none">
<div class="mini-cart__total">
<span>Total</span>
<strong id="cartTotal">AED 0</strong>
</div>
<button class="mini-cart__checkout" onclick="checkout()"><i class="fa fa-lock"></i> Proceed to Checkout</button>
</div>
</div>

<!-- ===== AUTH MODAL ===== -->
<div class="auth-modal" id="authModal">
<div class="auth-modal__bg" onclick="closeAuth()"></div>
<div class="auth-modal__box">
<button class="auth-modal__close" onclick="closeAuth()"><i class="fa fa-times"></i></button>
<div class="auth-industry">
<div class="ai-icon"><i class="fa <?php echo htmlspecialchars($icon);?>"></i></div>
<div class="ai-text"><strong><?php echo htmlspecialchars($name);?></strong>Your account will be configured for this industry</div>
</div>
<div class="auth-modal__tabs">
<div class="auth-modal__tab active" data-tab="login" onclick="switchAuthTab('login')">Login</div>
<div class="auth-modal__tab" data-tab="register" onclick="switchAuthTab('register')">Register</div>
</div>
<!-- Login Form -->
<form class="auth-modal__form active" id="formLogin" onsubmit="return handleLogin(event)">
<div class="auth-form-group">
<label>Email Address</label>
<input type="email" id="loginEmail" placeholder="your@email.com" required>
</div>
<div class="auth-form-group">
<label>Password</label>
<input type="password" id="loginPass" placeholder="Enter your password" required>
</div>
<button type="submit" class="auth-form-submit"><i class="fa fa-sign-in"></i> Login</button>
<p class="auth-form-footer">Don't have an account? <a href="#" onclick="switchAuthTab('register');return false">Register here</a></p>
<p class="auth-form-note">Demo: demo@ecomae.com / demo2026</p>
</form>
<!-- Register Form -->
<form class="auth-modal__form" id="formRegister" onsubmit="return handleRegister(event)">
<div class="auth-form-group">
<label>Full Name</label>
<input type="text" id="regName" placeholder="John Doe" required>
</div>
<div class="auth-form-group">
<label>Email Address</label>
<input type="email" id="regEmail" placeholder="your@email.com" required>
</div>
<div class="auth-form-group">
<label>Phone Number</label>
<input type="tel" id="regPhone" placeholder="+971 50 123 4567">
</div>
<div class="auth-form-group">
<label>Company Name</label>
<input type="text" id="regCompany" placeholder="Your business name">
</div>
<div class="auth-form-group">
<label>Industry</label>
<select id="regIndustry">
<option value="<?php echo htmlspecialchars($demoKey);?>"><?php echo htmlspecialchars($name);?> (current)</option>
<option value="automotive">Automotive & Vehicles</option>
<option value="healthcare">Healthcare & Medical</option>
<option value="food_beverage">Food & Beverage</option>
<option value="fashion">Fashion & Apparel</option>
<option value="jewellery">Jewellery & Luxury</option>
<option value="electronics">Electronics & Technology</option>
<option value="construction">Construction & Real Estate</option>
<option value="manufacturing">Manufacturing & Industrial</option>
<option value="professional">Professional Services</option>
<option value="education">Education & Training</option>
<option value="hospitality">Hospitality & Travel</option>
<option value="beauty">Beauty & Wellness</option>
<option value="retail">Retail & E-commerce</option>
<option value="agriculture">Agriculture & Farming</option>
<option value="logistics">Logistics & Transport</option>
<option value="energy">Energy & Utilities</option>
<option value="finance">Financial Services</option>
<option value="technology">IT & Software</option>
<option value="media">Media & Entertainment</option>
<option value="sports">Sports & Fitness</option>
<option value="homeliving">Home & Living</option>
<option value="wholesale">Wholesale & Trading</option>
<option value="rental">Rental & Leasing</option>
<option value="nonprofit">Non-Profit & Government</option>
<option value="cleaning">Cleaning & Maintenance</option>
<option value="pet">Pet & Animal Services</option>
<option value="printing">Printing & Signage</option>
<option value="security">Security & Safety</option>
</select>
</div>
<div class="auth-form-group">
<label>Password</label>
<input type="password" id="regPass" placeholder="Min 8 characters" required minlength="8">
</div>
<button type="submit" class="auth-form-submit"><i class="fa fa-user-plus"></i> Create Account</button>
<p class="auth-form-footer">Already have an account? <a href="#" onclick="switchAuthTab('login');return false">Login</a></p>
</form>
</div>
</div>

<!-- ===== HERO ===== -->
<?php
$heroVideo = $industryData['about_video'] ?? '';
$heroVideoUrl = '';
if($heroVideo) {
    // Normalize to youtube.com (more reliable than youtube-nocookie.com for autoplay)
    $heroVideo = str_replace('www.youtube-nocookie.com', 'www.youtube.com', $heroVideo);
    $sep = (strpos($heroVideo, '?') !== false) ? '&' : '?';
    $heroVideoUrl = $heroVideo . $sep . 'autoplay=1&mute=1&loop=1&controls=0&showinfo=0&rel=0&modestbranding=1&playsinline=1&enablejsapi=1&origin=https://www.ecomae.com';
    if(preg_match('/embed\/([a-zA-Z0-9_-]+)/', $heroVideoUrl, $vm)) {
        $heroVideoUrl .= '&playlist=' . $vm[1];
    }
}
?>
<section class="ind-hero" id="about">
<?php if($heroPhoto): ?>
<div class="ind-hero-bg" style="background-image:url('<?php echo htmlspecialchars($heroPhoto);?>')"></div>
<?php endif; ?>
<?php if($heroVideoUrl): ?>
<div class="ind-hero-video" id="heroVideoWrap">
<iframe id="heroVideoFrame" src="<?php echo htmlspecialchars($heroVideoUrl);?>" allow="autoplay; accelerometer; encrypted-media; gyroscope" frameborder="0"></iframe>
</div>
<?php endif; ?>
<div class="ind-hero-overlay"></div>
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
<div class="ind-hero-ctas">
<a href="#products" class="btn-hero btn-hero--primary"><i class="fa fa-shopping-bag"></i> Browse Products</a>
<a href="#" class="btn-hero btn-hero--secondary" onclick="openAuth('register');return false"><i class="fa fa-user-plus"></i> Start Free</a>
</div>
</div>
</section>

<!-- ===== STATS ===== -->
<?php if(!empty($stats)): ?>
<section class="ind-stats reveal">
<?php foreach($stats as $stat): ?>
<div class="ind-stat">
<h3><?php echo htmlspecialchars($stat['value']);?></h3>
<p><?php echo htmlspecialchars($stat['label']);?></p>
</div>
<?php endforeach; ?>
</section>
<?php endif; ?>

<!-- ===== ABOUT SECTION WITH VIDEO ===== -->
<?php
$aboutVideo = $industryData['about_video'] ?? '';
$aboutPhoto2 = $industryData['about_photo'] ?? ($galleryPhotos[0] ?? $heroPhoto);
$aboutText = $industryData['about_text'] ?? "The $name industry encompasses a wide range of specialized services and solutions designed to meet the evolving needs of businesses worldwide. From enterprise resource planning to customer management, our platform provides the digital infrastructure needed to compete in today's market.";
$aboutHighlights = $industryData['about_highlights'] ?? array(
    'Dedicated ERP workflows aligned to ' . strtolower($name) . ' operations',
    'Control Panel with industry-specific configuration',
    count($subIndustries) . ' sub-industries covered — each with custom storefront',
    'Integration-ready: connects with existing tools and processes',
    'Demo environment with sample data — try before you commit'
);
?>
<section class="ind-about reveal" id="about-section">
<div class="ind-about-inner">
<div class="ind-about-text">
<h2>About <?php echo htmlspecialchars($name);?></h2>
<p><?php echo htmlspecialchars($aboutText);?></p>
<p>Our platform serves <strong><?php echo count($subIndustries);?> specialized sub-industries</strong> within <?php echo htmlspecialchars(strtolower($name));?>, each with tailored ERP processes, storefront templates, and Control Panel configurations.</p>
<ul class="about-highlights">
<?php foreach($aboutHighlights as $highlight): ?>
<li><?php echo htmlspecialchars($highlight);?></li>
<?php endforeach; ?>
</ul>
</div>
<div class="ind-about-media">
<img src="<?php echo htmlspecialchars($aboutPhoto2);?>" alt="About <?php echo htmlspecialchars($name);?> — Industry Overview" loading="lazy">
</div>
</div>
</section>

<!-- ===== PRODUCT/SERVICE CATEGORIES & SUB-CATEGORIES ===== -->
<?php
$subProducts = $industryData['sub_industry_products'] ?? array();
$allCategories = array();
if(!empty($subIndustries) && !empty($subProducts)) {
    foreach($subIndustries as $subName) {
        $sd = $subProducts[$subName] ?? null;
        if($sd && !empty($sd['categories'])) {
            $allCategories[$subName] = $sd['categories'];
        }
    }
}
if(!empty($allCategories)):
$totalCats = 0; foreach($allCategories as $cats) $totalCats += count($cats);
?>
<section class="ind-categories reveal" id="categories">
<h2>Product & Service Categories</h2>
<p class="section-sub"><?php echo $totalCats;?> categories across <?php echo count($allCategories);?> sub-industries — covering the full <?php echo htmlspecialchars(strtolower($name));?> spectrum</p>
<div class="ind-categories-grid">
<?php
$catIcons = array('fa-cubes','fa-tags','fa-briefcase','fa-cogs','fa-shopping-bag','fa-industry','fa-wrench','fa-flask','fa-truck','fa-heartbeat','fa-graduation-cap','fa-building','fa-leaf','fa-bolt','fa-paint-brush','fa-diamond','fa-plane','fa-shield','fa-futbol-o','fa-print','fa-paw','fa-home','fa-film','fa-balance-scale','fa-cutlery','fa-laptop','fa-car','fa-plug');
$iconIdx = 0;
foreach(array_slice($allCategories, 0, 12, true) as $subName => $cats):
$catIcon = $catIcons[$iconIdx % count($catIcons)];
$iconIdx++;
?>
<div class="cat-group">
<div class="cat-group__header">
<div class="cat-group__icon"><i class="fa <?php echo $catIcon;?>"></i></div>
<div>
<div class="cat-group__title"><?php echo htmlspecialchars($subName);?></div>
<div class="cat-group__count"><?php echo count($cats);?> categories</div>
</div>
</div>
<ul class="cat-group__list">
<?php foreach($cats as $cat): ?>
<li><?php echo htmlspecialchars($cat);?><span class="cat-badge">Products</span></li>
<?php endforeach; ?>
</ul>
</div>
<?php endforeach; ?>
</div>
<?php if(count($allCategories) > 12): ?>
<p style="text-align:center;margin-top:24px;color:#64748b;font-size:14px">+ <?php echo (count($allCategories) - 12);?> more sub-industry categories below in the detailed section</p>
<?php endif; ?>
</section>
<?php endif; ?>

<!-- ===== PRODUCTS ===== -->
<?php if(!empty($products)): ?>
<section class="ind-products reveal" id="products">
<h2>Sample <?php echo htmlspecialchars(($industryData['product_label'] ?? '') ?: 'Products & Services');?></h2>
<p class="section-sub">Explore our catalog — add items to cart and experience the full e-commerce flow</p>
<div class="ind-products-grid">
<?php foreach($products as $idx => $prod): ?>
<div class="product-card">
<div class="product-card__img">
<?php if(!empty($prod['image'])): ?>
<img src="<?php echo htmlspecialchars($prod['image']);?>" alt="<?php echo htmlspecialchars($prod['name']);?>" loading="lazy">
<?php else: ?>
<div class="prod-icon-lg"><i class="fa <?php echo htmlspecialchars($prod['icon'] ?? 'fa-cube');?>"></i></div>
<?php endif; ?>
<?php if(!empty($prod['category'])): ?>
<span class="product-card__cat"><?php echo htmlspecialchars($prod['category']);?></span>
<?php endif; ?>
</div>
<div class="product-card__body">
<div class="product-card__name"><?php echo htmlspecialchars($prod['name']);?></div>
<div class="product-card__price"><?php echo htmlspecialchars($prod['price'] ?? '');?></div>
<button class="product-card__add" onclick="addToCart(<?php echo $idx;?>)" id="addBtn<?php echo $idx;?>"><i class="fa fa-cart-plus"></i> Add to Cart</button>
</div>
</div>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- ===== FEATURES ===== -->
<?php if(!empty($features)): ?>
<section class="ind-features reveal" id="features">
<h2>Industry-Specific ERP Features</h2>
<p class="section-sub">Purpose-built tools for <?php echo htmlspecialchars(strtolower($name));?> businesses worldwide</p>
<div class="ind-features-grid">
<?php foreach($features as $feat): ?>
<div class="feat-card">
<div class="feat-icon"><i class="fa <?php echo htmlspecialchars($feat['icon'] ?? 'fa-check');?>"></i></div>
<h4><?php echo htmlspecialchars($feat['title']);?></h4>
<p><?php echo htmlspecialchars($feat['desc']);?></p>
</div>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- ===== GALLERY ===== -->
<?php if(!empty($galleryPhotos)): ?>
<section class="ind-gallery reveal">
<h2><?php echo htmlspecialchars($name);?> in Action</h2>
<div class="ind-gallery-grid">
<?php foreach(array_slice($galleryPhotos, 0, 5) as $photo): ?>
<div class="ind-gallery-item">
<img src="<?php echo htmlspecialchars($photo);?>" alt="<?php echo htmlspecialchars($name);?>" loading="lazy">
</div>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- ===== SUB-INDUSTRIES WITH BUSINESS PROCESSES ===== -->
<?php
$subProducts = $industryData['sub_industry_products'] ?? array();
if(!empty($subIndustries)):
?>
<section class="ind-subs reveal" id="industries">
<h2><?php echo htmlspecialchars($name);?> — Sub-Industry Solutions</h2>
<p class="section-sub"><?php echo count($subIndustries);?> specialized verticals — each with dedicated ERP workflows, CP configuration, and storefront</p>

<!-- Filter buttons -->
<div class="sub-filter">
<button class="sub-filter-btn active" onclick="filterSubs('all')">All (<?php echo count($subIndustries);?>)</button>
<?php
$chunks = array_chunk($subIndustries, (int)ceil(count($subIndustries)/3));
$labels = array('Core','Extended','Specialized');
foreach($chunks as $ci => $chunk):
?>
<button class="sub-filter-btn" onclick="filterSubs('group-<?php echo $ci;?>')"><?php echo $labels[$ci] ?? 'More';?> (<?php echo count($chunk);?>)</button>
<?php endforeach; ?>
</div>

<!-- Sub-industry cards grid -->
<div class="ind-subs-grid">
<?php
$groupIdx = 0;
$perGroup = (int)ceil(count($subIndustries)/3);
foreach($subIndustries as $si => $sub):
    $currentGroup = (int)floor($si / $perGroup);
    $subData = $subProducts[$sub] ?? null;
    $subPhoto = $subData ? $subData['photo'] : '';
    $subDesc = $subData ? $subData['desc'] : 'Specialized solutions for ' . strtolower($sub);
    $subProds = ($subData && isset($subData['products'])) ? $subData['products'] : array();
    $subCats = ($subData && isset($subData['categories'])) ? $subData['categories'] : array();
    
    // Generate business process steps from sub-industry name
    $processSteps = array('Discovery','Configure','Deploy','Operate','Optimize');
?>
<div class="sub-card reveal" data-group="group-<?php echo $currentGroup;?>" itemscope itemtype="https://schema.org/Service">
<div class="sub-card__img">
<?php if($subPhoto): ?>
<img src="<?php echo $subPhoto;?>" alt="<?php echo htmlspecialchars($sub);?> — <?php echo htmlspecialchars($name);?>" loading="lazy" itemprop="image">
<?php else: ?>
<div style="width:100%;height:100%;background:linear-gradient(135deg,var(--primary),var(--accent));display:flex;align-items:center;justify-content:center"><i class="fa <?php echo htmlspecialchars($icon);?>" style="font-size:3rem;color:rgba(255,255,255,.5)"></i></div>
<?php endif; ?>
<div class="sub-card__badge">
<span class="b-erp">ERP</span>
<span class="b-cp">CP</span>
<span class="b-store">Store</span>
</div>
</div>
<div class="sub-card__body">
<h3 class="sub-card__name" itemprop="name"><?php echo htmlspecialchars($sub);?></h3>
<p class="sub-card__desc" itemprop="description"><?php echo htmlspecialchars($subDesc);?></p>

<!-- Product Categories -->
<?php if(!empty($subCats)): ?>
<div class="sub-card__cats">
<?php foreach(array_slice($subCats, 0, 6) as $cat): ?>
<span class="cat-tag"><?php echo htmlspecialchars($cat);?></span>
<?php endforeach; ?>
</div>
<?php endif; ?>

<!-- Business Process Flow -->
<div class="sub-card__process">
<?php foreach($processSteps as $psi => $step): ?>
<?php if($psi > 0): ?><span class="proc-arrow"><i class="fa fa-chevron-right"></i></span><?php endif; ?>
<span class="proc-step"><?php echo $step;?></span>
<?php endforeach; ?>
</div>

<!-- Sub-industry Products -->
<?php if(!empty($subProds)): ?>
<div class="sub-card__products">
<?php foreach(array_slice($subProds, 0, 3) as $sp): ?>
<div class="sub-prod" itemprop="offers" itemscope itemtype="https://schema.org/Offer">
<img src="<?php echo $sp['image'];?>" alt="<?php echo htmlspecialchars($sp['name']);?>" loading="lazy">
<div class="sp-name" itemprop="name"><?php echo htmlspecialchars($sp['name']);?></div>
<div class="sp-price" itemprop="price"><?php echo $sp['price'];?></div>
</div>
<?php endforeach; ?>
</div>
<?php endif; ?>

<!-- CTA buttons -->
<div class="sub-card__cta">
<a href="#" class="sc-site" onclick="openAuth('register');return false"><i class="fa fa-shopping-cart"></i> Try Store</a>
<a href="/cp/demo/<?php echo urlencode($demoKey);?>/" class="sc-erp"><i class="fa fa-cogs"></i> Open CP</a>
</div>
</div>
</div>
<?php endforeach; ?>
</div>
</section>
<?php endif; ?>

<!-- ===== TESTIMONIAL ===== -->
<?php if($testimonial): ?>
<section class="ind-testimonial reveal">
<blockquote>&ldquo;<?php echo htmlspecialchars($testimonial['quote']);?>&rdquo;</blockquote>
<cite>— <?php echo htmlspecialchars($testimonial['author']);?></cite>
</section>
<?php endif; ?>

<!-- ===== CTA ===== -->
<section class="ind-cta reveal">
<h2>Ready to Transform Your <?php echo htmlspecialchars($name);?> Business?</h2>
<p>Start with a free demo — full ERP + CP + Storefront access. No credit card required.</p>
<div class="ind-cta-buttons">
<a href="#" class="btn-cta btn-cta-primary" onclick="openAuth('register');return false"><i class="fa fa-rocket"></i> Start Free Demo</a>
<a href="/platform/industries" class="btn-cta btn-cta-secondary"><i class="fa fa-th-large"></i> View All Industries</a>
</div>
</section>

<!-- ===== FOOTER ===== -->
<footer class="site-footer">
<div class="footer-inner">
<div class="footer-brand">
<h3><i class="fa <?php echo htmlspecialchars($icon);?>"></i> <?php echo htmlspecialchars($name);?></h3>
<p>Enterprise-grade storefront, control panel, and ERP — built for <?php echo htmlspecialchars(strtolower($name));?> businesses. Part of the ecomae platform covering 28 industries worldwide.</p>
<div class="footer-social">
<a href="#"><i class="fa fa-facebook"></i></a>
<a href="#"><i class="fa fa-twitter"></i></a>
<a href="#"><i class="fa fa-linkedin"></i></a>
<a href="#"><i class="fa fa-instagram"></i></a>
<a href="#"><i class="fa fa-youtube"></i></a>
</div>
</div>
<div class="footer-col">
<h4>Quick Links</h4>
<ul>
<li><a href="#products">Products</a></li>
<li><a href="#features">Features</a></li>
<li><a href="/platform/industries">All Industries</a></li>
<li><a href="/cp/">Control Panel</a></li>
<li><a href="/bos/">BOS Portal</a></li>
</ul>
</div>
<div class="footer-col">
<h4>Support</h4>
<ul>
<li><a href="#">Documentation</a></li>
<li><a href="#">API Reference</a></li>
<li><a href="#">Help Center</a></li>
<li><a href="#">Contact Sales</a></li>
<li><a href="#">Status Page</a></li>
</ul>
</div>
<div class="footer-col">
<h4>Stay Updated</h4>
<p style="font-size:12px;color:#64748b;margin-bottom:12px">Get industry insights and platform updates</p>
<div class="footer-newsletter">
<input type="email" placeholder="Enter your email">
<button type="button">Subscribe</button>
</div>
</div>
</div>
<div class="footer-bottom">
<p>&copy; <?php echo date('Y');?> <a href="https://www.ecomae.com" style="color:var(--accent)">ecomae</a> — Enterprise Business Operating System</p>
<span class="demo-link">Demo: <code>demo@ecomae.com</code> / <code>demo2026</code></span>
</div>
</footer>

<!-- ===== JAVASCRIPT ===== -->
<script>
(function(){
/* Product data for cart */
var products = <?php echo json_encode(array_values($products), JSON_HEX_TAG|JSON_HEX_AMP);?>;
var cart = [];

/* Header scroll effect */
var header = document.getElementById('siteHeader');
window.addEventListener('scroll', function(){
header.classList.toggle('scrolled', window.scrollY > 20);
});

/* Cart functions */
window.addToCart = function(idx){
var p = products[idx];
if(!p) return;
var existing = cart.find(function(c){return c.idx === idx;});
if(existing) existing.qty++;
else cart.push({idx:idx, name:p.name, price:p.price||'', image:p.image||'', icon:p.icon||'fa-cube', qty:1});
updateCartUI();
var btn = document.getElementById('addBtn'+idx);
if(btn){btn.classList.add('added');btn.innerHTML='<i class="fa fa-check"></i> Added!';setTimeout(function(){btn.classList.remove('added');btn.innerHTML='<i class="fa fa-cart-plus"></i> Add to Cart'},1500)}
};

window.removeFromCart = function(idx){
cart = cart.filter(function(c){return c.idx !== idx});
updateCartUI();
};

window.changeQty = function(idx, delta){
var item = cart.find(function(c){return c.idx === idx});
if(!item) return;
item.qty += delta;
if(item.qty <= 0) cart = cart.filter(function(c){return c.idx !== idx});
updateCartUI();
};

function updateCartUI(){
var countEl = document.getElementById('cartCount');
var itemsEl = document.getElementById('cartItems');
var footerEl = document.getElementById('cartFooter');
var totalEl = document.getElementById('cartTotal');
var total = cart.reduce(function(s,c){return s+c.qty},0);
countEl.textContent = total;
countEl.classList.toggle('show', total > 0);
if(total > 0) countEl.style.animation = 'cartBounce .3s';
if(cart.length === 0){
itemsEl.innerHTML = '<div class="mini-cart__empty"><i class="fa fa-shopping-bag"></i><p>Your cart is empty</p></div>';
footerEl.style.display = 'none';
} else {
var html = '';
var priceTotal = 0;
cart.forEach(function(c){
var numPrice = parseFloat((c.price||'0').replace(/[^0-9.]/g,''));
priceTotal += numPrice * c.qty;
var imgHtml = c.image ? '<img src="'+c.image+'" alt="">' : '<i class="fa '+c.icon+'"></i>';
html += '<div class="mini-cart__item"><div class="mini-cart__item-img">'+imgHtml+'</div><div class="mini-cart__item-info"><div class="mini-cart__item-name">'+c.name+'</div><div class="mini-cart__item-price">'+c.price+'</div><div class="mini-cart__item-qty"><button onclick="changeQty('+c.idx+',-1)">−</button><span>'+c.qty+'</span><button onclick="changeQty('+c.idx+',1)">+</button></div></div><div class="mini-cart__item-remove" onclick="removeFromCart('+c.idx+')"><i class="fa fa-trash"></i></div></div>';
});
itemsEl.innerHTML = html;
totalEl.textContent = 'AED ' + priceTotal.toFixed(2);
footerEl.style.display = 'block';
}
}

window.openCart = function(){
document.getElementById('miniCart').classList.add('open');
document.getElementById('cartOverlay').classList.add('open');
document.body.style.overflow = 'hidden';
};
window.closeCart = function(){
document.getElementById('miniCart').classList.remove('open');
document.getElementById('cartOverlay').classList.remove('open');
document.body.style.overflow = '';
};
window.checkout = function(){
closeCart();
openAuth('register');
};

/* Auth modal */
window.openAuth = function(tab){
document.getElementById('authModal').classList.add('open');
document.body.style.overflow = 'hidden';
switchAuthTab(tab||'login');
};
window.closeAuth = function(){
document.getElementById('authModal').classList.remove('open');
document.body.style.overflow = '';
};
window.switchAuthTab = function(tab){
document.querySelectorAll('.auth-modal__tab').forEach(function(t){t.classList.toggle('active',t.dataset.tab===tab)});
document.getElementById('formLogin').classList.toggle('active',tab==='login');
document.getElementById('formRegister').classList.toggle('active',tab==='register');
};
window.handleLogin = function(e){
e.preventDefault();
var email = document.getElementById('loginEmail').value;
var pass = document.getElementById('loginPass').value;
if(email === 'demo@ecomae.com' && pass === 'demo2026'){
window.location.href = '/cp/demo/<?php echo htmlspecialchars($demoKey);?>/';
} else {
window.location.href = '/cp/';
}
return false;
};
window.handleRegister = function(e){
e.preventDefault();
var industry = document.getElementById('regIndustry').value;
alert('Registration successful! Redirecting to your ' + industry + ' control panel...');
window.location.href = '/cp/demo/' + industry + '/';
return false;
};

/* Mobile nav */
window.toggleMobileNav = function(){
var nav = document.querySelector('.header-nav');
nav.style.display = nav.style.display === 'flex' ? 'none' : 'flex';
nav.style.position = 'absolute';
nav.style.top = '64px';
nav.style.left = '0';
nav.style.right = '0';
nav.style.background = '#fff';
nav.style.padding = '16px';
nav.style.flexDirection = 'column';
nav.style.boxShadow = '0 4px 20px rgba(0,0,0,.1)';
};

/* Sub-industry filter */
window.filterSubs = function(group){
var cards = document.querySelectorAll('.sub-card');
var btns = document.querySelectorAll('.sub-filter-btn');
btns.forEach(function(b){b.classList.remove('active')});
event.target.classList.add('active');
cards.forEach(function(c){
if(group === 'all'){c.style.display = '';c.classList.add('visible')}
else{
if(c.dataset.group === group){c.style.display = '';c.classList.add('visible')}
else{c.style.display = 'none'}
}
});
};

/* Scroll reveal */
var reveals = document.querySelectorAll('.reveal');
var io = new IntersectionObserver(function(entries){
entries.forEach(function(e){if(e.isIntersecting){e.target.classList.add('visible');io.unobserve(e.target)}})
},{threshold:.1,rootMargin:'0px 0px -40px 0px'});
reveals.forEach(function(el){io.observe(el)});

/* Smooth scroll for anchor links */
document.querySelectorAll('a[href^="#"]').forEach(function(a){
a.addEventListener('click', function(e){
var target = document.querySelector(this.getAttribute('href'));
if(target){e.preventDefault();target.scrollIntoView({behavior:'smooth',block:'start'})}
});
});
})();
</script>

<?php if($heroVideoUrl): ?>
<script>
/* YouTube IFrame API - detect video errors and hide broken videos */
var tag=document.createElement('script');tag.src='https://www.youtube.com/iframe_api';
var fs=document.getElementsByTagName('script')[0];fs.parentNode.insertBefore(tag,fs);
function onYouTubeIframeAPIReady(){
var frame=document.getElementById('heroVideoFrame');
if(!frame)return;
var player=new YT.Player('heroVideoFrame',{
events:{
'onError':function(){document.getElementById('heroVideoWrap').classList.add('video-error')},
'onReady':function(e){e.target.mute();e.target.playVideo()}
}
});
/* Fallback: if video doesn't start within 5s, hide it */
setTimeout(function(){
try{if(player.getPlayerState&&player.getPlayerState()<=0)document.getElementById('heroVideoWrap').classList.add('video-error')}catch(x){document.getElementById('heroVideoWrap').classList.add('video-error')}
},5000);
}
</script>
<?php endif; ?>

</body>
</html>
