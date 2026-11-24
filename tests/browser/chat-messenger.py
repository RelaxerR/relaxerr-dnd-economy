"""Run against a disposable local app with Python Playwright and Chromium.

Set DND_CHAT_URL, DND_CHAT_EMAIL and DND_CHAT_PASSWORD. DND_CHROMIUM is optional.
The script creates messages in the test account's main chat.
"""
import os
from urllib.parse import urlparse
from uuid import uuid4

from playwright.sync_api import expect, sync_playwright


url = os.environ['DND_CHAT_URL'].rstrip('/')
assert urlparse(url).hostname in ('localhost', '127.0.0.1', '::1'), 'Use a disposable local server only'
prefix = f'Проверка чата {uuid4().hex[:8]}'

with sync_playwright() as playwright:
    browser = playwright.chromium.launch(headless=True, executable_path=os.environ.get('DND_CHROMIUM'))
    page = browser.new_page(viewport={'width': 1280, 'height': 900})
    page.set_default_timeout(8000)
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.on('console', lambda message: errors.append(message.text) if message.type == 'error' else None)
    page.add_init_script("""
        const send = WebSocket.prototype.send;
        WebSocket.prototype.send = function (data) {
          setTimeout(() => {
            if (this.readyState === WebSocket.OPEN) send.call(this, data);
          }, 180);
        };
    """)

    page.goto(url + '/Account/Login')
    page.get_by_label('Email', exact=True).fill(os.environ['DND_CHAT_EMAIL'])
    page.get_by_label('Пароль', exact=True).fill(os.environ['DND_CHAT_PASSWORD'])
    page.get_by_role('button', name='Войти', exact=True).click()
    page.wait_for_url(lambda current: '/Account/Login' not in current)
    page.goto(url + '/chat')

    sidebar = page.get_by_role('complementary', name='Разговоры')
    history = page.locator('#chat-history')
    header = page.locator('.chat-conversation-header')
    draft = page.get_by_role('textbox', name='Сообщение', exact=True)
    send_button = page.get_by_role('button', name='Отправить', exact=True)
    expect(sidebar).to_be_visible()
    expect(send_button).to_be_enabled()

    def message(text):
        return history.locator('.chat-text').filter(has_text=text)

    def assert_sent(text, *, cleared=True):
        expect(message(text)).to_have_count(1)
        assert message(text).text_content() == text
        if cleared:
            expect(draft).to_have_value('')

    # Rapid key events, a re-render from the recognized date, and a multiline draft.
    first_line = f'{prefix} 2031-03-12 19:30'
    draft.fill(first_line)
    expect(page.get_by_role('checkbox', name='Подтвердить дату', exact=False)).to_be_visible()
    draft.press('Shift+Enter')
    second_line = 'abcdefghijklmnopqrstuvwxyz' * 5
    draft.press_sequentially(second_line, delay=4)
    full_text = f'{first_line}\n{second_line}'
    expect(draft).to_have_value(full_text)
    expect(message(first_line)).to_have_count(0)

    composing_enter = draft.evaluate("""field => field.dispatchEvent(new KeyboardEvent('keydown',
      {key: 'Enter', isComposing: true, bubbles: true, cancelable: true}))""")
    assert composing_enter, 'Enter should go to the input method during composition'
    draft.press('Enter')
    draft.press('Enter')
    next_text = f'{prefix} следующий черновик'
    draft.fill(next_text)
    assert_sent(full_text, cleared=False)
    expect(draft).to_have_value(next_text)
    expect(send_button).to_be_enabled()
    draft.press('Enter')
    assert_sent(next_text)

    own_message = history.locator('article').filter(has=page.locator('.chat-text').filter(has_text=next_text))
    own_message.locator('summary').click()
    own_message.get_by_role('button', name='Изменить', exact=True).click()
    expect(draft).to_have_value(next_text)
    draft.fill('   ')
    draft.press('Enter')
    expect(page.get_by_role('alert')).to_contain_text('Введите текст')
    expect(draft).to_have_value('   ')
    expect(send_button).to_be_enabled()
    edited = f'{prefix} изменено'
    draft.fill(edited)
    draft.press('Enter')
    assert_sent(edited)
    expect(message(next_text)).to_have_count(0)

    file_name = f'{prefix}.txt'
    file_body = b'Attachment sent from the composer'
    page.locator('.chat-attachment input[type=file]').set_input_files(
        {'name': file_name, 'mimeType': 'text/plain', 'buffer': file_body}
    )
    expect(page.get_by_role('button', name='Убрать', exact=True)).to_be_visible()
    send_button.click()
    attachment = history.get_by_role('link', name=file_name, exact=True)
    expect(attachment).to_be_visible()
    download = page.request.get(url + attachment.get_attribute('href'))
    assert download.ok and download.body() == file_body

    for index in range(8):
        text = '\n'.join([f'{prefix} история {index}'] * 7)
        draft.fill(text)
        send_button.click()
        assert_sent(text)

    for width, height in [(1280, 900), (360, 740), (667, 375)]:
        page.set_viewport_size({'width': width, 'height': height})
        if width <= 700:
            mobile_back = page.get_by_role('button', name='← Чаты')
            if mobile_back.is_visible():
                mobile_back.click()
            expect(sidebar).to_be_visible()
            expect(history).to_be_hidden()
            sidebar.locator('.btn--primary').click()
            expect(sidebar).to_be_hidden()
        else:
            expect(sidebar).to_be_visible()
        expect(history).to_be_visible()
        expect(draft).to_be_in_viewport()
        expect(send_button).to_be_in_viewport()
        expect(header).to_be_in_viewport()
        page.wait_for_function("""() => {
          const history = document.querySelector('#chat-history');
          return history.scrollTop > 0 && history.scrollHeight - history.scrollTop - history.clientHeight < 2;
        }""")
        original_scroll = history.evaluate('el => el.scrollTop')
        before = [item.bounding_box() for item in (header, draft, send_button)]
        history.hover()
        page.mouse.wheel(0, -300)
        page.wait_for_function('top => document.querySelector("#chat-history").scrollTop < top', arg=original_scroll)
        assert [item.bounding_box() for item in (header, draft, send_button)] == before
        assert page.evaluate("""() => window.scrollY === 0 &&
          document.documentElement.scrollHeight === innerHeight &&
          document.documentElement.scrollWidth === innerWidth"""), f'Page overflow at {width}×{height}'

    # A full-length draft can exceed SignalR's default byte limit in both input and send events.
    long_prefix = f'{prefix} Unicode '
    long_text = long_prefix + '界' * (12000 - len(long_prefix))
    draft.fill(long_text)
    draft.press('Enter')
    expect(message(long_prefix)).to_have_count(1)
    assert message(long_prefix).text_content() == long_text
    expect(draft).to_have_value('')

    assert not errors, errors
    print('Chat send, editing, draft retention, attachments and scrolling passed at 1280×900, 360×740 and 667×375.')
