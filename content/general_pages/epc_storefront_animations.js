/* Enhanced storefront animations — scroll reveals, particles, counters */
(function(){
'use strict';

/* ── Scroll-triggered reveal ──────────────────────────────────────── */
function initReveal(){
	var els = document.querySelectorAll('.epc-reveal, .epc-jrk-reveal');
	if(!els.length) return;
	if(!('IntersectionObserver' in window)){
		for(var i=0;i<els.length;i++) els[i].classList.add('is-visible');
		return;
	}
	var obs = new IntersectionObserver(function(entries){
		entries.forEach(function(e){
			if(e.isIntersecting){
				e.target.classList.add('is-visible');
				obs.unobserve(e.target);
			}
		});
	},{threshold:0.12,rootMargin:'0px 0px -40px 0px'});
	for(var j=0;j<els.length;j++) obs.observe(els[j]);
}

/* ── Floating particles ───────────────────────────────────────────── */
function initParticles(){
	var containers = document.querySelectorAll('.epc-particles');
	for(var i=0;i<containers.length;i++){
		var c = containers[i];
		var color = c.getAttribute('data-color') || 'rgba(255,255,255,.5)';
		var count = parseInt(c.getAttribute('data-count') || '15', 10);
		for(var j=0;j<count;j++){
			var p = document.createElement('span');
			p.className = 'epc-particle';
			var size = 3 + Math.random() * 5;
			p.style.width = size + 'px';
			p.style.height = size + 'px';
			p.style.background = color;
			p.style.left = Math.random() * 100 + '%';
			p.style.bottom = -(Math.random() * 20) + '%';
			p.style.animationDuration = (8 + Math.random() * 12) + 's';
			p.style.animationDelay = Math.random() * 10 + 's';
			c.appendChild(p);
		}
	}
}

/* ── Count-up animation ───────────────────────────────────────────── */
function initCountUp(){
	var els = document.querySelectorAll('.epc-count-up[data-target]');
	if(!els.length) return;
	var obs = new IntersectionObserver(function(entries){
		entries.forEach(function(e){
			if(e.isIntersecting){
				animateCount(e.target);
				obs.unobserve(e.target);
			}
		});
	},{threshold:0.5});
	for(var i=0;i<els.length;i++) obs.observe(els[i]);
}
function animateCount(el){
	var target = parseInt(el.getAttribute('data-target'),10);
	var suffix = el.getAttribute('data-suffix') || '';
	var prefix = el.getAttribute('data-prefix') || '';
	var duration = 1500;
	var start = performance.now();
	function step(now){
		var progress = Math.min((now - start) / duration, 1);
		var eased = 1 - Math.pow(1 - progress, 3);
		el.textContent = prefix + Math.floor(target * eased).toLocaleString() + suffix;
		if(progress < 1) requestAnimationFrame(step);
	}
	requestAnimationFrame(step);
}

/* ── Parallax on hero sections ────────────────────────────────────── */
function initParallax(){
	var heroes = document.querySelectorAll('.epc-parallax');
	if(!heroes.length) return;
	var ticking = false;
	function onScroll(){
		if(ticking) return;
		ticking = true;
		requestAnimationFrame(function(){
			var scrollY = window.pageYOffset || document.documentElement.scrollTop;
			for(var i=0;i<heroes.length;i++){
				var rect = heroes[i].getBoundingClientRect();
				if(rect.bottom > 0 && rect.top < window.innerHeight){
					var offset = scrollY * 0.3;
					heroes[i].style.transform = 'translateY(' + offset + 'px)';
				}
			}
			ticking = false;
		});
	}
	window.addEventListener('scroll', onScroll, {passive:true});
}

/* ── Product card tilt on hover ───────────────────────────────────── */
function initCardTilt(){
	var cards = document.querySelectorAll('.epc-card-tilt');
	for(var i=0;i<cards.length;i++){
		(function(card){
			card.addEventListener('mousemove',function(e){
				var rect = card.getBoundingClientRect();
				var x = (e.clientX - rect.left) / rect.width - 0.5;
				var y = (e.clientY - rect.top) / rect.height - 0.5;
				card.style.transform = 'perspective(600px) rotateX(' + (-y * 6) + 'deg) rotateY(' + (x * 6) + 'deg) scale(1.02)';
			});
			card.addEventListener('mouseleave',function(){
				card.style.transform = '';
			});
		})(cards[i]);
	}
}

/* ── Smooth hero text typing effect ───────────────────────────────── */
function initTyping(){
	var els = document.querySelectorAll('.epc-type-text');
	for(var i=0;i<els.length;i++){
		(function(el){
			var text = el.textContent;
			el.textContent = '';
			el.style.visibility = 'visible';
			var idx = 0;
			var timer = setInterval(function(){
				el.textContent = text.substring(0, ++idx);
				if(idx >= text.length) clearInterval(timer);
			}, 35);
		})(els[i]);
	}
}

/* ── Init all ─────────────────────────────────────────────────────── */
function init(){
	initReveal();
	initParticles();
	initCountUp();
	initParallax();
	initCardTilt();
	initTyping();
}

if(document.readyState === 'loading'){
	document.addEventListener('DOMContentLoaded', init);
} else {
	init();
}
})();
