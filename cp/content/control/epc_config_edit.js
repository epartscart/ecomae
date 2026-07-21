/**
 * CP Settings — sticky group nav active state + smooth scroll.
 */
(function () {
	'use strict';
	var root = document.querySelector('.epc-cfg');
	if (!root) {
		return;
	}
	var links = root.querySelectorAll('.epc-cfg-nav a[href^="#"]');
	var sections = [];
	for (var i = 0; i < links.length; i++) {
		var href = links[i].getAttribute('href') || '';
		var id = href.charAt(0) === '#' ? href.slice(1) : '';
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
			var href = this.getAttribute('href') || '';
			var id = href.charAt(0) === '#' ? href.slice(1) : '';
			var target = id ? document.getElementById(id) : null;
			if (!target) {
				return;
			}
			e.preventDefault();
			target.scrollIntoView({ behavior: 'smooth', block: 'start' });
			setActive(this);
			if (history && history.replaceState) {
				history.replaceState(null, '', href);
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
