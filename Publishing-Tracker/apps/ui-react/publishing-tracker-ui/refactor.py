import os
import re

src_dir = r"c:\Github\PublishingTracker\Publishing-Tracker\apps\ui-react\publishing-tracker-ui\src"
pages_dir = os.path.join(src_dir, "pages")

service_map = {
    'bookService': 'books',
    'authService': 'auth',
    'importService': 'imports',
    'platformService': 'platforms',
    'saleService': 'sales',
    'dashboardService': 'dashboard',
    'userService': 'users'
}

for root, _, files in os.walk(pages_dir):
    for file in files:
        if not file.endswith('.tsx'):
            continue
        filepath = os.path.join(root, file)
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        # Check if any service is imported
        services_used = []
        for s in service_map.keys():
            if s in content and f"from '../services/{s}'" in content or f"from '../../services/{s}'" in content:
                services_used.append(s)

        if not services_used:
            continue

        # Replace import
        import_re = re.compile(r"import\s+\{\s*([a-zA-Z]+Service)\s*\}\s+from\s+['\"](?:\.\./)+services/[a-zA-Z]+Service['\"];?\n")
        
        # Remove all service imports
        new_content = import_re.sub("", content)

        # Add DependencyContext import
        import_stmt = "import { useDependencies } from '../context/DependencyContext';\n"
        if import_stmt not in new_content:
            lines = new_content.splitlines()
            last_import = 0
            for i, line in enumerate(lines):
                if line.startswith("import "):
                    last_import = i
            lines.insert(last_import + 1, import_stmt.strip())
            new_content = "\n".join(lines) + "\n"

        # Find component function definition
        # e.g. const BookListPage = () => {
        # or const AddBookPage = () => {
        component_re = re.compile(r"(const\s+[A-Z][a-zA-Z0-9_]*\s*=\s*(?:<[^>]*>)?\s*\([^)]*\)\s*(?::\s*React\.FC)?\s*=>\s*\{)")
        
        match = component_re.search(new_content)
        if match:
            # Insert hook call right after component definition
            hook_vars = ", ".join([service_map[s] for s in services_used])
            hook_call = f"\n    const {{ {hook_vars} }} = useDependencies();"
            
            # Replace service calls
            for s in services_used:
                new_content = new_content.replace(s + ".", service_map[s] + ".")

            new_content = new_content[:match.end()] + hook_call + new_content[match.end():]
            
            # Need to add them to dependency arrays if used inside useEffect/useCallback
            # Just do a blanket replacement if they are used in arrays: [..., bookService, ...]
            for s in services_used:
                new_content = new_content.replace(s, service_map[s])

            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Refactored {filepath}")
        else:
            print(f"Could not find component definition in {filepath}")

