function log_my_log_error(e){
	//console.log(e);
}

//Фиксация шапки при прокрутке
$(document).ready(function() {
	var StickyElement = function(node){
		if($("div").is(".sticky-anchor")){
			var doc = $(document), 
			fixed = false,
			anchor = node.find('.sticky-anchor'),
			content = node.find('.schearch-line'); 
			var onScroll = function(e){
				var docTop = doc.scrollTop(),
				anchorTop = anchor.offset().top;
				if(docTop > anchorTop){
					if(!fixed){
						anchor.height(content.outerHeight());
						content.addClass('fixed');        
						fixed = true;
					}
					} else {
					if(fixed){
						anchor.height(0);
						content.removeClass('fixed'); 
						fixed = false;
					}
				}
			};
			$(window).on('scroll', onScroll);
		}
	};
	if($("div").is(".schearch-line")){
		$('.schearch-line').before($('<div>', {class: 'sticky-anchor'}));
		var sticky = new StickyElement($('header'));
	}
	
	//Замена главной страницы на иконку в хлебных крошках и верхнем меню
	$('.main-header .breadcrumb a:first').html('<i class="fa fa-home" aria-hidden="true"></i>');
	$("header .nav a[href='/']").prepend('<i style="font-size:15px;" class="fa fa-home" aria-hidden="true"></i> ');
	
	//Если все категории скрыты то кнопки на главную добавляем дополнительный клас который увеличит отступ от строки поиска
	if(!$('.header-cat-btn').length){
		$('.header-home-btn').addClass('header-home-margin');
	}
});



//Функция изменяет форму поиска
function change_header_search_form(id){
	if (typeof window.epcHeaderSearchSetMode === 'function') {
		var roots = document.querySelectorAll('.epc-header-search');
		for (var i = 0; i < roots.length; i++) {
			window.epcHeaderSearchSetMode(roots[i], id);
		}
		return false;
	}
	var mode = String(id);
	var map = {
		'1': '.header_search_form_1',
		'2': '.header_search_form_2',
		'3': '.header_search_form_3',
		'engine': '.header_search_form_engine',
		'car': '.header_search_form_car'
	};
	jQuery.each(map, function(key, selector){
		if(mode === key){
			jQuery(selector).removeClass('hidden');
		}else{
			jQuery(selector).addClass('hidden');
		}
	});
	jQuery('.epc-header-search__tab').each(function(){
		var tab = jQuery(this);
		var active = tab.attr('data-search-mode') === mode;
		tab.toggleClass('active', active);
		tab.attr('aria-selected', active ? 'true' : 'false');
	});
	jQuery('.epc-header-search').attr('data-active-mode', mode);
	var focusSelector = map[mode];
	if(focusSelector){
		var input = jQuery(focusSelector).find('input[type="text"], input[type="search"]').first();
		if(input.length){
			setTimeout(function(){ input.trigger('focus'); }, 40);
		}
	}
	return false;
}

