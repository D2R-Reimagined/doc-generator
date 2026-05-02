# D2TxtImporter
This program is used to process all the .txt files of your mod and generate documentation for it.

[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=HW6L5XFFAFZ5J&source=url) Buy me a cup of coffee if you feel like it. I spend a significant amount of time on this project.

# Guide
To function correctly you need the following .tbl files (they can be named however you like, as long as they have the .tbl extension):
- d2data.mpq
  - string.tbl
- patch_d2.mpq
  - patchstring.tbl
- d2exp.mpq
  - patchstring.tbl
  - expansionstring.tbl
  
## Client Application
- Excel Directory: Absolute path to your .txt files
- Table Directory: Absolute path to your .tbl files
- Output Directory: The directory you want your output to go to

## Console Application
To get information use:
.\D2TxtImporter.console.exe --help

## Contributing
Have an idea or found a gap? Please open an issue with details and sample files so it can be reproduced and addressed.

# Issues
If you open an issue, please provide the required .txt and .tbl files so I can debug it. As mentioned above, I only have the vanilla files for 1.13c to test with. Also attach the errorlog.txt file.

# Credits
- .tbl import functionality: https://github.com/kambala-decapitator/QTblEditor
- Ascended1962 (Creator of Diablo 2 Enriched): General help with the project
