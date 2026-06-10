//Color Switcher
//Copyright 2014 8Guild.com

$(document).ready(function(e) {
	
	var toggle = $('.color-switcher .toggle');
	var colorTile = $('.color-switcher a'); 
	toggle.click(function(){
		$(this).parent().toggleClass('open');
	});
	
	colorTile.click(function(e){
		colorTile.removeClass('current');
		$(this).addClass('current');

		var color = $(this).attr('data-color');
		var logo = $(this).attr('data-color');

		$('head link.color-scheme').attr('href', 'css/colors/color-' + color + '.css');
		e.preventDefault();

		$('header a.logo img').attr('src', 'img/logo-' + color + '.png');
		
		
		
		
		//Динамически меняем библиотеку jQuery-UI
		var head  = document.getElementsByTagName('head')[0];
		var jsElm = document.createElement("script");
		jsElm.type = "application/javascript";
		jsElm.src = '/templates/limo/jquery-ui/jquery-ui-'+color+'/jquery-ui.js';
		head.appendChild(jsElm);
		
		var link  = document.createElement('link');
		link.rel  = 'stylesheet';
		link.type = 'text/css';
		link.href = '/templates/limo/jquery-ui/jquery-ui-'+color+'/jquery-ui.css';
		head.appendChild(link);
		
		
		//Записываем выбор в куки
        var date = new Date(new Date().getTime() + 15552000 * 1000);//На долго
        document.cookie = "limo_main_color="+JSON.stringify(color)+"; path=/; expires=" + date.toUTCString();
		
		
		
		
	});
	
});
	
