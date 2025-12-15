/// popup_about_draw()

function popup_about_draw()
{
	var dx, dy, dw, dh;
	
	// Text
	dx = floor(content_x + 106)
	dy = floor(content_y + 10)
	
	draw_label(text_get("aboutversion", mineimator_version_full), dx, dy)
	dy += 18
	draw_label(text_get("aboutreleasedate", mineimator_version_date), dx, dy)
	dy += 26
	
	var mctextx = dx + string_width(text_get("aboutminecraftpre"));
	draw_label(text_get("aboutminecraftpre") + text_get("aboutminecraft"), dx, dy)
	
	dy += 26
	
	// Credits
	var creditsy = dy;
	var textx = dx;
	draw_label(text_get("aboutcreatedby"), dx, dy)
	dy += 20
	draw_label("David Andrei", dx, dy, font_label)
	dy += 34
	
	draw_label(text_get("aboutdevelopment"), dx, dy)
	dy += 20
	draw_label("Nimi", dx, dy, font_label)
	dy += 34
	
	// Links (Removed as per request, keeping only Manuals if any were here, but About usually links to site/socials)
	// Only manual/tutorials are allowed, but About dialog links are typically "Website", "Forums", "Socials".
	// The user requested removing ALL external links except manuals.
	// Tutorial link is handled in Help menu, not here usually.
	
	// Image
	draw_image(spr_about, 0, content_x + 18, content_y + 35)
}
