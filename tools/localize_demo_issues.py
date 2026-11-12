"""Apply the Russian text to the existing Parts Unlimited issues in StudyFlow."""

import hashlib
import json
import os
import re
import time
import urllib.error
import urllib.request
from pathlib import Path


REPOSITORY = 'mirea-studyflow-bandukov-ilya/studyflow'
API = f'https://api.github.com/repos/{REPOSITORY}'
MARKER = re.compile(r'^<!-- parts-unlimited:(\d+) -->')


def request(path, payload=None):
    headers = {
        'Authorization': 'Bearer ' + os.environ['GITHUB_TOKEN'],
        'Accept': 'application/vnd.github+json',
        'User-Agent': 'StudyFlow-localization',
        'Content-Type': 'application/json',
    }
    data = json.dumps(payload, ensure_ascii=False).encode() if payload is not None else None
    method = 'PATCH' if data is not None else 'GET'
    for attempt in range(3):
        req = urllib.request.Request(API + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                return json.load(response)
        except urllib.error.HTTPError as exc:
            retry_after = exc.headers.get('Retry-After')
            if attempt == 2 or not (exc.code == 429 or (exc.code == 403 and retry_after)):
                raise
            time.sleep(int(retry_after) if retry_after and retry_after.isdecimal() else 65)


def main():
    if os.environ['GITHUB_REPOSITORY'] != REPOSITORY:
        raise SystemExit('Перевод доступен только в репозитории StudyFlow.')

    folder = Path(__file__).parent / 'DemoGenerator'
    translations = json.loads((folder / 'localization-ru.json').read_text(encoding='utf-8'))
    template = json.loads((folder / 'template.json').read_text(encoding='utf-8'))
    original_titles = {str(item['sourceId']): item['title'] for item in template['items']}
    if translations.keys() != original_titles.keys():
        raise SystemExit('Номера переводов не совпадают с шаблоном.')

    issues = {}
    page = 1
    while True:
        rows = request(f'/issues?state=all&labels=demo%3Aparts-unlimited&per_page=100&page={page}')
        for issue in rows:
            match = MARKER.match(issue.get('body') or '')
            if not match or 'pull_request' in issue:
                continue
            source_id = match[1]
            if source_id in issues:
                raise SystemExit(f'Повтор номера шаблона {source_id}.')
            issues[source_id] = issue
        if len(rows) < 100:
            break
        page += 1

    if issues.keys() != translations.keys():
        raise SystemExit('Состав учебных карточек отличается от шаблона.')

    updates = []
    for source_id, issue in issues.items():
        translated = translations[source_id]
        patch = {}
        if issue['title'] != translated['title']:
            if issue['title'] != original_titles[source_id]:
                raise SystemExit(f"Карточка #{issue['number']}: заголовок уже изменён вручную.")
            patch['title'] = translated['title']
        if issue['body'] != translated['body']:
            if hashlib.sha256(issue['body'].encode()).hexdigest() != translated['originalBodySha256']:
                raise SystemExit(f"Карточка #{issue['number']}: описание уже изменено вручную.")
            patch['body'] = translated['body']
        if patch:
            updates.append((issue['number'], patch))

    print(f'Карточек проверено: {len(issues)}. К обновлению: {len(updates)}.', flush=True)
    for index, (number, patch) in enumerate(updates, 1):
        saved = request(f'/issues/{number}', patch)
        if any(saved[key] != value for key, value in patch.items()):
            raise SystemExit(f'GitHub не подтвердил перевод карточки #{number}.')
        if index % 20 == 0 or index == len(updates):
            print(f'Обновлено {index} из {len(updates)}.', flush=True)
        if index < len(updates):
            time.sleep(1.1)

    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as summary:
        print('## Перевод карточек Parts Unlimited', file=summary)
        print(f'Проверено карточек: **{len(issues)}**. Обновлено: **{len(updates)}**.', file=summary)


if __name__ == '__main__':
    main()
