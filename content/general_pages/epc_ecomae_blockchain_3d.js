/**
 * Premium 3D for /blockchain — particle field, floating proof blocks, tilt surfaces.
 */
(function () {
	'use strict';

	var ROOT = '#ebc-page.ebc-page--3d';
	var TILT_SEL = ['.ebc-layer', '.ebc-step', '.ebc-mode', '.ebc-facts li', '.ebc-surfaces__aside'].join(',');

	function prefersReducedMotion() {
		return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	}

	function mountCanvas(root) {
		var canvas = document.createElement('canvas');
		canvas.className = 'ebc-3d-canvas';
		canvas.setAttribute('aria-hidden', 'true');
		root.insertBefore(canvas, root.firstChild);
		var ctx = canvas.getContext('2d');
		if (!ctx) return null;

		var dpr = Math.min(window.devicePixelRatio || 1, 2);
		var w = 0;
		var h = 0;
		var particles = [];
		var blocks = [];
		var running = true;
		var raf = 0;
		var t0 = performance.now();
		var pointer = { x: 0.55, y: 0.35 };

		function resize() {
			w = root.clientWidth || window.innerWidth;
			h = Math.max(root.scrollHeight, root.clientHeight, 900);
			canvas.width = Math.floor(w * dpr);
			canvas.height = Math.floor(h * dpr);
			canvas.style.width = w + 'px';
			canvas.style.height = h + 'px';
			ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
			seed();
		}

		function seed() {
			var count = Math.min(110, Math.max(42, Math.floor((w * h) / 24000)));
			particles = [];
			for (var i = 0; i < count; i++) {
				particles.push({
					x: Math.random() * w,
					y: Math.random() * h,
					z: 0.2 + Math.random() * 0.8,
					vx: (Math.random() - 0.5) * 0.22,
					vy: (Math.random() - 0.5) * 0.14,
					r: 0.7 + Math.random() * 1.9
				});
			}
			blocks = [];
			var bc = Math.min(18, Math.max(8, Math.floor(w / 90)));
			for (var j = 0; j < bc; j++) {
				blocks.push({
					x: Math.random() * w,
					y: Math.random() * h,
					z: 0.35 + Math.random() * 0.65,
					size: 10 + Math.random() * 18,
					rot: Math.random() * Math.PI,
					spin: (Math.random() - 0.5) * 0.01,
					phase: Math.random() * Math.PI * 2
				});
			}
		}

		function drawPerspectiveGrid() {
			var spacing = 78;
			var ox = (pointer.x - 0.5) * 28;
			var oy = (pointer.y - 0.5) * 18;
			ctx.save();
			ctx.strokeStyle = 'rgba(34,211,238,0.05)';
			ctx.lineWidth = 1;
			ctx.beginPath();
			for (var x = -spacing; x < w + spacing; x += spacing) {
				var gx = x + ox;
				ctx.moveTo(gx, 0);
				ctx.lineTo(gx + (pointer.x - 0.5) * 48, h);
			}
			for (var y = -spacing; y < h + spacing; y += spacing) {
				var gy = y + oy;
				ctx.moveTo(0, gy);
				ctx.lineTo(w, gy + (pointer.y - 0.5) * 24);
			}
			ctx.stroke();
			ctx.restore();
		}

		function drawMerkleCue(now) {
			var cx = w * (0.58 + (pointer.x - 0.5) * 0.04);
			var cy = Math.min(h * 0.22, 220) + Math.sin(now * 0.0006) * 8;
			var nodes = [
				{ x: cx, y: cy },
				{ x: cx - 70, y: cy + 70 },
				{ x: cx + 70, y: cy + 70 },
				{ x: cx - 110, y: cy + 140 },
				{ x: cx - 30, y: cy + 140 },
				{ x: cx + 30, y: cy + 140 },
				{ x: cx + 110, y: cy + 140 }
			];
			var edges = [
				[0, 1],
				[0, 2],
				[1, 3],
				[1, 4],
				[2, 5],
				[2, 6]
			];
			ctx.save();
			ctx.globalAlpha = 0.55;
			ctx.strokeStyle = 'rgba(45,212,191,0.35)';
			ctx.lineWidth = 1.2;
			for (var e = 0; e < edges.length; e++) {
				var a = nodes[edges[e][0]];
				var b = nodes[edges[e][1]];
				ctx.beginPath();
				ctx.moveTo(a.x, a.y);
				ctx.lineTo(b.x, b.y);
				ctx.stroke();
			}
			for (var n = 0; n < nodes.length; n++) {
				var nd = nodes[n];
				var pulse = 0.45 + 0.25 * Math.sin(now * 0.003 + n);
				ctx.beginPath();
				ctx.fillStyle = n === 0 ? 'rgba(34,211,238,' + pulse + ')' : 'rgba(125,211,252,' + (pulse * 0.75) + ')';
				ctx.arc(nd.x, nd.y, n === 0 ? 5.5 : 3.5, 0, Math.PI * 2);
				ctx.fill();
			}
			ctx.restore();
		}

		function drawBlock(b, now) {
			var bob = Math.sin(now * 0.0012 + b.phase) * 10 * b.z;
			var x = b.x + (pointer.x - 0.5) * 20 * b.z;
			var y = b.y + bob + (pointer.y - 0.5) * 12 * b.z;
			var s = b.size * b.z;
			var rot = b.rot + now * b.spin;
			ctx.save();
			ctx.translate(x, y);
			ctx.rotate(rot * 0.15);
			ctx.globalAlpha = 0.25 + b.z * 0.45;
			ctx.strokeStyle = 'rgba(34,211,238,0.55)';
			ctx.fillStyle = 'rgba(14,165,233,0.08)';
			ctx.lineWidth = 1.2;
			ctx.beginPath();
			ctx.rect(-s / 2, -s / 2, s, s);
			ctx.fill();
			ctx.stroke();
			// hash-line cue
			ctx.strokeStyle = 'rgba(45,212,191,0.4)';
			ctx.beginPath();
			ctx.moveTo(-s * 0.28, -s * 0.1);
			ctx.lineTo(s * 0.28, -s * 0.1);
			ctx.moveTo(-s * 0.2, s * 0.12);
			ctx.lineTo(s * 0.2, s * 0.12);
			ctx.stroke();
			ctx.restore();
		}

		function tick(now) {
			raf = requestAnimationFrame(tick);
			if (!running) return;
			now = now || performance.now();
			ctx.clearRect(0, 0, w, h);
			drawPerspectiveGrid();
			drawMerkleCue(now);

			var px = pointer.x * w;
			var py = pointer.y * Math.min(h, window.innerHeight + window.scrollY);
			var links = [];

			for (var i = 0; i < particles.length; i++) {
				var p = particles[i];
				p.x += p.vx + (pointer.x - 0.5) * 0.05 * p.z;
				p.y += p.vy + (pointer.y - 0.5) * 0.035 * p.z;
				if (p.x < -20) p.x = w + 20;
				if (p.x > w + 20) p.x = -20;
				if (p.y < -20) p.y = h + 20;
				if (p.y > h + 20) p.y = -20;

				ctx.beginPath();
				ctx.fillStyle = 'rgba(125,211,252,' + (0.22 + p.z * 0.5) + ')';
				ctx.arc(p.x, p.y, p.r * p.z * 1.35, 0, Math.PI * 2);
				ctx.fill();

				var dx = p.x - px;
				var dy = p.y - py;
				if (dx * dx + dy * dy < 150 * 150) links.push(p);
			}

			ctx.strokeStyle = 'rgba(34,211,238,0.14)';
			ctx.lineWidth = 1;
			for (var a = 0; a < links.length; a++) {
				for (var b = a + 1; b < links.length; b++) {
					var pa = links[a];
					var pb = links[b];
					var ddx = pa.x - pb.x;
					var ddy = pa.y - pb.y;
					var dist = Math.sqrt(ddx * ddx + ddy * ddy);
					if (dist < 130) {
						ctx.globalAlpha = (1 - dist / 130) * 0.6;
						ctx.beginPath();
						ctx.moveTo(pa.x, pa.y);
						ctx.lineTo(pb.x, pb.y);
						ctx.stroke();
					}
				}
			}
			ctx.globalAlpha = 1;

			for (var k = 0; k < blocks.length; k++) {
				drawBlock(blocks[k], now);
			}

			// chain pulse line across mid-hero
			var chainY = Math.min(h * 0.28, 260);
			var grad = ctx.createLinearGradient(0, chainY, w, chainY);
			grad.addColorStop(0, 'rgba(34,211,238,0)');
			grad.addColorStop(0.5, 'rgba(45,212,191,0.18)');
			grad.addColorStop(1, 'rgba(34,211,238,0)');
			ctx.strokeStyle = grad;
			ctx.lineWidth = 2;
			ctx.beginPath();
			ctx.moveTo(w * 0.08, chainY + Math.sin(now * 0.002) * 4);
			for (var sx = 0.12; sx <= 0.92; sx += 0.04) {
				ctx.lineTo(w * sx, chainY + Math.sin(now * 0.002 + sx * 12) * 6);
			}
			ctx.stroke();
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

		tick(t0);
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
					var rx = (0.5 - py) * 7;
					var ry = (px - 0.5) * 9;
					el.style.transform =
						'perspective(900px) rotateX(' +
						rx.toFixed(2) +
						'deg) rotateY(' +
						ry.toFixed(2) +
						'deg) translateZ(12px)';
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
