<?php
/**
 * EPC Fashion Taxonomy — men, women, kids, accessories (3 levels).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<int,array{slug:string,name:string,sort?:int,children?:array}>
 */
function epc_fashion_tax_seed_tree(): array
{
	return array(
		array('slug' => 'fashion-men', 'name' => 'Men', 'sort' => 10, 'children' => array(
			array('slug' => 'fashion-men-shirts', 'name' => 'Shirts & polos', 'children' => array(
				array('slug' => 'fashion-men-shirts-casual', 'name' => 'Casual shirts'),
				array('slug' => 'fashion-men-shirts-formal', 'name' => 'Formal shirts'),
				array('slug' => 'fashion-men-shirts-polo', 'name' => 'Polos & T-shirts'),
			)),
			array('slug' => 'fashion-men-trousers', 'name' => 'Trousers & jeans', 'children' => array(
				array('slug' => 'fashion-men-trousers-chinos', 'name' => 'Chinos & formal trousers'),
				array('slug' => 'fashion-men-trousers-jeans', 'name' => 'Jeans & denim'),
			)),
			array('slug' => 'fashion-men-outerwear', 'name' => 'Jackets & outerwear'),
			array('slug' => 'fashion-men-footwear', 'name' => 'Footwear', 'children' => array(
				array('slug' => 'fashion-men-footwear-sneakers', 'name' => 'Sneakers & casual'),
				array('slug' => 'fashion-men-footwear-formal', 'name' => 'Formal shoes'),
			)),
			array('slug' => 'fashion-men-activewear', 'name' => 'Activewear & sportswear'),
		)),
		array('slug' => 'fashion-women', 'name' => 'Women', 'sort' => 20, 'children' => array(
			array('slug' => 'fashion-women-dresses', 'name' => 'Dresses & abayas', 'children' => array(
				array('slug' => 'fashion-women-dresses-casual', 'name' => 'Casual dresses'),
				array('slug' => 'fashion-women-dresses-evening', 'name' => 'Evening & occasion'),
				array('slug' => 'fashion-women-abayas', 'name' => 'Abayas & kaftans'),
			)),
			array('slug' => 'fashion-women-tops', 'name' => 'Tops & blouses'),
			array('slug' => 'fashion-women-trousers', 'name' => 'Trousers & skirts'),
			array('slug' => 'fashion-women-footwear', 'name' => 'Footwear', 'children' => array(
				array('slug' => 'fashion-women-footwear-heels', 'name' => 'Heels & pumps'),
				array('slug' => 'fashion-women-footwear-flats', 'name' => 'Flats & sandals'),
			)),
			array('slug' => 'fashion-women-activewear', 'name' => 'Activewear & leggings'),
			array('slug' => 'fashion-women-lingerie', 'name' => 'Lingerie & sleepwear'),
		)),
		array('slug' => 'fashion-kids', 'name' => 'Kids', 'sort' => 30, 'children' => array(
			array('slug' => 'fashion-kids-boys', 'name' => 'Boys', 'children' => array(
				array('slug' => 'fashion-kids-boys-tops', 'name' => 'Tops & T-shirts'),
				array('slug' => 'fashion-kids-boys-bottoms', 'name' => 'Trousers & shorts'),
			)),
			array('slug' => 'fashion-kids-girls', 'name' => 'Girls', 'children' => array(
				array('slug' => 'fashion-kids-girls-dresses', 'name' => 'Dresses & sets'),
				array('slug' => 'fashion-kids-girls-tops', 'name' => 'Tops & skirts'),
			)),
			array('slug' => 'fashion-kids-footwear', 'name' => 'Kids footwear'),
			array('slug' => 'fashion-kids-school', 'name' => 'School uniforms'),
		)),
		array('slug' => 'fashion-accessories', 'name' => 'Accessories', 'sort' => 40, 'children' => array(
			array('slug' => 'fashion-accessories-bags', 'name' => 'Bags & wallets', 'children' => array(
				array('slug' => 'fashion-accessories-handbags', 'name' => 'Handbags & totes'),
				array('slug' => 'fashion-accessories-wallets', 'name' => 'Wallets & card holders'),
			)),
			array('slug' => 'fashion-accessories-sunglasses', 'name' => 'Sunglasses'),
			array('slug' => 'fashion-accessories-belts', 'name' => 'Belts & scarves'),
			array('slug' => 'fashion-accessories-watches', 'name' => 'Fashion watches'),
		)),
	);
}
