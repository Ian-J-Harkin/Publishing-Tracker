import os
import re

src_dir = r"c:\Github\PublishingTracker\Publishing-Tracker\apps\ui-react\publishing-tracker-ui\src"
pages_dir = os.path.join(src_dir, "pages")
hooks_dir = os.path.join(src_dir, "hooks")
components_dir = os.path.join(src_dir, "components")

service_map = {
    'bookService': 'books',
    'authService': 'auth',
    'importService': 'imports',
    'platformService': 'platforms',
    'saleService': 'sales',
    'dashboardService': 'dashboard',
    'userService': 'users'
}

def process_directory(directory):
    for root, _, files in os.walk(directory):
        for file in files:
            if not file.endswith('.tsx') and not file.endswith('.ts'):
                continue
            
            # Skip test files and remove them later or manually
            if file.endswith('.test.tsx') or file.endswith('.test.ts'):
                continue

            filepath = os.path.join(root, file)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()

            # Check if any service is imported
            services_used = []
            for s in service_map.keys():
                if f"from '../services/{s}'" in content or f"from '../../services/{s}'" in content or f"from '../../../services/{s}'" in content:
                    services_used.append(s)

            if not services_used:
                continue

            # Replace import
            import_re = re.compile(r"import\s+\{\s*([a-zA-Z]+Service)\s*(?:,\s*[a-zA-Z]+)*\s*\}\s+from\s+['\"](?:\.\./)+services/[a-zA-Z]+Service['\"];?\n")
            
            # Note: Sometimes types are imported from the service file (e.g. import { userService, User }). 
            # We already moved those to types or adapters, but we'll just wipe the service import and fix manually if needed.
            # Actually, `import { userService, User } from '../services/userService'` -> if we wipe it, `User` is missing.
            # Let's just remove the `userService` from the import statement.
            for s in services_used:
                content = re.sub(rf"import\s+{{\s*{s}\s*}}\s+from\s+['\"](?:\.\./)+services/{s}['\"];?\n", "", content)
                content = re.sub(rf"{s}\s*,\s*", "", content)
                content = re.sub(rf",\s*{s}", "", content)
                content = re.sub(rf"import\s+{{\s*{s}\s*}}\s+from\s+['\"](?:\.\./)+services/[a-zA-Z]+Service['\"];?\n", "", content)
                
                # If it's now `import { } from '../services/...'`, remove it
                content = re.sub(r"import\s+\{\s*\}\s+from\s+['\"](?:\.\./)+services/[a-zA-Z]+Service['\"];?\n", "", content)
                # Change the path of remaining types from services to adapters
                content = re.sub(r"from\s+(['\"])(?:\.\./)+services/([a-zA-Z]+)Service\1", r"from \1../adapters/\2AxiosAdapter\1", content)

            # Add DependencyContext import
            import_stmt = "import { useDependencies } from '../context/DependencyContext';\n"
            # Some files are deeper, let's just use the relative path appropriately
            depth = filepath.replace(src_dir, "").count(os.sep) - 1
            rel_path = "../" * depth + "context/DependencyContext"
            import_stmt = f"import {{ useDependencies }} from '{rel_path}';\n"

            if import_stmt not in content:
                lines = content.splitlines()
                last_import = 0
                for i, line in enumerate(lines):
                    if line.startswith("import "):
                        last_import = i
                lines.insert(last_import + 1, import_stmt.strip())
                content = "\n".join(lines) + "\n"

            # Find component/hook function definition
            component_re = re.compile(r"(const\s+[A-Z][a-zA-Z0-9_]*\s*(?::\s*React\.FC(?:<[^>]*>)?\s*)?=\s*(?:<[^>]*>)?\s*\([^)]*\)\s*=>\s*\{|export\s+function\s+[a-zA-Z0-9_]+\s*\([^)]*\)\s*\{|const\s+use[A-Z][a-zA-Z0-9_]*\s*=\s*\([^)]*\)\s*=>\s*\{)")
            
            match = component_re.search(content)
            if match:
                hook_vars = ", ".join([f"{service_map[s]}: {s}Port" for s in services_used])
                hook_call = f"\n    const {{ {hook_vars} }} = useDependencies();"
                
                # Replace service calls
                for s in services_used:
                    content = content.replace(s, f"{s}Port")

                content = content[:match.end()] + hook_call + content[match.end():]

                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"Refactored {filepath}")
            else:
                print(f"Could not find component definition in {filepath}")

process_directory(pages_dir)
process_directory(hooks_dir)
process_directory(components_dir)
