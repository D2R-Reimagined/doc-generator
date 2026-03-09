import os
import json
import re

def find_text_entries(data, results):
    # The user mentioned "text": and textstring and textstrings
    # I'll use lowercase for comparison to handle variations if they exist
    target_keys = {"text", "textstring", "textstrings"}
    
    if isinstance(data, dict):
        for key, value in data.items():
            if key.lower() in target_keys:
                if isinstance(value, str):
                    if not value.strip().startswith("@"):
                        results.append((key, value))
                elif isinstance(value, list):
                    for item in value:
                        if isinstance(item, str) and not item.strip().startswith("@"):
                            results.append((key, item))
            find_text_entries(value, results)
    elif isinstance(data, list):
        for item in data:
            find_text_entries(item, results)

def strip_comments(json_str):
    # Remove C-style comments /* ... */
    json_str = re.sub(r'/\*.*?\*/', '', json_str, flags=re.DOTALL)
    # Remove single-line comments // ...
    json_str = re.sub(r'//.*', '', json_str)
    return json_str

def main():
    base_path = r"C:\z_GitHub\d2r-reimagined-mod\data\global\ui\layouts"
    output_file = "ui_text_extract.txt"
    
    all_extracted = {}
    
    if not os.path.exists(base_path):
        print(f"Error: Path not found: {base_path}")
        return

    for root, dirs, files in os.walk(base_path):
        for file in files:
            if file.endswith(".json"):
                file_path = os.path.join(root, file)
                try:
                    with open(file_path, "r", encoding="utf-8") as f_in:
                        content = f_in.read()
                        content = strip_comments(content)
                        # Handle trailing commas before ] or }
                        content = re.sub(r',\s*([\]}])', r'\1', content)
                        # Remove any remaining non-standard stuff if any
                        data = json.loads(content)
                    
                    extracted = []
                    find_text_entries(data, extracted)
                    if extracted:
                        all_extracted[file] = extracted
                except Exception as e:
                    # Fallback to regex if JSON parsing fails (D2R layout files can be weird)
                    try:
                        extracted = []
                        # Regex for "key": "value" or "key": ["value1", "value2"]
                        # This is a bit complex for a fallback, let's try a simpler one
                        # Look for "text": "...", "textstring": "...", "textstrings": [...]
                        
                        # Just search for the patterns in the raw text if JSON fails
                        content = strip_comments(content)
                        # Pattern for "key": "value"
                        pattern = r'"(text|textstring|textstrings)"\s*:\s*("(?:[^"\\]|\\.)*"|\[\s*(?:"(?:[^"\\]|\\.)*"\s*,\s*)*"(?:[^"\\]|\\.)*"\s*\])'
                        matches = re.findall(pattern, content, re.IGNORECASE)
                        
                        for key, val_raw in matches:
                            if val_raw.startswith('['):
                                # It's a list
                                items = re.findall(r'"((?:[^"\\]|\\.)*)"', val_raw)
                                for item in items:
                                    if not item.strip().startswith("@"):
                                        extracted.append((key, item))
                            else:
                                # It's a string
                                item = val_raw[1:-1] # remove quotes
                                if not item.strip().startswith("@"):
                                    extracted.append((key, item))
                        
                        if extracted:
                            all_extracted[file] = extracted
                    except:
                        pass

    with open(output_file, "w", encoding="utf-8") as f_out:
        if not all_extracted:
            f_out.write("No matching entries found.")
        else:
            for file in sorted(all_extracted.keys()):
                f_out.write(f"{file}\n")
                for key, val in all_extracted[file]:
                    # Escape newlines to keep each entry on one line
                    val_clean = val.replace('\n', '\\n')
                    f_out.write(f'  {key}: "{val_clean}"\n')
                f_out.write("\n")
    print(f"Done. Output written to {output_file}")

if __name__ == "__main__":
    main()
