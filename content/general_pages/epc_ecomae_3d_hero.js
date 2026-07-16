/**
 * ECOM AE premium WebGL hero — orbital glass stack around a luminous core.
 * Uses Three.js from CDN; falls back to CSS 3D stage when WebGL is unavailable.
 */
(function () {
	'use strict';

	var SECTION_SEL = '.epm-3d-hero';
	var CANVAS_SEL = '.epm-3d-hero__canvas';
	var THREE_CDN = 'https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.min.js';

	function prefersReducedMotion() {
		return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
	}

	function loadScript(src) {
		return new Promise(function (resolve, reject) {
			if (window.THREE) {
				resolve(window.THREE);
				return;
			}
			var s = document.createElement('script');
			s.src = src;
			s.async = true;
			s.onload = function () {
				if (window.THREE) resolve(window.THREE);
				else reject(new Error('THREE missing'));
			};
			s.onerror = function () {
				reject(new Error('THREE load failed'));
			};
			document.head.appendChild(s);
		});
	}

	function makeLabelTexture(THREE, title, sub) {
		var canvas = document.createElement('canvas');
		canvas.width = 512;
		canvas.height = 320;
		var ctx = canvas.getContext('2d');
		ctx.clearRect(0, 0, canvas.width, canvas.height);

		var grad = ctx.createLinearGradient(0, 0, canvas.width, canvas.height);
		grad.addColorStop(0, 'rgba(14, 165, 233, 0.42)');
		grad.addColorStop(1, 'rgba(8, 24, 42, 0.82)');
		roundRect(ctx, 16, 16, canvas.width - 32, canvas.height - 32, 36);
		ctx.fillStyle = grad;
		ctx.fill();
		ctx.strokeStyle = 'rgba(125, 211, 252, 0.55)';
		ctx.lineWidth = 3;
		ctx.stroke();

		ctx.fillStyle = '#f0f9ff';
		ctx.font = '700 54px Syne, DM Sans, sans-serif';
		ctx.textAlign = 'center';
		ctx.textBaseline = 'middle';
		ctx.fillText(title, canvas.width / 2, canvas.height / 2 - 28);
		ctx.fillStyle = 'rgba(186, 230, 253, 0.85)';
		ctx.font = '500 28px DM Sans, sans-serif';
		ctx.fillText(sub, canvas.width / 2, canvas.height / 2 + 36);

		var tex = new THREE.CanvasTexture(canvas);
		if (THREE.SRGBColorSpace) tex.colorSpace = THREE.SRGBColorSpace;
		else if (THREE.sRGBEncoding !== undefined) tex.encoding = THREE.sRGBEncoding;
		tex.anisotropy = 4;
		return tex;
	}

	function roundRect(ctx, x, y, w, h, r) {
		ctx.beginPath();
		ctx.moveTo(x + r, y);
		ctx.arcTo(x + w, y, x + w, y + h, r);
		ctx.arcTo(x + w, y + h, x, y + h, r);
		ctx.arcTo(x, y + h, x, y, r);
		ctx.arcTo(x, y, x + w, y, r);
		ctx.closePath();
	}

	function makeGlassPanel(THREE, title, sub, w, h) {
		var group = new THREE.Group();
		var geo = new THREE.BoxGeometry(w, h, 0.12);
		var mat = new THREE.MeshStandardMaterial({
			color: 0x0b3a5a,
			metalness: 0.55,
			roughness: 0.22,
			transparent: true,
			opacity: 0.78,
			emissive: 0x0284c7,
			emissiveIntensity: 0.18
		});
		var mesh = new THREE.Mesh(geo, mat);
		group.add(mesh);

		var labelMat = new THREE.MeshBasicMaterial({
			map: makeLabelTexture(THREE, title, sub),
			transparent: true,
			depthWrite: false
		});
		var label = new THREE.Mesh(new THREE.PlaneGeometry(w * 0.94, h * 0.94), labelMat);
		label.position.z = 0.07;
		group.add(label);

		var edge = new THREE.Mesh(
			new THREE.BoxGeometry(w * 1.04, h * 1.04, 0.04),
			new THREE.MeshBasicMaterial({
				color: 0x7dd3fc,
				transparent: true,
				opacity: 0.22
			})
		);
		edge.position.z = -0.02;
		group.add(edge);
		return group;
	}

	function makeCore(THREE) {
		var group = new THREE.Group();
		var geo = new THREE.IcosahedronGeometry(1.15, 1);
		var mat = new THREE.MeshStandardMaterial({
			color: 0x38bdf8,
			emissive: 0x0284c7,
			emissiveIntensity: 0.7,
			metalness: 0.72,
			roughness: 0.16,
			transparent: true,
			opacity: 0.96
		});
		var core = new THREE.Mesh(geo, mat);
		group.add(core);

		var wire = new THREE.Mesh(
			new THREE.IcosahedronGeometry(1.35, 1),
			new THREE.MeshBasicMaterial({
				color: 0x5eead4,
				wireframe: true,
				transparent: true,
				opacity: 0.28
			})
		);
		group.add(wire);

		var glow = new THREE.Mesh(
			new THREE.SphereGeometry(1.9, 32, 32),
			new THREE.MeshBasicMaterial({
				color: 0x0ea5e9,
				transparent: true,
				opacity: 0.08,
				depthWrite: false
			})
		);
		group.add(glow);

		group.userData.core = core;
		group.userData.wire = wire;
		return group;
	}

	function makeParticles(THREE, count) {
		var positions = new Float32Array(count * 3);
		var speeds = new Float32Array(count);
		for (var i = 0; i < count; i++) {
			var r = 2.2 + Math.random() * 5.5;
			var a = Math.random() * Math.PI * 2;
			var y = (Math.random() - 0.5) * 4.2;
			positions[i * 3] = Math.cos(a) * r;
			positions[i * 3 + 1] = y;
			positions[i * 3 + 2] = Math.sin(a) * r;
			speeds[i] = 0.15 + Math.random() * 0.45;
		}
		var geo = new THREE.BufferGeometry();
		geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
		var mat = new THREE.PointsMaterial({
			color: 0x7dd3fc,
			size: 0.035,
			transparent: true,
			opacity: 0.85,
			depthWrite: false,
			blending: THREE.AdditiveBlending
		});
		var points = new THREE.Points(geo, mat);
		points.userData.speeds = speeds;
		return points;
	}

	function makeOrbitRing(THREE, radius) {
		var geo = new THREE.TorusGeometry(radius, 0.012, 12, 120);
		var mat = new THREE.MeshBasicMaterial({
			color: 0x38bdf8,
			transparent: true,
			opacity: 0.22
		});
		var ring = new THREE.Mesh(geo, mat);
		ring.rotation.x = Math.PI / 2.15;
		return ring;
	}

	function initScene(section, THREE) {
		var canvas = section.querySelector(CANVAS_SEL);
		if (!canvas) return null;

		var renderer;
		try {
			renderer = new THREE.WebGLRenderer({
				canvas: canvas,
				antialias: true,
				alpha: true,
				powerPreference: 'high-performance'
			});
		} catch (e) {
			return null;
		}
		if (!renderer.getContext()) return null;

		renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
		renderer.setClearColor(0x000000, 0);
		if (THREE.SRGBColorSpace) renderer.outputColorSpace = THREE.SRGBColorSpace;

		var scene = new THREE.Scene();
		scene.fog = new THREE.FogExp2(0x020814, 0.045);

		var camera = new THREE.PerspectiveCamera(42, 1, 0.1, 80);
		camera.position.set(0.2, 0.8, 9.2);

		var amb = new THREE.AmbientLight(0x9ec9e8, 0.55);
		scene.add(amb);
		var key = new THREE.DirectionalLight(0x7dd3fc, 1.35);
		key.position.set(4, 6, 5);
		scene.add(key);
		var rim = new THREE.DirectionalLight(0x5eead4, 0.7);
		rim.position.set(-5, 2, -3);
		scene.add(rim);
		var point = new THREE.PointLight(0x38bdf8, 1.8, 18);
		point.position.set(0, 0.4, 0);
		scene.add(point);

		var root = new THREE.Group();
		root.position.set(1.6, 0.15, 0);
		scene.add(root);

		var core = makeCore(THREE);
		root.add(core);

		root.add(makeOrbitRing(THREE, 3.1));
		root.add(makeOrbitRing(THREE, 4.35));

		var panels = [
			{ title: 'Storefront', sub: 'Commerce', pos: [-3.2, 0.9, 0.6], rot: [0.1, 0.45, 0] },
			{ title: 'Control Panel', sub: 'Operations', pos: [3.0, 1.1, -0.4], rot: [0.05, -0.5, 0] },
			{ title: 'ERP + CRM', sub: 'Finance', pos: [-2.4, -1.4, 1.1], rot: [-0.08, 0.35, 0] },
			{ title: 'Super CP', sub: 'Multi-tenant', pos: [2.7, -1.2, 0.8], rot: [0.12, -0.4, 0] }
		];
		var panelMeshes = [];
		for (var i = 0; i < panels.length; i++) {
			var p = panels[i];
			var panel = makeGlassPanel(THREE, p.title, p.sub, 2.35, 1.4);
			panel.position.set(p.pos[0], p.pos[1], p.pos[2]);
			panel.rotation.set(p.rot[0], p.rot[1], p.rot[2]);
			panel.userData.base = { x: p.pos[0], y: p.pos[1], z: p.pos[2], phase: i * 1.3 };
			root.add(panel);
			panelMeshes.push(panel);
		}

		var particles = makeParticles(THREE, 420);
		root.add(particles);

		var grid = new THREE.GridHelper(18, 28, 0x0ea5e9, 0x12324a);
		grid.position.y = -2.6;
		grid.material.transparent = true;
		grid.material.opacity = 0.22;
		root.add(grid);

		var pointer = { x: 0, y: 0, tx: 0, ty: 0 };
		function onPointer(e) {
			var rect = section.getBoundingClientRect();
			var nx = ((e.clientX - rect.left) / Math.max(rect.width, 1)) * 2 - 1;
			var ny = ((e.clientY - rect.top) / Math.max(rect.height, 1)) * 2 - 1;
			pointer.tx = Math.max(-1, Math.min(1, nx));
			pointer.ty = Math.max(-1, Math.min(1, ny));
		}
		window.addEventListener('pointermove', onPointer, { passive: true });

		function resize() {
			var w = section.clientWidth || window.innerWidth;
			var h = section.clientHeight || window.innerHeight;
			renderer.setSize(w, h, false);
			camera.aspect = w / Math.max(h, 1);
			camera.updateProjectionMatrix();
			var narrow = w < 900;
			root.position.set(narrow ? 0 : 1.6, narrow ? -1.35 : 0.15, 0);
			camera.position.set(0, narrow ? 0.15 : 0.8, narrow ? 11.2 : 9.2);
		}
		resize();
		window.addEventListener('resize', resize, { passive: true });

		var clock = new THREE.Clock();
		var running = true;
		var raf = 0;

		if ('IntersectionObserver' in window) {
			var io = new IntersectionObserver(
				function (entries) {
					running = entries[0] && entries[0].isIntersecting;
				},
				{ threshold: 0.05 }
			);
			io.observe(section);
		}

		function tick() {
			raf = requestAnimationFrame(tick);
			if (!running) return;
			var t = clock.getElapsedTime();
			pointer.x += (pointer.tx - pointer.x) * 0.05;
			pointer.y += (pointer.ty - pointer.y) * 0.05;

			root.rotation.y = t * 0.08 + pointer.x * 0.18;
			root.rotation.x = pointer.y * -0.1;
			core.rotation.y = t * 0.35;
			core.rotation.x = Math.sin(t * 0.4) * 0.15;
			if (core.userData.wire) core.userData.wire.rotation.y = -t * 0.22;
			core.position.y = Math.sin(t * 0.9) * 0.12;

			for (var i = 0; i < panelMeshes.length; i++) {
				var panel = panelMeshes[i];
				var b = panel.userData.base;
				panel.position.y = b.y + Math.sin(t * 0.85 + b.phase) * 0.18;
				panel.position.x = b.x + Math.cos(t * 0.55 + b.phase) * 0.08;
				panel.rotation.z = Math.sin(t * 0.4 + b.phase) * 0.04;
			}

			var pos = particles.geometry.attributes.position.array;
			var speeds = particles.userData.speeds;
			for (var p = 0; p < speeds.length; p++) {
				var ix = p * 3;
				var ang = Math.atan2(pos[ix + 2], pos[ix]) + speeds[p] * 0.01;
				var rad = Math.sqrt(pos[ix] * pos[ix] + pos[ix + 2] * pos[ix + 2]);
				pos[ix] = Math.cos(ang) * rad;
				pos[ix + 2] = Math.sin(ang) * rad;
				pos[ix + 1] += Math.sin(t + p) * 0.0015;
			}
			particles.geometry.attributes.position.needsUpdate = true;

			camera.lookAt(root.position.x * 0.35, 0.1, 0);
			renderer.render(scene, camera);
		}
		tick();

		return {
			dispose: function () {
				cancelAnimationFrame(raf);
				window.removeEventListener('pointermove', onPointer);
				window.removeEventListener('resize', resize);
				renderer.dispose();
			}
		};
	}

	function boot() {
		var section = document.querySelector(SECTION_SEL);
		if (!section) return;

		if (prefersReducedMotion()) {
			section.classList.add('is-fallback');
			return;
		}

		loadScript(THREE_CDN)
			.then(function (THREE) {
				var handle = initScene(section, THREE);
				if (!handle) {
					section.classList.add('is-fallback');
					return;
				}
				section.classList.add('is-webgl');
			})
			.catch(function () {
				section.classList.add('is-fallback');
			});
	}

	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', boot);
	} else {
		boot();
	}
})();
