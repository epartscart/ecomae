<?php
defined('_ASTEXE_') or die('No access');
// ----------------  Слайдер начало

// Получаем настройки слайдера
$stmt = $db_link->prepare('SELECT * FROM `slider_setings`;');
$stmt->execute();
$slider_setings = $stmt->fetch(PDO::FETCH_ASSOC);

if( is_array($slider_setings) )
{
	// Если слайдер подключен
	if($slider_setings['connected'] == 1)
	{
		// Получаем список картинок слайдера
		$slider_images = array();
		$stmt = $db_link->prepare('SELECT * FROM `slider_images` ORDER BY `orders` ASC;');
		$stmt->execute();
		while( $row = $stmt->fetch(PDO::FETCH_ASSOC) )
		{
			$slider_images[] = $row;
		}
		
		
		if(!empty($slider_images))
		{
		?>
			<div class="slider_box">
				<div class="row">
					<?php
					
					if($DP_Content->main_flag && !empty($DP_Template->data_value->menu_category_show)){
					
					}
					?>
					<script type="text/javascript" src="/modules/slider/js/jquery.flexisel.js"></script>
					<div class="col-lg-12">
						<ul id="flexiselDemo3">
							<?php
								foreach($slider_images as $img)
								{
									if(!empty($img['link']))
									{
										echo '<li><a href="'. $img['link'] .'"><img class="navbar-inverse" src="'. $img['href'] .'" /></a></li>';
									}
									else
									{
										echo '<li><img class="navbar-inverse" src="'. $img['href'] .'" /></li>';
									}
								}
							?>                                     
						</ul>
					</div>
				</div>
			</div>
			<script type="text/javascript">
				$(window).load(function() {
					$("#flexiselDemo3").flexisel({
						visibleItems: <?=$slider_setings['cnt_img'];?>,// Количество отображаемых картинок на странице
						itemsToScroll: <?=$slider_setings['cnt_img_next'];?>,// Количество картинок которые нужно пролистать при клике
						autoPlay: {
							enable: true,
							interval: <?=$slider_setings['time_next'];?>,// Время через которое листать картинки
							pauseOnHover: true
						}
					});
					<?php
					// Если в настройках шаблона включено отображение меню категорий товаров раскрытым и открыта главная страница
					if($DP_Content->main_flag && !empty($DP_Template->data_value->menu_category_show)){
					?>
						if ($(window).width() >= 1200){
							if ($('.keep_open').length > 0){
								if(!$('.keep_open').hasClass('open_t')){
									$('.nav .panel-collapse').collapse('hide');
									$('.nav .dropdown-menu').removeClass('open_t');
									$('.keep_open').addClass('open_t');
									$(".slider_box ").css('margin','0px 0px 0px 200px');
								}
								<?php
								if($DP_Template->data_value->header_style == 'no_header'){
								?>
								if($(".nav_cat").length > 0){
									$(".nav_cat").css('position','static');
									$(".nav_cat > li").css('position','static');
									$(".keep_open ").css('left','0');
								}
								<?php
								}
								?>
							}
						}
					<?php
					}
					?>
				});
			</script>
		<?php
		}
	}
}
// ----------------  Слайдер конец
?>