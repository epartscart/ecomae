/**
 * CP Settings — sticky group nav active state + smooth scroll.
 * CP shell scrolls inside .content (not window), so target that pane.
 */
(function () {
	'use strict';
	var root = document.querySelector('.epc-cfg');
	if (!root) {
		return;
	}
	var links = root.querySelectorAll('.epc-cfg-nav a[href*="#epc-cfg-group-"]');
	var sections = [];

	function hashId(href) {
		var idx = (href || '').indexOf('#');
		return idx >= 0 ? href.slice(idx + 1) : '';
	}

	function scrollParent(el) {
		var p = el.parentElement;
		while (p && p !== document.body) {
			var st = window.getComputedStyle(p);
			var oy = st.overflowY;
			if ((oy === 'auto' || oy === 'scroll' || oy === 'overlay') && p.scrollHeight > p.clientHeight + 4) {
				return p;
			}
			p = p.parentElement;
		}
		return document.querySelector('#wrapper .content') || document.scrollingElement || document.documentElement;
	}

	function scrollToEl(el) {
		var pane = scrollParent(el);
		if (!pane || pane === document.body || pane === document.documentElement || pane === document.scrollingElement) {
			el.scrollIntoView({ behavior: 'smooth', block: 'start' });
			return;
		}
		var top = el.getBoundingClientRect().top - pane.getBoundingClientRect().top + pane.scrollTop - 16;
		if (typeof pane.scrollTo === 'function') {
			pane.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
		} else {
			pane.scrollTop = Math.max(0, top);
		}
	}

	for (var i = 0; i < links.length; i++) {
		var id = hashId(links[i].getAttribute('href') || '');
		var el = id ? document.getElementById(id) : null;
		if (el) {
			sections.push({ link: links[i], el: el });
		}
	}
	if (!sections.length) {
		return;
	}

	function setActive(activeLink) {
		for (var i = 0; i < links.length; i++) {
			links[i].classList.toggle('is-active', links[i] === activeLink);
		}
	}

	for (var j = 0; j < links.length; j++) {
		links[j].addEventListener('click', function (e) {
			var id = hashId(this.getAttribute('href') || '');
			var target = id ? document.getElementById(id) : null;
			if (!target) {
				return;
			}
			e.preventDefault();
			scrollToEl(target);
			setActive(this);
			if (history && history.replaceState) {
				history.replaceState(null, '', '#' + id);
			}
		});
	}

	var pane = scrollParent(sections[0].el);

	function onScroll() {
		var base = pane && pane.getBoundingClientRect ? pane.getBoundingClientRect().top : 0;
		var focusY = (pane && typeof pane.scrollTop === 'number' ? pane.scrollTop : window.scrollY) + 120;
		var current = sections[0].link;
		for (var i = 0; i < sections.length; i++) {
			var top = sections[i].el.getBoundingClientRect().top - base + (pane.scrollTop || 0);
			if (top <= focusY) {
				current = sections[i].link;
			}
		}
		setActive(current);
	}

	if (pane && pane.addEventListener) {
		pane.addEventListener('scroll', onScroll, { passive: true });
	}
	window.addEventListener('scroll', onScroll, { passive: true });
	onScroll();

	var bootHash = hashId(window.location.hash || '');
	if (bootHash) {
		var bootEl = document.getElementById(bootHash);
		if (bootEl) {
			setTimeout(function () {
				scrollToEl(bootEl);
			}, 50);
		}
	}
})();
