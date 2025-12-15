/// open_url(url)
/// @arg url

function open_url(url)
{
	if (url == "")
		return 0;

	if (os_type = os_windows || os_type = os_macosx || os_type = os_linux)
		external_call(lib_open_url, url)
	else
		url_open(url)
}

function popup_open_url(url)
{
	open_url(url)
}
