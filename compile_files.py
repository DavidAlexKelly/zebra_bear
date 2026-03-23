#!/usr/bin/env python3
"""
Compiles all text-based files in a directory into one long text file,
separated by ========filename======== headers.
"""

import os
import argparse
import sys

# Text-based file extensions to include
TEXT_EXTENSIONS = {
    # Programming languages
    '.py', '.js', '.ts', '.tsx', '.jsx', '.java', '.c', '.cpp', '.cc', '.cxx',
    '.h', '.hpp', '.hxx', '.cs', '.go', '.rs', '.rb', '.php', '.swift',
    '.kt', '.kts', '.scala', '.clj', '.cljs', '.erl', '.ex', '.exs',
    '.hs', '.ml', '.mli', '.fs', '.fsx', '.lua', '.pl', '.pm', '.r',
    '.R', '.dart', '.v', '.vhdl', '.vhd', '.sv', '.svh','.cs','csproj',
    
    # Web
    '.html', '.htm', '.css', '.scss', '.sass', '.less', '.styl',
    '.vue', '.svelte', '.astro',
    
    # Data / Config
    '.json', '.yaml', '.yml', '.toml', '.xml', '.csv', '.tsv',
    '.ini', '.cfg', '.conf', '.env', '.properties',
    
    # Documentation / Text
    '.txt', '.md', '.markdown', '.rst', '.adoc', '.tex', '.latex',
    '.org', '.wiki', '.rtf',
    
    # Shell / Scripts
    '.sh', '.bash', '.zsh', '.fish', '.bat', '.cmd', '.ps1', '.psm1',
    
    # Build / DevOps
    '.dockerfile', '.dockerignore', '.gitignore', '.gitattributes',
    '.editorconfig', '.eslintrc', '.prettierrc', '.babelrc',
    '.makefile', '.cmake', '.gradle', '.sbt',
    
    # SQL
    '.sql', '.psql', '.plsql',
    
    # Other
    '.graphql', '.gql', '.proto', '.thrift', '.avsc',
    '.tf', '.tfvars', '.hcl',
    '.nix', '.dhall',
    '.asm', '.s',
    '.lisp', '.el', '.scm', '.rkt',
    '.vim', '.vimrc',
    '.log', '.diff', '.patch',
}

# Also match files with no extension but known names
TEXT_FILENAMES = {
    'Makefile', 'Dockerfile', 'Vagrantfile', 'Gemfile', 'Rakefile',
    'Procfile', 'Brewfile', 'Justfile',
    '.gitignore', '.gitattributes', '.dockerignore', '.editorconfig',
    '.eslintrc', '.prettierrc', '.babelrc', '.env', '.flake8',
    'requirements.txt', 'LICENSE', 'README', 'CHANGELOG', 'CONTRIBUTING',
    'AUTHORS', 'NOTICE', 'TODO', 'COPYING',
}

# Directories to skip
SKIP_DIRS = {
    'node_modules', '.git', '.svn', '.hg', '__pycache__', '.mypy_cache',
    '.pytest_cache', '.tox', '.venv', 'venv', 'env', '.env',
    'dist', 'build', '.next', '.nuxt', '.output',
    'target', 'bin', 'obj',
    '.idea', '.vscode', '.vs',
    'vendor', 'bower_components',
    '.cache', '.parcel-cache',
    'coverage', '.nyc_output',
    'egg-info', '.eggs',
}

# Max file size to include (default 1MB) to avoid accidentally including huge files
MAX_FILE_SIZE = 1 * 1024 * 1024  # 1 MB


def is_text_file(filepath):
    """Determine if a file should be included based on extension or filename."""
    basename = os.path.basename(filepath)
    _, ext = os.path.splitext(basename)
    
    if ext.lower() in TEXT_EXTENSIONS:
        return True
    if basename in TEXT_FILENAMES:
        return True
    
    return False


def should_skip_dir(dirname):
    """Check if a directory should be skipped."""
    return dirname in SKIP_DIRS or dirname.startswith('.')


def collect_files(root_dir, respect_gitignore=False):
    """Walk the directory tree and collect all text-based files."""
    collected = []
    root_dir = os.path.abspath(root_dir)
    
    for dirpath, dirnames, filenames in os.walk(root_dir):
        # Modify dirnames in-place to skip certain directories
        dirnames[:] = [d for d in dirnames if not should_skip_dir(d)]
        dirnames.sort()
        
        for filename in sorted(filenames):
            filepath = os.path.join(dirpath, filename)
            
            # Make sure the file is actually inside root_dir (no symlink escapes)
            real_filepath = os.path.realpath(filepath)
            real_root = os.path.realpath(root_dir)
            if not real_filepath.startswith(real_root + os.sep) and real_filepath != real_root:
                continue
            
            # Check file size
            try:
                if os.path.getsize(filepath) > MAX_FILE_SIZE:
                    continue
            except OSError:
                continue
            
            if is_text_file(filepath):
                rel_path = os.path.relpath(filepath, root_dir)
                collected.append((rel_path, filepath))
    
    return collected


