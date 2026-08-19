import os
import re

src_dir = r"c:\Github\PublishingTracker\Publishing-Tracker\apps\ui-react\publishing-tracker-ui\src"

service_map = {
    'bookService': 'books',
    'authService': 'auth',
    'importService': 'imports',
    'platformService': 'platforms',
    'saleService': 'sales',
    'dashboardService': 'dashboard',
    'userService': 'users'
}

for root, _, files in os.walk(os.path.join(src_dir, 'pages')):
    for file in files:
        if not file.endswith('.test.tsx'):
            continue
        filepath = os.path.join(root, file)
        
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Replace service imports
        import_re = re.compile(r"import\s+\{\s*([a-zA-Z]+Service)\s*\}\s+from\s+['\"](?:\.\./)+services/[a-zA-Z]+Service['\"];?\n")
        new_content = import_re.sub("", content)
        
        # Replace jest.mock('../services/bookService') -> jest.mock('../context/DependencyContext')
        mock_re = re.compile(r"jest\.mock\(['\"](?:\.\./)+services/[a-zA-Z]+Service['\"]\);?\n")
        new_content = mock_re.sub("", new_content)
        
        if "useDependencies" not in new_content:
            lines = new_content.splitlines()
            last_import = 0
            for i, line in enumerate(lines):
                if line.startswith("import "):
                    last_import = i
            lines.insert(last_import + 1, "import { useDependencies } from '../context/DependencyContext';\njest.mock('../context/DependencyContext');\nconst mockUseDependencies = useDependencies as jest.Mock;")
            new_content = "\n".join(lines) + "\n"
        
        # Replace const mockedBookService = bookService as jest.Mocked<typeof bookService>; -> mock the return value in the test
        new_content = re.compile(r"const\s+mocked[A-Za-z]+Service\s*=\s*[a-zA-Z]+Service\s+as\s+jest\.Mocked<[^>]+>;\n").sub("", new_content)
        
        # Replace mockedBookService.getBooks.mockResolvedValue -> mockUseDependencies.mockReturnValue({ books: { getBooks: jest.fn().mockResolvedValue } })
        # Actually it's easier to just do:
        # let mockBooks = { getBooks: jest.fn(), ... }
        # mockUseDependencies.mockReturnValue({ books: mockBooks })
        
        # This is getting too complex to regex easily.
        pass
