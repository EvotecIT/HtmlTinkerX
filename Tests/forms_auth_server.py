import http.server
import urllib.parse
import os

USERNAME = os.environ.get('FORM_USER', 'user')
PASSWORD = os.environ.get('FORM_PASS', 'pass')
PORT = int(os.environ.get('PORT', '8000'))

class Handler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path == '/login':
            self.send_response(200)
            self.send_header('Content-Type', 'text/html')
            self.end_headers()
            self.wfile.write(b"""
<html><body>
<form method='post' action='/login'>
<input type='text' name='user'>
<input type='password' name='pass'>
<input type='submit' value='Login'>
</form>
</body></html>""")
        elif self.path == '/secret.html':
            if self.headers.get('Cookie') != 'session=1':
                self.send_response(302)
                self.send_header('Location', '/login')
                self.end_headers()
                return
            self.send_response(200)
            self.send_header('Content-Type', 'text/html')
            self.end_headers()
            self.wfile.write(b"<p id='secret'>Authenticated</p>")
        else:
            super().do_GET()

    def do_POST(self):
        if self.path == '/login':
            length = int(self.headers.get('Content-Length', 0))
            data = self.rfile.read(length).decode()
            params = urllib.parse.parse_qs(data)
            if params.get('user', [''])[0] == USERNAME and params.get('pass', [''])[0] == PASSWORD:
                self.send_response(302)
                self.send_header('Set-Cookie', 'session=1')
                self.send_header('Location', '/secret.html')
                self.end_headers()
            else:
                self.send_response(302)
                self.send_header('Location', '/login')
                self.end_headers()
        else:
            self.send_error(404)

if __name__ == '__main__':
    http.server.test(Handler, port=PORT)
