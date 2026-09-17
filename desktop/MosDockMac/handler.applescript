on helperPython()
	set appPath to POSIX path of (path to me)
	return appPath & "Contents/Resources/mosdock_mac.py"
end helperPython

on startHelper()
	set py to helperPython()
	do shell script "/usr/bin/python3 " & quoted form of py & " >/tmp/mosdock.log 2>&1 &"
end startHelper

on run
	startHelper()
end run

on open location theURL
	startHelper()
	delay 0.5
	do shell script "curl -s -G --data-urlencode " & quoted form of ("url=" & theURL) & " http://127.0.0.1:17331/from-url >/dev/null 2>&1 || /usr/bin/python3 " & quoted form of helperPython() & " --url " & quoted form of theURL & " >/tmp/mosdock.log 2>&1 &"
end open location
