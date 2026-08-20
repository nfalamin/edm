from html.parser import HTMLParser
from pathlib import Path

text = Path('index.html').read_text(encoding='utf-8')

class Checker(HTMLParser):
    def __init__(self):
        super().__init__()
        self.errors = []
    def error(self, message):
        self.errors.append(message)

checker = Checker()
try:
    checker.feed(text)
    checker.close()
    if checker.errors:
        print('ERRORS:')
        for err in checker.errors:
            print(err)
    else:
        print('OK')
except Exception as e:
    print('PARSE ERROR', e)
