This program is a command-line tool made for packing/unpacking PAK files from the video game Metaphor: ReFantazio.  
(Note: This program requires dotnet 8.0 or later to work!)  

## Features

- APK Mode: Drag and Drop an APK file into the program to extract all the DDS files inside.
-  - This will also dump a file list into FileList.txt for preserving order when repacking.
- DDS Folder Mode: Drag and Drop a folder containing DDS files (and a FileList.txt file) to compress and pack them into a PAK file to use in the game via mods.

# Usage
1. APK Mode
  
- Drag and drop an .apk file onto the executable.  
    The program will:  
        1. Read the APK file.  
        2. Extract all DDS texture files from the APK file into a folder named after the APK  
        3. Generate a FileList.txt containing the filenames (used for repacking).  

2. DDS Folder Mode  
  
-  Drag and drop a folder containing .dds files and a FileList.txt.  
    The program will:  
        1. Compress each DDS file using LZ4 compression (HC level).  
        2. Generate a new APK containing the compressed DDS files.  
        3. The file order is maintained according to FileList.txt.  
