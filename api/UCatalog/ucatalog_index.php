<!-- UCatalog -->
<div id="UCatalog_container"></div>

<link href="/api/UCatalog/style.css" rel="stylesheet" type="text/css"/>
<script src="/api/UCatalog/api.js"></script>

<link href="/lib/Lightbox/css/lightbox.css" rel="stylesheet" type="text/css"/>
<script type="text/javascript" src="/lib/Lightbox/js/lightbox.js"></script>

<?php
if(isset($_GET['UCatalog_get_garage'])){
?>
<script>
jQuery(document).ready(function () {
	UCatalog_get_garage(<?php echo $_GET['UCatalog_get_garage']; ?>);
	
	//Удалить параметр из адреса
	let url = new URL(document.location);
	let searchParams = url.searchParams;
	searchParams.delete("UCatalog_get_garage");
	window.history.pushState({}, '', url.toString());
});
</script>
<?php
}
?>