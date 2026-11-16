"""Local acceptance with a disposable account/database; pip install playwright first.
DND_CALENDAR_URL, DND_CALENDAR_EMAIL, DND_CALENDAR_PASSWORD are required.
Optional: DND_CHROMIUM, DND_CALENDAR_SCREENSHOTS. Never use a real user account.
"""
import os
from pathlib import Path
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright, expect

url = os.environ['DND_CALENDAR_URL'].rstrip('/')
assert urlparse(url).hostname in ('localhost', '127.0.0.1', '::1'), 'Use a disposable local server only'
artifacts = Path(os.environ.get('DND_CALENDAR_SCREENSHOTS', '/tmp/calendar-recurring-screenshots'))
artifacts.mkdir(parents=True, exist_ok=True)
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, executable_path=os.environ.get('DND_CHROMIUM'))
    page = browser.new_page(viewport={'width': 1280, 'height': 900})
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.goto(url + '/Account/Login')
    page.get_by_label('Email', exact=True).fill(os.environ['DND_CALENDAR_EMAIL'])
    page.get_by_label('Пароль', exact=True).fill(os.environ['DND_CALENDAR_PASSWORD'])
    page.get_by_role('button', name='Войти', exact=True).click()
    page.wait_for_url(lambda current: '/Account/Login' not in current)
    page.goto(url + '/calendar?from=2026-11-16&until=2026-11-23')
    # Wait for the initial SSR-to-circuit handoff before clicking prerendered buttons.
    page.wait_for_timeout(1000)
    editor = page.locator('#calendar-editor')
    section = page.locator('#recurring-windows')
    cards = section.locator('.recurring-list .recurring-card')
    initial = cards.count()
    assert initial == 0 and section.get_by_role('button', name='Удалить разовое окно', exact=True).count() == 0, 'Use an empty disposable account'

    def open_new():
        # Static prerender can precede the interactive circuit on the first visit.
        for attempt in range(5):
            page.get_by_role('button', name='Добавить окно', exact=True).click()
            try:
                expect(editor).to_be_visible(timeout=2000)
                break
            except AssertionError:
                if attempt == 4:
                    raise

    def save():
        editor.get_by_role('button', name='Сохранить окно', exact=True).click()
        expect(editor).not_to_be_visible()

    # All repeats start outside the visible week: list must still expose them.
    for index, kind in enumerate(['Daily', 'Weekly', 'Monthly']):
        open_new()
        editor.locator('summary').click()
        editor.locator('select').nth(2).select_option(kind)
        editor.get_by_label('Повторять начиная с', exact=True).fill('2030-12-01')
        editor.get_by_label('С', exact=True).fill('23:00')
        editor.get_by_label('До', exact=True).fill('02:00')
        if kind == 'Weekly':
            editor.get_by_role('button', name='Будние дни', exact=True).click()
        if kind == 'Monthly':
            editor.get_by_label('Числа месяца, через запятую').fill('1,31')
            editor.get_by_label('Без даты окончания', exact=True).uncheck()
            editor.get_by_label('Последний день повтора', exact=True).fill('2031-03-31')
        save()
        expect(cards).to_have_count(initial + index + 1)

    # Editing opens the source immediately, retaining repeat days and boundedness.
    daily = cards.filter(has=page.get_by_role('heading', name='Ежедневно · 23:00–02:00', exact=True))
    daily.get_by_role('button', name='Изменить', exact=True).click()
    expect(editor.locator('select').nth(2)).to_have_value('Daily')
    expect(editor.get_by_label('Повторять начиная с', exact=True)).to_have_value('2030-12-01')
    editor.get_by_label('С', exact=True).fill('22:15')
    editor.locator('select').nth(0).select_option('Unavailable')
    save()
    expect(cards).to_have_count(initial + 3)
    daily = cards.filter(has=page.get_by_role('heading', name='Ежедневно · 22:15–02:00', exact=True))
    expect(daily).to_contain_text('Точно не смогу')
    monthly = cards.filter(has=page.get_by_role('heading', name='По числам месяца · 23:00–02:00', exact=True))
    monthly.get_by_role('button', name='Изменить', exact=True).click()
    expect(editor.get_by_label('Числа месяца, через запятую')).to_have_value('1,31')
    expect(editor.get_by_label('Последний день повтора', exact=True)).to_have_value('2031-03-31')
    editor.get_by_role('button', name='Удалить серию', exact=True).click()
    editor_delete = page.locator('#calendar-rule-delete')
    expect(editor_delete).to_be_visible()
    expect(editor_delete.get_by_label('Только выбранные разовые исключения', exact=True)).to_be_visible()
    editor_delete.get_by_role('button', name='Отмена', exact=True).click()

    # Independent one-off exception to preserve after series deletion.
    open_new()
    editor.get_by_label('Дата начала', exact=True).fill('2030-12-02')
    editor.get_by_label('Дата окончания', exact=True).fill('2030-12-02')
    editor.get_by_label('С', exact=True).fill('00:00')
    editor.get_by_label('До', exact=True).fill('01:00')
    editor.locator('select').nth(1).select_option('Online')
    save()
    page.get_by_role('link', name='Повторяющиеся окна', exact=False).click()
    assert '/calendar?from=2026-11-16&until=2026-11-23#recurring-windows' in page.url
    modal = page.locator('#calendar-rule-delete')
    for width in [1280, 360, 400]:
        page.set_viewport_size({'width': width, 'height': 900})
        section.scroll_into_view_if_needed()
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), f'Overflow at {width}'
        page.screenshot(path=str(artifacts / f'rules-{width}.png'))
        daily.get_by_role('button', name='Изменить', exact=True).click()
        assert editor.bounding_box()['width'] <= width
        editor.get_by_role('button', name='Отмена', exact=True).click()
        daily.get_by_role('button', name='Удалить', exact=True).click()
        expect(modal).to_be_visible()
        assert modal.bounding_box()['width'] <= width
        expect(modal).to_contain_text('сохранятся')
        page.screenshot(path=str(artifacts / f'delete-{width}.png'))
        modal.get_by_role('button', name='Отмена', exact=True).click()
        expect(cards).to_have_count(initial + 3)

    # Delete only explicitly selected exception, preserving all series.
    daily.get_by_role('button', name='Удалить', exact=True).click()
    modal.get_by_label('Только выбранные разовые исключения', exact=True).check()
    expect(modal.get_by_role('button', name='Подтвердить удаление', exact=False)).to_be_disabled()
    modal.get_by_role('checkbox').filter(visible=True).first.check()
    modal.get_by_role('button', name='Подтвердить удаление', exact=False).click()
    expect(modal).not_to_be_visible()
    expect(cards).to_have_count(initial + 3)

    # Recreate a one-off; deleting its series must leave it manageable.
    open_new()
    save()
    once_count = section.get_by_role('button', name='Удалить разовое окно', exact=True).count()
    for kind in ['Ежедневно · 22:15–02:00', 'По дням недели · 23:00–02:00', 'По числам месяца · 23:00–02:00']:
        card = cards.filter(has=page.get_by_role('heading', name=kind, exact=True))
        card.get_by_role('button', name='Удалить', exact=True).click()
        modal.get_by_role('button', name='Подтвердить удаление правила', exact=True).click()
        expect(modal).not_to_be_visible()
    expect(cards).to_have_count(initial)
    assert section.get_by_role('button', name='Удалить разовое окно', exact=True).count() == once_count
    page.reload()
    expect(cards).to_have_count(initial)
    assert not errors, errors
    browser.close()
    print('PASS: create/edit/cancel/delete, independent exceptions, 1280/360/400 px; no page errors')
