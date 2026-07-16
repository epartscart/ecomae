/**
 * Industry subdomain premium 3D + motif animations.
 * Reads data-ind-motif / CSS vars from body.ind-premium-3d.
 */
(function () {
	'use strict';

	var ROOT_SEL = 'body.ind-premium-3d';
	var TILT_SEL = [
		'.product-card',
		'.feat-card',
		'.sub-card',
		'.ind-gallery-item',
		'.ind-stat',
		'.about-card',
		'.ind-highlight'
	].join(',');

	var MOTIFS = {
		energy: { shape: 'bolt', density: 72, speed: 1.25, links: true, trails: true },
		automotive: { shape: 'hex', density: 58, speed: 1.35, links: true, trails: true },
		fashion: { shape: 'ribbon', density: 42, speed: 0.65, links: false, trails: false },
		healthcare: { shape: 'pulse', density: 48, speed: 0.85, links: true, trails: false },
		food: { shape: 'bubble', density: 50, speed: 0.7, links: false, trails: false },
		jewellery: { shape: 'spark', density: 64, speed: 0.9, links: false, trails: true },
		electronics: { shape: 'node', density: 70, speed: 1.1, links: true, trails: true },
		construction: { shape: 'block', density: 44, speed: 0.75, links: true, trails: false },
		manufacturing: { shape: 'gear', density: 52, speed: 1.0, links: true, trails: false },
		finance: { shape: 'bar', density: 55, speed: 0.95, links: true, trails: true },
		logistics: { shape: 'route', density: 60, speed: 1.2, links: true, trails: true },
		hospitality: { shape: 'wave', density: 40, speed: 0.6, links: false, trails: false },
		beauty: { shape: 'petal', density: 46, speed: 0.7, links: false, trails: false },
		agriculture: { shape: 'leaf', density: 48, speed: 0.7, links: false, trails: false },
		education: { shape: 'orbit', density: 50, speed: 0.85, links: true, trails: false },
		professional: { shape: 'orbit', density: 46, speed: 0.8, links: true, trails: false },
		retail: { shape: 'bubble', density: 52, speed: 0.9, links: false, trails: false },
		media: { shape: 'node', density: 58, speed: 1.15, links: true, trails: true },
		sports: { shape: 'hex', density: 56, speed: 1.3, links: true, trails: true },
		security: { shape: 'hex', density: 50, speed: 0.9, links: true, trails: false },
		technology: { shape: 'node', density: 68, speed: 1.15, links: true, trails: true },
		default: { shape: 'orb', density: 54, speed: 1.0, links: true, trails: false }
	};

	var SPRITE_ICONS = {
		energy: ['fa-bolt', 'fa-sun-o', 'fa-plug', 'fa-battery-full', 'fa-tachometer', 'fa-leaf'],
		automotive: ['fa-car', 'fa-cog', 'fa-wrench', 'fa-dashboard', 'fa-road', 'fa-key'],
		fashion: ['fa-shopping-bag', 'fa-diamond', 'fa-heart', 'fa-star', 'fa-tag', 'fa-camera'],
		healthcare: ['fa-heartbeat', 'fa-plus-square', 'fa-user-md', 'fa-stethoscope', 'fa-medkit', 'fa-hospital-o'],
		food: ['fa-cutlery', 'fa-coffee', 'fa-lemon-o', 'fa-shopping-basket', 'fa-fire', 'fa-leaf'],
		jewellery: ['fa-diamond', 'fa-star', 'fa-certificate', 'fa-magic', 'fa-gift', 'fa-heart'],
		electronics: ['fa-mobile', 'fa-laptop', 'fa-wifi', 'fa-hdd-o', 'fa-headphones', 'fa-gamepad'],
		construction: ['fa-building', 'fa-wrench', 'fa-home', 'fa-truck', 'fa-compass', 'fa-map'],
		manufacturing: ['fa-cogs', 'fa-industry', 'fa-cubes', 'fa-flask', 'fa-sliders', 'fa-recycle'],
		finance: ['fa-line-chart', 'fa-university', 'fa-credit-card', 'fa-pie-chart', 'fa-briefcase', 'fa-balance-scale'],
		logistics: ['fa-truck', 'fa-plane', 'fa-ship', 'fa-globe', 'fa-map-marker', 'fa-archive'],
		hospitality: ['fa-bed', 'fa-cutlery', 'fa-glass', 'fa-key', 'fa-suitcase', 'fa-star'],
		beauty: ['fa-heart', 'fa-magic', 'fa-leaf', 'fa-star', 'fa-female', 'fa-smile-o'],
		agriculture: ['fa-leaf', 'fa-tree', 'fa-sun-o', 'fa-tint', 'fa-pagelines', 'fa-globe'],
		education: ['fa-graduation-cap', 'fa-book', 'fa-pencil', 'fa-users', 'fa-lightbulb-o', 'fa-desktop'],
		professional: ['fa-briefcase', 'fa-users', 'fa-file-text', 'fa-comments', 'fa-calendar', 'fa-check'],
		retail: ['fa-shopping-cart', 'fa-tags', 'fa-gift', 'fa-barcode', 'fa-credit-card', 'fa-star'],
		media: ['fa-film', 'fa-video-camera', 'fa-microphone', 'fa-camera', 'fa-play', 'fa-rss'],
		sports: ['fa-futbol-o', 'fa-trophy', 'fa-heartbeat', 'fa-flag', 'fa-bolt', 'fa-users'],
		security: ['fa-shield', 'fa-lock', 'fa-eye', 'fa-video-camera', 'fa-key', 'fa-user-secret'],
		technology: ['fa-code', 'fa-cloud', 'fa-server', 'fa-database', 'fa-rocket', 'fa-cogs'],
		default: ['fa-cube', 'fa-star', 'fa-bolt', 'fa-globe', 'fa-cog', 'fa-diamond']
	};

	function prefersReducedMotion() {
		return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	}

	function cssColor(name, fallback) {
		var v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
		return v || fallback;
	}

	function hexToRgb(hex) {
		hex = (hex || '').replace('#', '');
		if (hex.length === 3) {
			hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
		}
		if (hex.length !== 6) return { r: 56, g: 189, b: 248 };
		return {
			r: parseInt(hex.slice(0, 2), 16),
			g: parseInt(hex.slice(2, 4), 16),
			b: parseInt(hex.slice(4, 6), 16)
		};
	}

	function mountSprites(root, motif) {
		var hero = root.querySelector('.ind-hero');
		if (!hero) return;
		var wrap = document.createElement('div');
		wrap.className = 'ind3d-sprites';
		wrap.setAttribute('aria-hidden', 'true');
		var icons = SPRITE_ICONS[motif] || SPRITE_ICONS.default;
		for (var i = 0; i < Math.min(6, icons.length); i++) {
			var el = document.createElement('div');
			el.className = 'ind3d-sprite';
			el.innerHTML = '<i class="fa ' + icons[i] + '"></i>';
			wrap.appendChild(el);
		}
		hero.appendChild(wrap);
	}

	function mountCanvas(root, motifKey) {
		var cfg = MOTIFS[motifKey] || MOTIFS.default;
		var canvas = document.createElement('canvas');
		canvas.className = 'ind3d-canvas';
		canvas.setAttribute('aria-hidden', 'true');
		document.body.insertBefore(canvas, document.body.firstChild);
		var ctx = canvas.getContext('2d');
		if (!ctx) return null;

		var primary = hexToRgb(cssColor('--primary', '#3b82f6'));
		var accent = hexToRgb(cssColor('--accent', '#60a5fa'));
		var dpr = Math.min(window.devicePixelRatio || 1, 2);
		var w = 0;
		var h = 0;
		var particles = [];
		var running = true;
		var raf = 0;
		var t0 = performance.now();
		var pointer = { x: 0.5, y: 0.4 };

		function resize() {
			w = window.innerWidth;
			h = window.innerHeight;
			canvas.width = Math.floor(w * dpr);
			canvas.height = Math.floor(h * dpr);
			canvas.style.width = w + 'px';
			canvas.style.height = h + 'px';
			ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
			seed();
		}

		function seed() {
			particles = [];
			var count = Math.min(110, Math.max(30, Math.floor((cfg.density * (w * h)) / 900000)));
			for (var i = 0; i < count; i++) {
				particles.push({
					x: Math.random() * w,
					y: Math.random() * h,
					z: 0.3 + Math.random() * 0.7,
					vx: (Math.random() - 0.5) * 0.35 * cfg.speed,
					vy: (Math.random() - 0.5) * 0.25 * cfg.speed,
					r: 1 + Math.random() * 2.4,
					phase: Math.random() * Math.PI * 2,
					trail: []
				});
			}
		}

		function drawShape(p, color, alpha) {
			var size = p.r * (0.8 + p.z);
			ctx.save();
			ctx.translate(p.x, p.y);
			ctx.globalAlpha = alpha;
			ctx.fillStyle = color;
			ctx.strokeStyle = color;
			switch (cfg.shape) {
				case 'bolt':
					ctx.beginPath();
					ctx.moveTo(-size, -size * 1.2);
					ctx.lineTo(size * 0.2, -size * 0.1);
					ctx.lineTo(-size * 0.1, 0);
					ctx.lineTo(size, size * 1.2);
					ctx.lineTo(-size * 0.15, size * 0.15);
					ctx.lineTo(size * 0.2, 0);
					ctx.closePath();
					ctx.fill();
					break;
				case 'hex':
					ctx.beginPath();
					for (var i = 0; i < 6; i++) {
						var a = (Math.PI / 3) * i + Math.PI / 6;
						var px = Math.cos(a) * size * 1.4;
						var py = Math.sin(a) * size * 1.4;
						if (i === 0) ctx.moveTo(px, py);
						else ctx.lineTo(px, py);
					}
					ctx.closePath();
					ctx.stroke();
					break;
				case 'ribbon':
					ctx.beginPath();
					ctx.moveTo(-size * 1.6, 0);
					ctx.quadraticCurveTo(0, -size * 1.5, size * 1.6, 0);
					ctx.quadraticCurveTo(0, size * 1.2, -size * 1.6, 0);
					ctx.fill();
					break;
				case 'pulse':
					ctx.beginPath();
					ctx.arc(0, 0, size, 0, Math.PI * 2);
					ctx.fill();
					ctx.globalAlpha = alpha * 0.35;
					ctx.beginPath();
					ctx.arc(0, 0, size * (2 + Math.sin(p.phase) * 0.6), 0, Math.PI * 2);
					ctx.stroke();
					break;
				case 'bubble':
					ctx.beginPath();
					ctx.arc(0, 0, size * 1.3, 0, Math.PI * 2);
					ctx.globalAlpha = alpha * 0.5;
					ctx.stroke();
					ctx.globalAlpha = alpha;
					ctx.beginPath();
					ctx.arc(-size * 0.3, -size * 0.3, size * 0.35, 0, Math.PI * 2);
					ctx.fill();
					break;
				case 'spark':
					for (var s = 0; s < 4; s++) {
						ctx.rotate(Math.PI / 4);
						ctx.fillRect(-size * 0.2, -size * 1.6, size * 0.4, size * 3.2);
					}
					break;
				case 'node':
					ctx.beginPath();
					ctx.arc(0, 0, size, 0, Math.PI * 2);
					ctx.fill();
					ctx.globalAlpha = alpha * 0.5;
					ctx.strokeRect(-size * 1.6, -size * 1.6, size * 3.2, size * 3.2);
					break;
				case 'block':
					ctx.globalAlpha = alpha * 0.7;
					ctx.fillRect(-size, -size, size * 2, size * 2);
					break;
				case 'gear':
					ctx.beginPath();
					ctx.arc(0, 0, size * 0.7, 0, Math.PI * 2);
					ctx.fill();
					for (var g = 0; g < 6; g++) {
						ctx.rotate(Math.PI / 3);
						ctx.fillRect(size * 0.4, -size * 0.25, size * 1.1, size * 0.5);
					}
					break;
				case 'bar':
					ctx.fillRect(-size * 0.4, -size * 1.5, size * 0.8, size * 3);
					ctx.globalAlpha = alpha * 0.5;
					ctx.fillRect(size * 0.5, -size * 0.6, size * 0.7, size * 1.8);
					break;
				case 'route':
					ctx.beginPath();
					ctx.moveTo(-size * 1.5, size * 0.5);
					ctx.lineTo(0, -size);
					ctx.lineTo(size * 1.5, size * 0.5);
					ctx.stroke();
					ctx.beginPath();
					ctx.arc(0, -size, size * 0.45, 0, Math.PI * 2);
					ctx.fill();
					break;
				case 'wave':
					ctx.beginPath();
					ctx.moveTo(-size * 2, 0);
					ctx.quadraticCurveTo(-size, -size, 0, 0);
					ctx.quadraticCurveTo(size, size, size * 2, 0);
					ctx.stroke();
					break;
				case 'petal':
					ctx.beginPath();
					ctx.ellipse(0, -size * 0.4, size * 0.7, size * 1.3, 0, 0, Math.PI * 2);
					ctx.fill();
					break;
				case 'leaf':
					ctx.beginPath();
					ctx.moveTo(0, -size * 1.5);
					ctx.quadraticCurveTo(size * 1.4, 0, 0, size * 1.5);
					ctx.quadraticCurveTo(-size * 1.4, 0, 0, -size * 1.5);
					ctx.fill();
					break;
				case 'orbit':
					ctx.beginPath();
					ctx.arc(0, 0, size * 1.6, 0, Math.PI * 2);
					ctx.stroke();
					ctx.beginPath();
					ctx.arc(Math.cos(p.phase) * size * 1.6, Math.sin(p.phase) * size * 1.6, size * 0.45, 0, Math.PI * 2);
					ctx.fill();
					break;
				default:
					ctx.beginPath();
					ctx.arc(0, 0, size, 0, Math.PI * 2);
					ctx.fill();
			}
			ctx.restore();
		}

		function drawGrid(time) {
			ctx.save();
			ctx.strokeStyle = 'rgba(' + primary.r + ',' + primary.g + ',' + primary.b + ',0.05)';
			ctx.lineWidth = 1;
			var spacing = cfg.shape === 'hex' ? 64 : 80;
			var ox = (pointer.x - 0.5) * 30 + Math.sin(time * 0.0002) * 10;
			var oy = (pointer.y - 0.5) * 20;
			ctx.beginPath();
			for (var x = -spacing; x < w + spacing; x += spacing) {
				ctx.moveTo(x + ox, 0);
				ctx.lineTo(x + ox + (pointer.x - 0.5) * 40, h);
			}
			for (var y = -spacing; y < h + spacing; y += spacing) {
				ctx.moveTo(0, y + oy);
				ctx.lineTo(w, y + oy + (pointer.y - 0.5) * 24);
			}
			ctx.stroke();
			ctx.restore();
		}

		function tick(now) {
			raf = requestAnimationFrame(tick);
			if (!running) return;
			var time = now - t0;
			ctx.clearRect(0, 0, w, h);
			drawGrid(time);

			var colorA = 'rgb(' + accent.r + ',' + accent.g + ',' + accent.b + ')';
			var colorP = 'rgb(' + primary.r + ',' + primary.g + ',' + primary.b + ')';

			for (var i = 0; i < particles.length; i++) {
				var p = particles[i];
				p.phase += 0.02 * cfg.speed;
				p.x += p.vx + Math.sin(time * 0.001 + p.phase) * 0.08 * cfg.speed;
				p.y += p.vy + Math.cos(time * 0.0012 + p.phase) * 0.06 * cfg.speed;
				if (cfg.shape === 'bubble' || cfg.shape === 'leaf') p.y -= 0.12 * cfg.speed;
				if (cfg.shape === 'route' || cfg.shape === 'bar') p.x += 0.15 * cfg.speed * (p.z);

				if (p.x < -30) p.x = w + 30;
				if (p.x > w + 30) p.x = -30;
				if (p.y < -30) p.y = h + 30;
				if (p.y > h + 30) p.y = -30;

				if (cfg.trails) {
					p.trail.push({ x: p.x, y: p.y });
					if (p.trail.length > 6) p.trail.shift();
					for (var tr = 0; tr < p.trail.length; tr++) {
						var tp = p.trail[tr];
						ctx.beginPath();
						ctx.fillStyle = colorA;
						ctx.globalAlpha = (tr / p.trail.length) * 0.18 * p.z;
						ctx.arc(tp.x, tp.y, 1.2, 0, Math.PI * 2);
						ctx.fill();
					}
					ctx.globalAlpha = 1;
				}

				drawShape(p, i % 2 ? colorA : colorP, 0.22 + p.z * 0.45);
			}

			if (cfg.links) {
				ctx.lineWidth = 1;
				for (var a = 0; a < particles.length; a++) {
					for (var b = a + 1; b < a + 6 && b < particles.length; b++) {
						var pa = particles[a];
						var pb = particles[b];
						var dx = pa.x - pb.x;
						var dy = pa.y - pb.y;
						var dist = Math.sqrt(dx * dx + dy * dy);
						if (dist < 110) {
							ctx.strokeStyle = 'rgba(' + accent.r + ',' + accent.g + ',' + accent.b + ',' + ((1 - dist / 110) * 0.18) + ')';
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
			pointer.x = e.clientX / Math.max(w, 1);
			pointer.y = e.clientY / Math.max(h, 1);
		}

		resize();
		window.addEventListener('resize', resize, { passive: true });
		window.addEventListener('pointermove', onPointer, { passive: true });
		document.addEventListener(
			'visibilitychange',
			function () {
				running = document.visibilityState !== 'hidden';
			},
			false
		);
		tick(performance.now());
		return true;
	}

	function bindTilt(root) {
		var nodes = root.querySelectorAll(TILT_SEL);
		nodes.forEach(function (el) {
			el.addEventListener(
				'pointermove',
				function (e) {
					var rect = el.getBoundingClientRect();
					var px = (e.clientX - rect.left) / Math.max(rect.width, 1);
					var py = (e.clientY - rect.top) / Math.max(rect.height, 1);
					var rx = (0.5 - py) * 10;
					var ry = (px - 0.5) * 12;
					el.style.transform =
						'perspective(900px) rotateX(' +
						rx.toFixed(2) +
						'deg) rotateY(' +
						ry.toFixed(2) +
						'deg) translateZ(14px)';
				},
				{ passive: true }
			);
			el.addEventListener(
				'pointerleave',
				function () {
					el.style.transform = '';
				},
				{ passive: true }
			);
		});
	}

	function bindMagneticButtons(root) {
		var btns = root.querySelectorAll('.btn-hero, .header-btn--register, .mini-cart__checkout, .auth-form-submit');
		btns.forEach(function (btn) {
			btn.addEventListener(
				'pointermove',
				function (e) {
					var rect = btn.getBoundingClientRect();
					var x = e.clientX - (rect.left + rect.width / 2);
					var y = e.clientY - (rect.top + rect.height / 2);
					btn.style.transform = 'translate(' + (x * 0.12).toFixed(1) + 'px,' + (y * 0.16).toFixed(1) + 'px)';
				},
				{ passive: true }
			);
			btn.addEventListener(
				'pointerleave',
				function () {
					btn.style.transform = '';
				},
				{ passive: true }
			);
		});
	}

	function bindParallax(root) {
		var heroBg = root.querySelector('.ind-hero-bg');
		if (!heroBg) return;
		window.addEventListener(
			'scroll',
			function () {
				var y = window.scrollY || 0;
				if (y > window.innerHeight) return;
				heroBg.style.transform = 'scale(1.08) translateY(' + (y * 0.18).toFixed(1) + 'px)';
			},
			{ passive: true }
		);
	}

	function boot() {
		var root = document.querySelector(ROOT_SEL);
		if (!root) return;
		var motif = root.getAttribute('data-ind-motif') || 'default';
		if (prefersReducedMotion()) {
			root.classList.add('ind-premium-3d--reduced');
			return;
		}
		mountSprites(root, motif);
		mountCanvas(root, motif);
		bindTilt(root);
		bindMagneticButtons(root);
		bindParallax(root);
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