def compile_files(root_dir, output_file, max_size_mb=1):
    """Compile all text files into a single output file."""
    global MAX_FILE_SIZE
    MAX_FILE_SIZE = max_size_mb * 1024 * 1024
    
    files = collect_files(root_dir)
    
    if not files:
        print("No text-based files found.")
        return 0, 0
    
    # Make sure we don't include the output file itself
    output_abs = os.path.abspath(output_file)
    files = [(rel, abs_path) for rel, abs_path in files if os.path.abspath(abs_path) != output_abs]
    
    count = 0
    errors = 0
    
    with open(output_file, 'w', encoding='utf-8') as out:
        for rel_path, abs_path in files:
            separator = f"{'=' * 8} {rel_path} {'=' * 8}"
            
            try:
                with open(abs_path, 'r', encoding='utf-8', errors='replace') as f:
                    content = f.read()
                
                out.write(separator + '\n')
                out.write(content)
                if not content.endswith('\n'):
                    out.write('\n')
                out.write('\n')
                
                count += 1
                print(f"  ✓ {rel_path}")
                
            except Exception as e:
                errors += 1
                print(f"  ✗ {rel_path} — Error: {e}", file=sys.stderr)
    
    return count, errors


def main():
    # Determine the directory where this script lives
    script_dir = os.path.dirname(os.path.abspath(__file__))

    parser = argparse.ArgumentParser(
        description="Compile all text-based files in a directory into one file."
    )
    parser.add_argument(
        'directory',
        nargs='?',
        default=None,
        help='Root directory to scan (default: the directory where this script lives)'
    )
    parser.add_argument(
        '-o', '--output',
        default='compiled_output.txt',
        help='Output file path (default: compiled_output.txt)'
    )
    parser.add_argument(
        '--max-size',
        type=float,
        default=1.0,
        help='Max individual file size in MB to include (default: 1.0)'
    )
    parser.add_argument(
        '--list-only',
        action='store_true',
        help='Only list files that would be included, without compiling'
    )
    parser.add_argument(
        '--extra-ext',
        nargs='*',
        default=[],
        help='Additional file extensions to include (e.g. .custom .xyz)'
    )
    
    args = parser.parse_args()
    
    # Add any extra extensions
    for ext in args.extra_ext:
        if not ext.startswith('.'):
            ext = '.' + ext
        TEXT_EXTENSIONS.add(ext.lower())
    
    # Use the script's own directory as the default, NOT the cwd
    if args.directory is None:
        root_dir = script_dir
    else:
        root_dir = os.path.abspath(args.directory)
    
    if not os.path.isdir(root_dir):
        print(f"Error: '{root_dir}' is not a valid directory.", file=sys.stderr)
        sys.exit(1)
    
    # If output path is relative, place it inside root_dir
    if not os.path.isabs(args.output):
        output_file = os.path.join(root_dir, args.output)
    else:
        output_file = args.output
    
    print(f"Scanning: {root_dir}")
    print(f"Output:   {output_file}")
    print(f"Max size: {args.max_size} MB per file")
    print("-" * 50)
    
    if args.list_only:
        global MAX_FILE_SIZE
        MAX_FILE_SIZE = int(args.max_size * 1024 * 1024)
        files = collect_files(root_dir)
        output_abs = os.path.abspath(output_file)
        files = [(rel, abs_path) for rel, abs_path in files if os.path.abspath(abs_path) != output_abs]
        
        for rel_path, _ in files:
            print(f"  {rel_path}")
        print(f"\nTotal: {len(files)} files")
    else:
        count, errors = compile_files(root_dir, output_file, args.max_size)
        print("-" * 50)
        print(f"Done! Compiled {count} files into '{output_file}'")
        if errors:
            print(f"  ({errors} files had errors and were skipped)")
        
        # Show output file size
        try:
            size = os.path.getsize(output_file)
            if size > 1024 * 1024:
                print(f"  Output size: {size / (1024*1024):.2f} MB")
            elif size > 1024:
                print(f"  Output size: {size / 1024:.1f} KB")
            else:
                print(f"  Output size: {size} bytes")
        except OSError:
            pass


if __name__ == '__main__':
    main()