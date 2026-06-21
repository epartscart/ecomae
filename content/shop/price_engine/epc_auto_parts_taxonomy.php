<?php
/**
 * EPC Auto Parts Taxonomy — engine, drivetrain, OEM brands (3 levels).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<int,array{slug:string,name:string,sort?:int,children?:array}>
 */
function epc_auto_tax_seed_tree(): array
{
	return array(
		array('slug' => 'auto-engine', 'name' => 'Engine & drivetrain', 'sort' => 10, 'children' => array(
			array('slug' => 'auto-engine-parts', 'name' => 'Engine parts', 'children' => array(
				array('slug' => 'auto-engine-parts-pistons', 'name' => 'Pistons & rings'),
				array('slug' => 'auto-engine-parts-gaskets', 'name' => 'Gaskets & seals'),
				array('slug' => 'auto-engine-parts-valves', 'name' => 'Valves & guides'),
			)),
			array('slug' => 'auto-engine-filters', 'name' => 'Filters', 'children' => array(
				array('slug' => 'auto-engine-filters-oil', 'name' => 'Oil filters'),
				array('slug' => 'auto-engine-filters-air', 'name' => 'Air filters'),
				array('slug' => 'auto-engine-filters-fuel', 'name' => 'Fuel filters'),
				array('slug' => 'auto-engine-filters-cabin', 'name' => 'Cabin filters'),
			)),
			array('slug' => 'auto-engine-belts', 'name' => 'Belts & hoses', 'children' => array(
				array('slug' => 'auto-engine-belts-timing', 'name' => 'Timing belts'),
				array('slug' => 'auto-engine-belts-serpentine', 'name' => 'Serpentine belts'),
				array('slug' => 'auto-engine-belts-hoses', 'name' => 'Radiator & coolant hoses'),
			)),
			array('slug' => 'auto-engine-ignition', 'name' => 'Ignition system', 'children' => array(
				array('slug' => 'auto-engine-spark', 'name' => 'Spark plugs'),
				array('slug' => 'auto-engine-coils', 'name' => 'Ignition coils'),
				array('slug' => 'auto-engine-distributors', 'name' => 'Distributors & caps'),
			)),
			array('slug' => 'auto-engine-turbo', 'name' => 'Turbo & supercharger', 'children' => array(
				array('slug' => 'auto-engine-turbo-kits', 'name' => 'Turbo kits'),
				array('slug' => 'auto-engine-turbo-intercooler', 'name' => 'Intercoolers'),
			)),
		)),
		array('slug' => 'auto-transmission', 'name' => 'Transmission & clutch', 'sort' => 15, 'children' => array(
			array('slug' => 'auto-transmission-gearbox', 'name' => 'Gearbox parts'),
			array('slug' => 'auto-transmission-clutch', 'name' => 'Clutch kits', 'children' => array(
				array('slug' => 'auto-transmission-clutch-disc', 'name' => 'Clutch discs'),
				array('slug' => 'auto-transmission-clutch-pressure', 'name' => 'Pressure plates'),
			)),
			array('slug' => 'auto-transmission-cv', 'name' => 'CV joints & axles', 'children' => array(
				array('slug' => 'auto-transmission-cv-joints', 'name' => 'CV joints'),
				array('slug' => 'auto-transmission-cv-boots', 'name' => 'CV boots'),
			)),
			array('slug' => 'auto-transmission-differential', 'name' => 'Differential parts'),
		)),
		array('slug' => 'auto-brakes', 'name' => 'Brakes', 'sort' => 20, 'children' => array(
			array('slug' => 'auto-brakes-pads', 'name' => 'Brake pads', 'children' => array(
				array('slug' => 'auto-brakes-pads-ceramic', 'name' => 'Ceramic pads'),
				array('slug' => 'auto-brakes-pads-semi', 'name' => 'Semi-metallic pads'),
			)),
			array('slug' => 'auto-brakes-rotors', 'name' => 'Brake rotors & discs'),
			array('slug' => 'auto-brakes-calipers', 'name' => 'Calipers & hardware'),
			array('slug' => 'auto-brakes-lines', 'name' => 'Brake lines & fluid'),
		)),
		array('slug' => 'auto-suspension', 'name' => 'Suspension & steering', 'sort' => 25, 'children' => array(
			array('slug' => 'auto-suspension-shocks', 'name' => 'Shock absorbers & struts', 'children' => array(
				array('slug' => 'auto-brakes-shocks', 'name' => 'Shocks & struts'),
				array('slug' => 'auto-suspension-coilovers', 'name' => 'Coilovers'),
			)),
			array('slug' => 'auto-suspension-control-arms', 'name' => 'Control arms & bushings'),
			array('slug' => 'auto-suspension-bearings', 'name' => 'Wheel bearings & hubs'),
			array('slug' => 'auto-steering', 'name' => 'Steering parts', 'children' => array(
				array('slug' => 'auto-steering-rack', 'name' => 'Steering racks'),
				array('slug' => 'auto-steering-pump', 'name' => 'Power steering pumps'),
				array('slug' => 'auto-steering-tie-rods', 'name' => 'Tie rods & ends'),
			)),
		)),
		array('slug' => 'auto-cooling', 'name' => 'Cooling & climate', 'sort' => 30, 'children' => array(
			array('slug' => 'auto-cooling-radiator', 'name' => 'Radiators & caps'),
			array('slug' => 'auto-cooling-water-pump', 'name' => 'Water pumps'),
			array('slug' => 'auto-cooling-fans', 'name' => 'Cooling fans'),
			array('slug' => 'auto-ac', 'name' => 'AC & climate', 'children' => array(
				array('slug' => 'auto-ac-compressor', 'name' => 'AC compressors'),
				array('slug' => 'auto-ac-condenser', 'name' => 'Condensers & evaporators'),
			)),
		)),
		array('slug' => 'auto-fuel', 'name' => 'Fuel system', 'sort' => 35, 'children' => array(
			array('slug' => 'auto-fuel-pumps', 'name' => 'Fuel pumps'),
			array('slug' => 'auto-fuel-injectors', 'name' => 'Fuel injectors'),
			array('slug' => 'auto-fuel-tanks', 'name' => 'Fuel tanks & caps'),
		)),
		array('slug' => 'auto-exhaust', 'name' => 'Exhaust system', 'sort' => 40, 'children' => array(
			array('slug' => 'auto-exhaust-manifolds', 'name' => 'Manifolds & headers'),
			array('slug' => 'auto-exhaust-catalytic', 'name' => 'Catalytic converters'),
			array('slug' => 'auto-exhaust-mufflers', 'name' => 'Mufflers & pipes'),
		)),
		array('slug' => 'auto-electrical', 'name' => 'Electrical & sensors', 'sort' => 45, 'children' => array(
			array('slug' => 'auto-electrical-batteries', 'name' => 'Batteries', 'children' => array(
				array('slug' => 'auto-batteries', 'name' => 'Car batteries'),
				array('slug' => 'auto-batteries-agm', 'name' => 'AGM & start-stop batteries'),
			)),
			array('slug' => 'auto-electrical-alternators', 'name' => 'Alternators'),
			array('slug' => 'auto-electrical-starters', 'name' => 'Starters & solenoids'),
			array('slug' => 'auto-sensors-ecu', 'name' => 'Sensors & ECU', 'children' => array(
				array('slug' => 'auto-sensors-o2', 'name' => 'O2 & lambda sensors'),
				array('slug' => 'auto-sensors-abs', 'name' => 'ABS & wheel speed sensors'),
				array('slug' => 'auto-sensors-ecu-units', 'name' => 'ECU & control modules'),
			)),
		)),
		array('slug' => 'auto-body', 'name' => 'Body & exterior', 'sort' => 50, 'children' => array(
			array('slug' => 'auto-body-panels', 'name' => 'Body panels', 'children' => array(
				array('slug' => 'auto-body-panels-doors', 'name' => 'Doors & fenders'),
				array('slug' => 'auto-body-panels-hood', 'name' => 'Hoods & trunk lids'),
			)),
			array('slug' => 'auto-body-lights', 'name' => 'Lighting', 'children' => array(
				array('slug' => 'auto-lighting-headlights', 'name' => 'Headlights'),
				array('slug' => 'auto-lighting-tail', 'name' => 'Tail & brake lights'),
				array('slug' => 'auto-lighting-fog', 'name' => 'Fog & DRL lamps'),
			)),
			array('slug' => 'auto-body-mirrors', 'name' => 'Mirrors & glass'),
			array('slug' => 'auto-body-bumpers', 'name' => 'Bumpers & trim'),
			array('slug' => 'auto-wiper', 'name' => 'Wiper & wash', 'children' => array(
				array('slug' => 'auto-wiper-blades', 'name' => 'Wiper blades'),
				array('slug' => 'auto-wiper-motors', 'name' => 'Wiper motors & pumps'),
			)),
		)),
		array('slug' => 'auto-interior', 'name' => 'Interior & trim', 'sort' => 55, 'children' => array(
			array('slug' => 'auto-interior-mats', 'name' => 'Floor mats & covers'),
			array('slug' => 'auto-interior-seats', 'name' => 'Seat covers & cushions'),
			array('slug' => 'auto-interior-dash', 'name' => 'Dashboard & trim panels'),
			array('slug' => 'auto-interior-electronics', 'name' => 'Car electronics & infotainment'),
		)),
		array('slug' => 'auto-fluids', 'name' => 'Oils & fluids', 'sort' => 60, 'children' => array(
			array('slug' => 'auto-fluids-engine-oil', 'name' => 'Engine oil'),
			array('slug' => 'auto-fluids-transmission', 'name' => 'Transmission fluid'),
			array('slug' => 'auto-fluids-coolant', 'name' => 'Coolant & antifreeze'),
			array('slug' => 'auto-fluids-brake', 'name' => 'Brake fluid'),
		)),
		array('slug' => 'auto-tires', 'name' => 'Tires & wheels', 'sort' => 65, 'children' => array(
			array('slug' => 'auto-tires-passenger', 'name' => 'Passenger tires'),
			array('slug' => 'auto-tires-suv', 'name' => 'SUV & 4x4 tires'),
			array('slug' => 'auto-tires-alloy', 'name' => 'Alloy wheels & rims'),
		)),
		array('slug' => 'auto-oem-brands', 'name' => 'OEM brand lines', 'sort' => 70, 'children' => array(
			array('slug' => 'auto-oem-toyota', 'name' => 'Toyota & Lexus', 'children' => array(
				array('slug' => 'auto-oem-toyota-engine', 'name' => 'Toyota engine parts'),
				array('slug' => 'auto-oem-lexus', 'name' => 'Lexus parts'),
			)),
			array('slug' => 'auto-oem-nissan', 'name' => 'Nissan & Infiniti'),
			array('slug' => 'auto-oem-honda', 'name' => 'Honda & Acura'),
			array('slug' => 'auto-oem-bmw', 'name' => 'BMW'),
			array('slug' => 'auto-oem-mercedes', 'name' => 'Mercedes-Benz'),
			array('slug' => 'auto-oem-ford', 'name' => 'Ford & Lincoln'),
			array('slug' => 'auto-oem-hyundai', 'name' => 'Hyundai'),
			array('slug' => 'auto-oem-kia', 'name' => 'Kia'),
			array('slug' => 'auto-oem-mitsubishi', 'name' => 'Mitsubishi'),
			array('slug' => 'auto-oem-landrover', 'name' => 'Land Rover & Range Rover'),
			array('slug' => 'auto-oem-chevrolet', 'name' => 'Chevrolet & GMC'),
		)),
	);
}
