/**
 * Live-tenant premium 3D — hero-mounted canvas + depth tilt for custom storefronts.
 * Motifs align with industry_3d.js particle families.
 */
(function () {
	'use strict';

	var ROOT_SEL = 'body.epc-sf-premium-3d';
	var HERO_SEL = [
		'.epc-er-hero-banner',
		'.epc-frn-hero-banner',
		'.epc-jrk-hero-banner',
		'.epc-cpi-hero-banner',
		'.epc-cpi-hero-slider',
		'.epc-er-hero',
		'.epc-frn-hero',
		'.epc-jrk-hero'
	].join(',');
	var TILT_SEL = [
		'.epc-er-product-card',
		'.epc-frn-product-card',
		'.epc-jrk-product-card',
		'.epc-er-cat-tile',
		'.epc-frn-cat-tile',
		'.epc-jrk-cat-tile',
		'.epc-cpi-icon-box',
		'.epc-cpi-service-card',
		'.epc-home-pro__stat',
		'.epc-card-lift',
		'.epc-cpi-hero-slide__card'
	].join(',');

	var MOTIFS = {
		electronics: { shape: 'node', density: 88, speed: 1.2, links: true, trails: true },
		fashion: { shape: 'ribbon', density: 55, speed: 0.7, links: false, trails: false },
		jewellery: { shape: 'spark', density: 85, speed: 1.0, links: false, trails: true },
		professional: { shape: 'orbit', density: 60, speed: 0.85, links: true, trails: false },
		finance: { shape: 'bar', density: 72, speed: 1.0, links: true, trails: true },
		automotive: { shape: 'hex', density: 80, speed: 1.45, links: true, trails: true },
		default: { shape: 'orb', density: 70, speed: 1.05, links: true, trails: true }
	};

	var SPRITE_ICONS = {
		electronics: ['fa-mobile', 'fa-laptop', 'fa-wifi', 'fa-hdd-o', 'fa-headphones', 'fa-gamepad'],
		fashion: ['fa-shopping-bag', 'fa-diamond', 'fa-heart', 'fa-star', 'fa-tag', 'fa-camera'],
		jewellery: ['fa-diamond', 'fa-star', 'fa-certificate', 'fa-magic', 'fa-gift', 'fa-heart'],
		professional: ['fa-briefcase', 'fa-users', 'fa-file-text', 'fa-comments', 'fa-calendar', 'fa-check'],
		finance: ['fa-line-chart', 'fa-university', 'fa-credit-card', 'fa-pie-chart', 'fa-briefcase', 'fa-balance-scale'],
		automotive: ['fa-car', 'fa-cog', 'fa-wrench', 'fa-tachometer', 'fa-road', 'fa-key'],
		default: ['fa-cube', 'fa-star', 'fa-bolt', 'fa-globe', 'fa-cog', 'fa-diamond']
	};

	function prefersReducedMotion() {
		return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	}

	function cssColor(name, fallback) {
		var v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
		if (!v) {
			v = getComputedStyle(document.body).getPropertyValue(name).trim();
		}
		return v || fallback;
	}

	function hexToRgb(hex) {
		hex = (hex || '').replace('#', '');
		if (hex.length === 3) hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
		if (hex.length !== 6) return { r: 15, g: 118, b: 110 };
		return {
			r: parseInt(hex.slice(0, 2), 16),
			g: parseInt(hex.slice(2, 4), 16),
			b: parseInt(hex.slice(4, 6), 16)
		};
	}

	function ensureStage(hero, motif, iconClass) {
		if (!hero.querySelector('.ind3d-stage')) {
			var stage = document.createElement('div');
			stage.className = 'ind3d-stage';
			stage.setAttribute('aria-hidden', 'true');
			stage.innerHTML =
				'<div class="ind3d-ring"></div>' +
				'<div class="ind3d-ring ind3d-ring--2"></div>' +
				'<div class="ind3d-core"><i class="fa ' + (iconClass || 'fa-cube') + '"></i></div>';
			hero.insertBefore(stage, hero.firstChild);
		}
		if (!hero.querySelector('.ind3d-sprites')) {
			var sprites = document.createElement('div');
			sprites.className = 'ind3d-sprites';
			sprites.setAttribute('aria-hidden', 'true');
			var icons = SPRITE_ICONS[motif] || SPRITE_ICONS.default;
			for (var i = 0; i < Math.min(6, icons.length); i++) {
				var el = document.createElement('div');
				el.className = 'ind3d-sprite';
				el.innerHTML = '<i class="fa ' + icons[i] + '"></i>';
				sprites.appendChild(el);
			}
			hero.appendChild(sprites);
		}
	}

	function mountCanvas(hero, motifKey) {
		var cfg = MOTIFS[motifKey] || MOTIFS.default;
		var canvas = document.createElement('canvas');
		canvas.className = 'ind3d-canvas epc-sf3d-canvas';
		canvas.setAttribute('aria-hidden', 'true');
		hero.insertBefore(canvas, hero.firstChild);

		var ctx = canvas.getContext('2d');
		if (!ctx) return;

		var primary = hexToRgb(cssColor('--epc-sf3d-primary', cssColor('--primary', '#0f766e')));
		var accent = hexToRgb(cssColor('--epc-sf3d-accent', cssColor('--accent', '#14b8a6')));
		var dpr = Math.min(window.devicePixelRatio || 1, 2);
		var w = 0;
		var h = 0;
		var particles = [];
		var running = true;
		var t0 = performance.now();
		var pointer = { x: 0.72, y: 0.45 };

		function resize() {
			var rect = hero.getBoundingClientRect();
			w = Math.max(320, rect.width || window.innerWidth);
			h = Math.max(280, rect.height || 420);
			canvas.width = Math.floor(w * dpr);
			canvas.height = Math.floor(h * dpr);
			canvas.style.width = w + 'px';
			canvas.style.height = h + 'px';
			ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
			seed();
		}

		function seed() {
			particles = [];
			var count = Math.min(120, Math.max(40, Math.floor(cfg.density * (w * h) / 780000)));
			for (var i = 0; i < count; i++) {
				particles.push({
					x: Math.random() * w,
					y: Math.random() * h,
					z: 0.35 + Math.random() * 0.65,
					vx: (Math.random() - 0.5) * 0.55 * cfg.speed,
					vy: (Math.random() - 0.5) * 0.4 * cfg.speed,
					r: 1.4 + Math.random() * 3.2,
					phase: Math.random() * Math.PI * 2,
					trail: []
				});
			}
		}

		function drawShape(p, color, alpha) {
			var size = p.r * (1 + p.z);
			ctx.save();
			ctx.translate(p.x, p.y);
			ctx.globalAlpha = alpha;
			ctx.fillStyle = color;
			ctx.strokeStyle = color;
			ctx.lineWidth = 1.4;
			switch (cfg.shape) {
				case 'ribbon':
					ctx.beginPath();
					ctx.moveTo(-size * 1.8, 0);
					ctx.quadraticCurveTo(0, -size * 1.8, size * 1.8, 0);
					ctx.quadraticCurveTo(0, size * 1.4, -size * 1.8, 0);
					ctx.fill();
					break;
				case 'spark':
					for (var s = 0; s < 4; s++) {
						ctx.rotate(Math.PI / 4);
						ctx.fillRect(-size * 0.22, -size * 1.8, size * 0.44, size * 3.6);
					}
					break;
				case 'node':
					ctx.beginPath();
					ctx.arc(0, 0, size, 0, Math.PI * 2);
					ctx.fill();
					ctx.globalAlpha = alpha * 0.55;
					ctx.strokeRect(-size * 1.8, -size * 1.8, size * 3.6, size * 3.6);
					break;
				case 'bar':
					ctx.fillRect(-size * 0.45, -size * 1.7, size * 0.9, size * 3.4);
					ctx.globalAlpha = alpha * 0.55;
					ctx.fillRect(size * 0.55, -size * 0.7, size * 0.8, size * 2);
					break;
				case 'orbit':
					ctx.beginPath();
					ctx.arc(0, 0, size * 1.8, 0, Math.PI * 2);
					ctx.stroke();
					ctx.beginPath();
					ctx.arc(Math.cos(p.phase) * size * 1.8, Math.sin(p.phase) * size * 1.8, size * 0.5, 0, Math.PI * 2);
					ctx.fill();
					break;
				case 'hex':
					ctx.beginPath();
					for (var i = 0; i < 6; i++) {
						var a = (Math.PI / 3) * i + Math.PI / 6;
						var px = Math.cos(a) * size * 1.6;
						var py = Math.sin(a) * size * 1.6;
						if (i === 0) ctx.moveTo(px, py);
						else ctx.lineTo(px, py);
					}
					ctx.closePath();
					ctx.stroke();
					ctx.globalAlpha = alpha * 0.35;
					ctx.fill();
					break;
				default:
					ctx.beginPath();
					ctx.arc(0, 0, size, 0, Math.PI * 2);
					ctx.fill();
			}
			ctx.restore();
		}

		function tick(now) {
			requestAnimationFrame(tick);
			if (!running) return;
			var time = now - t0;
			ctx.clearRect(0, 0, w, h);

			ctx.strokeStyle = 'rgba(' + accent.r + ',' + accent.g + ',' + accent.b + ',0.07)';
			ctx.lineWidth = 1;
			var spacing = 72;
			var ox = (pointer.x - 0.5) * 36;
			ctx.beginPath();
			for (var x = 0; x < w; x += spacing) {
				ctx.moveTo(x + ox, 0);
				ctx.lineTo(x + ox + 28, h);
			}
			ctx.stroke();

			var colorA = 'rgb(' + accent.r + ',' + accent.g + ',' + accent.b + ')';
			var colorP = 'rgb(' + primary.r + ',' + primary.g + ',' + primary.b + ')';

			for (var i = 0; i < particles.length; i++) {
				var p = particles[i];
				p.phase += 0.025 * cfg.speed;
				p.x += p.vx + Math.sin(time * 0.001 + p.phase) * 0.12 * cfg.speed;
				p.y += p.vy + Math.cos(time * 0.0011 + p.phase) * 0.1 * cfg.speed;
				if (p.x < -40) p.x = w + 40;
				if (p.x > w + 40) p.x = -40;
				if (p.y < -40) p.y = h + 40;
				if (p.y > h + 40) p.y = -40;

				if (cfg.trails) {
					p.trail.push({ x: p.x, y: p.y });
					if (p.trail.length > 8) p.trail.shift();
					for (var tr = 1; tr < p.trail.length; tr++) {
						var a = p.trail[tr - 1];
						var b = p.trail[tr];
						ctx.strokeStyle = colorA;
						ctx.globalAlpha = (tr / p.trail.length) * 0.32 * p.z;
						ctx.beginPath();
						ctx.moveTo(a.x, a.y);
						ctx.lineTo(b.x, b.y);
						ctx.stroke();
					}
					ctx.globalAlpha = 1;
				}

				drawShape(p, i % 2 ? colorA : colorP, 0.32 + p.z * 0.5);
			}

			if (cfg.links) {
				for (var ai = 0; ai < particles.length; ai++) {
					for (var bi = ai + 1; bi < ai + 5 && bi < particles.length; bi++) {
						var pa = particles[ai];
						var pb = particles[bi];
						var dx = pa.x - pb.x;
						var dy = pa.y - pb.y;
						var dist = Math.sqrt(dx * dx + dy * dy);
						if (dist < 120) {
							ctx.strokeStyle = 'rgba(' + accent.r + ',' + accent.g + ',' + accent.b + ',' + ((1 - dist / 120) * 0.26) + ')';
							ctx.beginPath();
							ctx.moveTo(pa.x, pa.y);
							ctx.lineTo(pb.x, pb.y);
							ctx.stroke();
						}
					}
				}
			}
		}

		function onPointer(e) {
			var rect = hero.getBoundingClientRect();
			pointer.x = (e.clientX - rect.left) / Math.max(rect.width, 1);
			pointer.y = (e.clientY - rect.top) / Math.max(rect.height, 1);
		}

		resize();
		window.addEventListener('resize', resize, { passive: true });
		window.addEventListener('pointermove', onPointer, { passive: true });
		if ('IntersectionObserver' in window) {
			var io = new IntersectionObserver(function (entries) {
				running = !!(entries[0] && entries[0].isIntersecting);
			}, { threshold: 0.05 });
			io.observe(hero);
		}
		tick(performance.now());
	}

	function bindTilt(root) {
		root.querySelectorAll(TILT_SEL).forEach(function (el) {
			el.addEventListener('pointermove', function (e) {
				var rect = el.getBoundingClientRect();
				var px = (e.clientX - rect.left) / Math.max(rect.width, 1);
				var py = (e.clientY - rect.top) / Math.max(rect.height, 1);
				el.style.transform =
					'perspective(900px) rotateX(' + ((0.5 - py) * 10).toFixed(2) +
					'deg) rotateY(' + ((px - 0.5) * 12).toFixed(2) + 'deg) translateZ(14px) scale(1.02)';
			}, { passive: true });
			el.addEventListener('pointerleave', function () {
				el.style.transform = '';
			}, { passive: true });
		});
	}

	function boot() {
		var root = document.querySelector(ROOT_SEL);
		if (!root) return;
		var hero = root.querySelector(HERO_SEL);
		if (!hero) return;
		var motif = root.getAttribute('data-epc-3d-motif') || 'default';
		var iconClass = root.getAttribute('data-epc-3d-icon') || 'fa-cube';
		if (iconClass.indexOf('fa-') !== 0) iconClass = 'fa-cube';

		ensureStage(hero, motif, iconClass);
		if (!prefersReducedMotion()) {
			mountCanvas(hero, motif);
			bindTilt(root);
		}
		root.setAttribute('data-epc-sf3d-ready', '1');
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
