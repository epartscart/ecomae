/**
 * CP Settings — sticky group nav active state + smooth scroll.
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
			target.scrollIntoView({ behavior: 'smooth', block: 'start' });
			setActive(this);
			if (history && history.replaceState) {
				history.replaceState(null, '', '#' + id);
			}
		});
	}

	function onScroll() {
		var focusY = window.scrollY + 120;
		var current = sections[0].link;
		for (var i = 0; i < sections.length; i++) {
			if (sections[i].el.offsetTop <= focusY) {
				current = sections[i].link;
			}
		}
		setActive(current);
	}

	window.addEventListener('scroll', onScroll, { passive: true });
	onScroll();
})();
