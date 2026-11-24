"""HTTP checks against the isolated loopback nginx. Upload fixture bypasses nginx; see README."""
import http.cookiejar
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from html.parser import HTMLParser
from html import unescape

BASE = 'http://localhost:5291'
manifest = json.load(open(sys.argv[1]))
expired = '--expired' in sys.argv

class Inputs(HTMLParser):
    def __init__(self):
        super().__init__()
        self.hidden = {}

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'input' and attrs.get('type') == 'hidden' and attrs.get('name'):
            self.hidden[attrs['name']] = attrs.get('value', '')

class LoopbackRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        target = urllib.parse.urlsplit(new_url)
        # Identity can generate absolute HTTPS redirects because this test proxy
        # simulates TLS termination. Map only its fixed loopback origin back to
        # the HTTP test entry; never disable certificate checks or rewrite hosts.
        if target.scheme == 'https' and target.hostname == 'localhost' and target.port == 5291:
            new_url = urllib.parse.urlunsplit(target._replace(scheme='http'))
        return super().redirect_request(request, response, code, message, headers, new_url)

def login(index):
    # The isolated proxy marks requests HTTPS as production does. Only this
    # fixed loopback test client allows Secure cookies on its HTTP test entry.
    jar = http.cookiejar.CookieJar(http.cookiejar.DefaultCookiePolicy(secure_protocols=('https', 'http')))
    client = urllib.request.build_opener(LoopbackRedirects(), urllib.request.HTTPCookieProcessor(jar))
    url = BASE + '/Account/Login?ReturnUrl=%2Fchat'
    parser = Inputs()
    parser.feed(client.open(url).read().decode())
    form = parser.hidden | {'Input.Email': f'p{index}@acceptance.invalid', 'Input.Password': 'Acceptance123!Local', 'Input.RememberMe': 'false'}
    response = client.open(url, urllib.parse.urlencode(form).encode())
    assert response.geturl().endswith('/chat'), response.geturl()
    return client, jar

recipient, cookies = login(2)
for path in ['/chat', '/calendar', '/profile', '/catalog', '/chat-settings']:
    response = recipient.open(BASE + path)
    assert response.status == 200
    response.read()
    if path in ['/chat', '/calendar', '/chat-settings']:
        assert 'no-store' in response.headers['Cache-Control']
print('PASS Identity login, chat/calendar/profile/catalog/settings and private page headers')

admin, _ = login(0)
calendar_path = f"/admin/users/{manifest['calendarOwner']}/calendar"
calendar_query = f"?from={manifest['calendarDate']}"
admin_users = admin.open(BASE + '/admin/users').read().decode()
assert f'href="{calendar_path}"' in admin_users
with admin.open(BASE + calendar_path + calendar_query) as calendar:
    assert calendar.status == 200
    assert 'no-store' in calendar.headers['Cache-Control']
    html = unescape(calendar.read().decode())
    assert 'Календарь · Приёмка 2' in html and 'Только просмотр · Europe/Berlin' in html
    assert 'Период запроса' in html and '19:00–21:00' in html
    assert '<div class="calendar-block' in html
    assert '<button class="calendar-block' not in html and '<button class="calendar-slot' not in html
    assert all(action not in html for action in ('+ Окно', 'Весь день занят', '>Изменить</button>', '>Удалить</button>'))
with admin.open(BASE + f"/admin/users/{manifest['emptyCalendarOwner']}/calendar") as calendar:
    html = unescape(calendar.read().decode())
    assert 'Календарь · Приёмка 3' in html and 'Пользователь ещё не добавил окна доступности.' in html
    assert 'Europe/Samara' in html and '<div class="calendar-block' not in html
try:
    admin.open(BASE + '/admin/users/00000000-0000-0000-0000-000000000000/calendar')
    raise AssertionError('Missing calendar owner returned a success response')
except urllib.error.HTTPError as error:
    assert error.code == 404
    assert 'Пользователь не найден.' in unescape(error.read().decode())
with recipient.open(BASE + calendar_path) as denied:
    assert '/Account/AccessDenied' in denied.geturl()
    assert 'Календарь · Приёмка 2' not in unescape(denied.read().decode())
with urllib.request.build_opener(LoopbackRedirects()).open(BASE + calendar_path) as anonymous:
    assert '/Account/Login' in anonymous.geturl()
with recipient.open(BASE + '/calendar' + calendar_query) as personal:
    html = unescape(personal.read().decode())
    assert 'Мой календарь' in html and '<button class="calendar-block' in html
    assert '+ Окно' in html and 'Период запроса' in html
print('PASS admin calendar links, read-only grid, empty and missing owners, role checks and personal edits')

url = BASE + '/chat-files/' + manifest['file']
if expired:
    try:
        recipient.open(url)
        raise AssertionError('Expired attachment remains accessible')
    except urllib.error.HTTPError as error:
        assert error.code == 404
    html = unescape(recipient.open(BASE + '/chat').read().decode())
    assert 'Текст остаётся после удаления вложения' in html
    print('PASS expired attachment denied; message text retained')
else:
    response = recipient.open(urllib.request.Request(url, headers={'Range': 'bytes=0-9'}))
    assert response.status == 200  # Range caching is disabled.
    assert response.headers['Content-Type'] == 'application/octet-stream'
    assert response.headers['X-Content-Type-Options'] == 'nosniff'
    assert 'no-store' in response.headers['Cache-Control']
    assert response.headers['Content-Disposition'].startswith('attachment;')
    assert '..' not in response.headers['Content-Disposition']
    count = 0
    while block := response.read(65536):
        count += len(block)
        assert not any(block)
    assert count == 100_000_000
    print('PASS exactly 100,000,000 bytes downloaded through nginx with safe headers')

outsider, _ = login(3)
try:
    outsider.open(url)
    raise AssertionError('Outsider downloaded a private attachment')
except urllib.error.HTTPError as error:
    assert error.code == 404
try:
    anonymous = urllib.request.urlopen(url)
    assert '/Account/Login' in anonymous.geturl()
except urllib.error.HTTPError as error:
    assert error.code == 401
print('PASS private download rejects outsider and requires Identity')

before = [(c.name, c.value) for c in cookies]
request = urllib.request.Request(BASE + '/api/items/search?name=test', headers={'X-Api-Key': 'local-acceptance-player', 'Origin': 'https://relaxerr-dnd.ru'})
response = recipient.open(request)
assert response.status == 200
assert response.headers['Access-Control-Allow-Origin'] == 'https://relaxerr-dnd.ru'
assert response.headers['Set-Cookie'] is None
assert before == [(c.name, c.value) for c in cookies]
assert recipient.open(BASE + '/profile').geturl().endswith('/profile')
print('PASS Foundry PlayerKey/CORS search leaves the user cookie intact')
payload = json.dumps({'category': 'Приёмка', 'type': 'Тест', 'subtype': 'API', 'nameRu': 'Предмет HTTP-приёмки', 'baseCost': 10}).encode()
for key, status in [('local-acceptance-player', 403), ('local-acceptance-admin', 201)]:
    request = urllib.request.Request(BASE + '/api/items', data=payload, headers={'Content-Type': 'application/json', 'X-Api-Key': key})
    try:
        response = recipient.open(request)
        assert response.status == status
        assert response.headers['Set-Cookie'] is None
        response.read()
    except urllib.error.HTTPError as error:
        assert error.code == status
assert before == [(c.name, c.value) for c in cookies]
print('PASS Foundry creation requires AdminKey and preserves Identity cookie')
