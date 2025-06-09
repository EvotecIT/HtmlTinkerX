import http.server
import base64
import os

USERNAME = os.environ.get('BASIC_USER', 'user')
PASSWORD = os.environ.get('BASIC_PASS', 'pass')
PORT = int(os.environ.get('PORT', '8000'))

auth_string = 'Basic ' + base64.b64encode(f'{USERNAME}:{PASSWORD}'.encode()).decode()

class Handler(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.headers.get('Authorization') != auth_string:
            self.send_response(401)
            self.send_header('WWW-Authenticate', 'Basic realm="Test"')
            self.end_headers()
            return
        super().do_GET()

if __name__ == '__main__':
    http.server.test(Handler, port=PORT)
