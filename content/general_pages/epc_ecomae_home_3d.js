/**
 * Premium 3D depth for ecomae.com homepage sections (below the existing hero).
 * Ambient particle field + pointer tilt on interactive surfaces.
 */
(function () {
	'use strict';

	var ROOT = '#ehm-home-sections.ehm-home--3d';
	var TILT_SEL = [
		'.ehm-fc',
		'.ehm-layer',
		'.ehm-ind-card',
		'.ehm-ai-card',
		'.ehm-comp-card',
		'.ehm-client-card',
		'.ehm-cp-feat',
		'.ehm-sf-feat',
		'.ehm-tenant-card',
		'.ehm-ss',
		'.ehm-bc-graphic',
		'.ehm-bc-node',
		'.ehm-bc-plane'
	].join(',');

	function prefersReducedMotion() {
		return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	}

	function mountCanvas(root) {
		var canvas = document.createElement('canvas');
		canvas.className = 'ehm-3d-canvas';
		canvas.setAttribute('aria-hidden', 'true');
		root.insertBefore(canvas, root.firstChild);
		var ctx = canvas.getContext('2d');
		if (!ctx) return null;

		var dpr = Math.min(window.devicePixelRatio || 1, 2);
		var w = 0;
		var h = 0;
		var particles = [];
		var links = [];
		var running = true;
		var raf = 0;
		var pointer = { x: 0.5, y: 0.35 };

		function resize() {
			w = root.clientWidth || window.innerWidth;
			h = Math.max(root.scrollHeight, root.clientHeight, 800);
			canvas.width = Math.floor(w * dpr);
			canvas.height = Math.floor(h * dpr);
			canvas.style.width = w + 'px';
			canvas.style.height = h + 'px';
			ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
			seed();
		}

		function seed() {
			var count = Math.min(90, Math.max(36, Math.floor((w * h) / 28000)));
			particles = [];
			for (var i = 0; i < count; i++) {
				particles.push({
					x: Math.random() * w,
					y: Math.random() * h,
					z: 0.25 + Math.random() * 0.75,
					vx: (Math.random() - 0.5) * 0.18,
					vy: (Math.random() - 0.5) * 0.12,
					r: 0.8 + Math.random() * 1.8
				});
			}
		}

		function drawGrid() {
			var spacing = 72;
			var offsetX = (pointer.x - 0.5) * 24;
			var offsetY = (pointer.y - 0.5) * 16;
			ctx.save();
			ctx.strokeStyle = 'rgba(0,204,255,0.045)';
			ctx.lineWidth = 1;
			ctx.beginPath();
			for (var x = -spacing; x < w + spacing; x += spacing) {
				var gx = x + offsetX;
				ctx.moveTo(gx, 0);
				ctx.lineTo(gx + (pointer.x - 0.5) * 40, h);
			}
			for (var y = -spacing; y < h + spacing; y += spacing) {
				var gy = y + offsetY;
				ctx.moveTo(0, gy);
				ctx.lineTo(w, gy + (pointer.y - 0.5) * 20);
			}
			ctx.stroke();
			ctx.restore();
		}

		function tick() {
			raf = requestAnimationFrame(tick);
			if (!running) return;
			ctx.clearRect(0, 0, w, h);
			drawGrid();

			var px = pointer.x * w;
			var py = pointer.y * Math.min(h, window.innerHeight + window.scrollY);

			links = [];
			for (var i = 0; i < particles.length; i++) {
				var p = particles[i];
				p.x += p.vx + (pointer.x - 0.5) * 0.04 * p.z;
				p.y += p.vy + (pointer.y - 0.5) * 0.03 * p.z;
				if (p.x < -20) p.x = w + 20;
				if (p.x > w + 20) p.x = -20;
				if (p.y < -20) p.y = h + 20;
				if (p.y > h + 20) p.y = -20;

				var glow = 0.35 + p.z * 0.55;
				ctx.beginPath();
				ctx.fillStyle = 'rgba(125,211,252,' + (0.25 + p.z * 0.45) + ')';
				ctx.arc(p.x, p.y, p.r * p.z * 1.4, 0, Math.PI * 2);
				ctx.fill();

				var dx = p.x - px;
				var dy = p.y - py;
				if (dx * dx + dy * dy < 140 * 140) {
					links.push(p);
				}
			}

			ctx.strokeStyle = 'rgba(0,204,255,0.12)';
			ctx.lineWidth = 1;
			for (var a = 0; a < links.length; a++) {
				for (var b = a + 1; b < links.length; b++) {
					var pa = links[a];
					var pb = links[b];
					var ddx = pa.x - pb.x;
					var ddy = pa.y - pb.y;
					var dist = Math.sqrt(ddx * ddx + ddy * ddy);
					if (dist < 120) {
						ctx.globalAlpha = (1 - dist / 120) * 0.55;
						ctx.beginPath();
						ctx.moveTo(pa.x, pa.y);
						ctx.lineTo(pb.x, pb.y);
						ctx.stroke();
					}
				}
			}
			ctx.globalAlpha = 1;

			// soft horizon plane cue
			var horizon = h * 0.72;
			var grad = ctx.createLinearGradient(0, horizon - 80, 0, horizon + 120);
			grad.addColorStop(0, 'rgba(0,204,255,0)');
			grad.addColorStop(0.5, 'rgba(0,204,255,0.05)');
			grad.addColorStop(1, 'rgba(0,255,178,0)');
			ctx.fillStyle = grad;
			ctx.fillRect(0, horizon - 80, w, 200);
		}

		function onPointer(e) {
			var rect = root.getBoundingClientRect();
			pointer.x = (e.clientX - rect.left) / Math.max(rect.width, 1);
			pointer.y = (e.clientY - rect.top + root.scrollTop) / Math.max(h, 1);
			pointer.x = Math.max(0, Math.min(1, pointer.x));
			pointer.y = Math.max(0, Math.min(1, pointer.y));
		}

		resize();
		window.addEventListener('resize', resize, { passive: true });
		window.addEventListener('pointermove', onPointer, { passive: true });

		if ('IntersectionObserver' in window) {
			var io = new IntersectionObserver(
				function (entries) {
					running = !!(entries[0] && entries[0].isIntersecting);
				},
				{ threshold: 0.02 }
			);
			io.observe(root);
		}

		tick();
		return {
			dispose: function () {
				cancelAnimationFrame(raf);
				window.removeEventListener('resize', resize);
				window.removeEventListener('pointermove', onPointer);
				if (canvas.parentNode) canvas.parentNode.removeChild(canvas);
			}
		};
	}

	function bindTilt(root) {
		var nodes = root.querySelectorAll(TILT_SEL);
		if (!nodes.length) return;

		nodes.forEach(function (el) {
			el.addEventListener(
				'pointermove',
				function (e) {
					var rect = el.getBoundingClientRect();
					var px = (e.clientX - rect.left) / Math.max(rect.width, 1);
					var py = (e.clientY - rect.top) / Math.max(rect.height, 1);
					var rx = (0.5 - py) * 8;
					var ry = (px - 0.5) * 10;
					el.style.transform =
						'perspective(900px) rotateX(' +
						rx.toFixed(2) +
						'deg) rotateY(' +
						ry.toFixed(2) +
						'deg) translateZ(10px)';
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

	function boot() {
		var root = document.querySelector(ROOT);
		if (!root) return;
		if (prefersReducedMotion()) return;
		mountCanvas(root);
		bindTilt(root);
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