function epcHeaderVinSubmit(form){
	if(!form){ return false; }
	// Laximo Guayaquil uses identString; keep vin for backward compatibility.
	var input = form.querySelector('input[name="identString"], input[name="vin"]');
	if(!input){ return true; }
	var vin = String(input.value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
	input.value = vin;
	if(vin.length < 11){
		alert('Enter a valid VIN / Frame number (11–17 characters).');
		input.focus();
		return false;
	}
	return true;
}

function epcHeaderEngineSubmit(form){
	if(!form){ return false; }
	var input = form.querySelector('input[name="engine"]');
	if(!input){ return true; }
	var code = String(input.value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
	input.value = code;
	if(code.length < 2 || code.length > 12){
		alert('Enter a valid engine code (2–12 characters, e.g. 3L, 12R, 5L).');
		input.focus();
		return false;
	}
	return true;
}

jQuery(document).ready(function(){
	if (typeof window.epcHeaderSearchTab !== 'function') {
		jQuery(document).on('click', '.epc-header-search__tab', function(){
			change_header_search_form(jQuery(this).attr('data-search-mode'));
		});
		jQuery('.epc-header-search').each(function(){
			var root = jQuery(this);
			var mode = root.attr('data-active-mode') || '1';
			change_header_search_form(mode);
		});
	}
	jQuery(document).on('input', '.epc-header-search__vin-input', function(){
		this.value = String(this.value || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
	});
});



jQuery(document).ready(function () {

    $('.nav .dropdown-cat-btn').on('click',function(event){
		if ($(window).width() > 767){
			if ( $('.keep_open').hasClass('open_t')) {
				$('.keep_open').removeClass('open_t');
				$('.nav .dropdown').removeClass('open');
				
				if($('#top-menu-catalogue-accordion .catalogue-collapse-link-box a')){
					if($('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').text() == 'Свернуть'){
						$('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').click();
					}
				}
				
			}else{
				$('.nav .panel-collapse').collapse('hide');
				$('.nav .dropdown-menu').removeClass('open_t');
				$('.keep_open').addClass('open_t');
				
				if($('#top-menu-catalogue-accordion .catalogue-collapse-link-box a')){
					if($('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').text() == 'Свернуть'){
						$('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').text('Показать все');
					}
				}
				
			}
			return false;
		}
    });
	
	
	// При клике в любой области сайта не закрывать меню каталога
	$(document).on('click', '.dropdown-menu', function (e) {
		$(this).hasClass('keep_open') && e.stopPropagation(); // This replace if conditional.
	}); 
	
	
	// Изменить текст кнопки при разворячивании всего списка категорий
	$('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').on('click', function (e) {
		if($('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').text() == 'Свернуть'){
			$('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').text('Показать все');
		}else{
			$('#top-menu-catalogue-accordion .catalogue-collapse-link-box a').text('Свернуть');
		}
	}); 
	
	
	var cbpAnimatedHeader = (function() {
		
		var docElem = document.documentElement,
			header = $( '.header-box' );
			didScroll = false,
			changeHeaderOn = 300;

		function init() {
			if ($(window).width() > 991){
				window.addEventListener( 'scroll', function( event ) {
					if( !didScroll ) {
						didScroll = true;
						setTimeout( scrollPage, 50 );
					}
				}, false );
			}
		}

		function scrollPage() {
			var sy = scrollY();
			header = $( '#header-box' );
			if ( sy >= changeHeaderOn) {
				
				
				// Если список категорий развернут полностью то запрещаем анимацию шапки
				// Сделано для того что бы была возможность прокрутки всего списка категорий так как после анимации шапки блок становится
				// абсолютно позицирнированным и его прокрутка не возможна
				
				if ( ! $('#top-menu-catalogue-collapseTwo').hasClass('in') ) {
					
					if ( ! header.hasClass('header-full-shrink')) {
						flag = false;
						var height = (header.css('height'));
						$('#sb-site').css('margin-top', height);
						header.addClass('header-full-shrink');
						setTimeout(function() { header.addClass('header-full-shrink-stuck'); }, 200);
					
						if ($(window).width() > 767){
							if ( $('.keep_open').hasClass('open_t')) {
								$('.keep_open').removeClass('open_t');
								$('.nav .dropdown').removeClass('open');
								$('.keep_open').addClass('open_s');
							}
						}
					}
					
				}
			}
			else {
				if (header.hasClass('header-full-shrink')) {
					
					header.removeClass('header-full-shrink');
					header.removeClass('header-full-shrink-stuck');
					$('#sb-site').css('margin-top', 0);
					
					if ($(window).width() > 767){
						if ( $('.keep_open').hasClass('open_s')) {
							$('.nav .panel-collapse').collapse('hide');
							$('.nav .dropdown-menu').removeClass('open_t');
							$('.keep_open').addClass('open_t');
							$('.keep_open').removeClass('open_s');
						}else{
							if ( $('.keep_open').hasClass('open_t')) {
								$('.keep_open').removeClass('open_t');
								$('.nav .dropdown').removeClass('open');
							}
						}
					}
				}
			}
			didScroll = false;
		}

		function scrollY() {
			return window.pageYOffset || docElem.scrollTop;
		}

		init();

	})();
	
	/*
	Код нужен для работы кнопки корзины в проценке для браузеров IE
	*/
	if (typeof Object.assign != 'function') {
	  Object.assign = function(target) {
		'use strict';
		if (target == null) {
		  throw new TypeError('Cannot convert undefined or null to object');
		}
		target = Object(target);
		for (var index = 1; index < arguments.length; index++) {
		  var source = arguments[index];
		  if (source != null) {
			for (var key in source) {
			  if (Object.prototype.hasOwnProperty.call(source, key)) {
				target[key] = source[key];
			  }
			}
		  }
		}
		return target;
	  };
	}
	
});



/* Smooth scrolling para anclas */
$(document).on('click','a.smooth', function(e){
    e.preventDefault();
    var $link = $(this);
    var anchor = $link.attr('href');
    $('html, body').stop().animate({
        scrollTop: $(anchor).offset().top
    }, 1000);
});

(function($) {
    $(document).ready(function() {
		try{
			$.slidebars();
		}catch(e){
			log_my_log_error(e);
		}
    });
}) (jQuery);

(function( $ ) {
	if($('.masonry-container').length){
		var $container = $('.masonry-container');
		$container.imagesLoaded( function () {
			$container.masonry({
				columnWidth: '.masonry-item',
				itemSelector: '.masonry-item'
			});
		});
	}
})(jQuery);

// Syntax Enable
SyntaxHighlighter.all();

jQuery(document).ready(function () {
	$('.nav').on('click', 'a.has_children', function () {
		if ( $(this).next('ul').hasClass('open_t')) {
			$(this).next('ul').removeClass('open_t');
			if ($(window).width() < 768){
				$(this).next('ul').css('display','none');
			}
			return false;
		}
		$('.open_t').not($(this).parents('ul')).removeClass('open_t');
		$(this).next('ul').addClass('open_t');
		
		if($(this).parents('ul').hasClass('keep_open')){
			if ($(window).width() > 767){
				return true;
			}else{
				return false;
			}
		}else{
			return false;
		}
		
	});
		
    // hide #back-top first
    $("#back-top").hide();

    // fade in #back-top
    $(function () {
        $(window).scroll(function () {
            if ($(this).scrollTop() > 100) {
                $('#back-top').fadeIn();
            } else {
                $('#back-top').fadeOut();
            }
        });

        // scroll body to 0px on click
        $('#back-top a').click(function () {
            $('body,html').animate({
                scrollTop: 0
            }, 500);
            return false;
        });
    });

});

try{
	// WOW Activate
	new WOW().init();
}catch(e){
	log_my_log_error(e);
}

jQuery(document).ready(function() { // makes sure the whole site is loaded
    $('#status').fadeOut(); // will first fade out the loading animation
    $('#preloader').delay(350).fadeOut('slow'); // will fade out the white DIV that covers the website.
    //$('body').delay(350).css({'overflow':'visible'});
});


try{
	// full-width-checkbox
	$("[name='full-width-checkbox']").bootstrapSwitch();
}catch(e){
	log_my_log_error(e);
}


if($('.col-megamenu').length){
	$('.col-megamenu').matchHeight({
		byRow: true,
		property: 'height',
		target: null,
		remove: false
	});
}

/**
-* jQuery scroroller Plugin 1.0
-*
-* http://www.tinywall.net/
-*
-* Developers: Arun David, Boobalan
-* Copyright (c) 2014
-*/
/* jshint -W061 */
(function($){
    $(window).on("load",function(){
        $(document).scrollzipInit();
        $(document).rollerInit();
    });
    $(window).on("load scroll resize", function(){
        $('.numscroller').scrollzip({
            showFunction    :   function() {
                                    numberRoller($(this).attr('data-slno'));
                                },
            wholeVisible    :     false,
        });
    });
    $.fn.scrollzipInit=function(){
        $('body').prepend("<div style='position:fixed;top:0px;left:0px;width:0;height:0;' id='scrollzipPoint'></div>" );
    };
    $.fn.rollerInit=function(){
        var i=0;
        $('.numscroller').each(function() {
            i++;
           $(this).attr('data-slno',i);
           $(this).addClass("roller-title-number-"+i);
        });
    };
    $.fn.scrollzip = function(options){
        var settings = $.extend({
            showFunction    : null,
            hideFunction    : null,
            showShift       : 0,
            wholeVisible    : false,
            hideShift       : 0,
        }, options);
        return this.each(function(i,obj){
            $(this).addClass('scrollzip');
            if ( $.isFunction( settings.showFunction ) ){
                if(
                    !$(this).hasClass('isShown')&&
                    ($(window).outerHeight()+$('#scrollzipPoint').offset().top-settings.showShift)>($(this).offset().top+((settings.wholeVisible)?$(this).outerHeight():0))&&
                    ($('#scrollzipPoint').offset().top+((settings.wholeVisible)?$(this).outerHeight():0))<($(this).outerHeight()+$(this).offset().top-settings.showShift)
                ){
                    $(this).addClass('isShown');
                    settings.showFunction.call( this );
                }
            }
            if ( $.isFunction( settings.hideFunction ) ){
                if(
                    $(this).hasClass('isShown')&&
                    (($(window).outerHeight()+$('#scrollzipPoint').offset().top-settings.hideShift)<($(this).offset().top+((settings.wholeVisible)?$(this).outerHeight():0))||
                    ($('#scrollzipPoint').offset().top+((settings.wholeVisible)?$(this).outerHeight():0))>($(this).outerHeight()+$(this).offset().top-settings.hideShift))
                ){
                    $(this).removeClass('isShown');
                    settings.hideFunction.call( this );
                }
            }
            return this;
        });
    };
    function numberRoller(slno){
            var min=$('.roller-title-number-'+slno).attr('data-min');
            var max=$('.roller-title-number-'+slno).attr('data-max');
            var timediff=$('.roller-title-number-'+slno).attr('data-delay');
            var increment=$('.roller-title-number-'+slno).attr('data-increment');
            var numdiff=max-min;
            var timeout=(timediff*1000)/numdiff;
            //if(numinc<10){
                //increment=Math.floor((timediff*1000)/10);
            //}//alert(increment);
            numberRoll(slno,min,max,increment,timeout);

    }
    function numberRoll(slno,min,max,increment,timeout){//alert(slno+"="+min+"="+max+"="+increment+"="+timeout);
        if(min<=max){
            $('.roller-title-number-'+slno).html(min);
            min=parseInt(min)+parseInt(increment);
            setTimeout(function(){numberRoll(eval(slno),eval(min),eval(max),eval(increment),eval(timeout));},timeout);
        }else{
            $('.roller-title-number-'+slno).html(max);
        }
    }
})(jQuery);


function setCookie(name, value, days) {
	let expires = "";
	if (days) {
		let date = new Date();
		date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
		expires = "; expires=" + date.toUTCString();
	}
	document.cookie = name + "=" + (value || "") + expires + "; path=/";
}

function getCookie(name) {
	let matches = document.cookie.match(new RegExp("(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"));
	return matches ? decodeURIComponent(matches[1]) : undefined;
}


function checkCookies() {
	let cookieNote = document.getElementById('cookie-message');
	if(cookieNote){
		let cookieBtnAccept = cookieNote.querySelector('.cookie-btn');

		// Если куки cookies_policy нет или она просрочена, то показываем уведомление
		if (!getCookie('cookies_policy')) {
			cookieNote.classList.add('show');
		}

		// При клике на кнопку устанавливаем куку cookies_policy на один год
		cookieBtnAccept.addEventListener('click', function () {
			setCookie('cookies_policy', 'true', 365);
			cookieNote.classList.remove('show');
		});
	}
}

jQuery(document).ready(function() {
	
	checkCookies();

});
