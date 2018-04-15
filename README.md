# NUnit HTML Report Generator

Converts NUnit XML to a bootstrap-based static web page.

NOTE: customized to support the Jasmine NUnitXML Reporter, which appears to take some liberties with the expected format.

Taken from https://github.com/JatechUK/NUnit-HTML-Report-Generator and customized.

## USAGE:

    nreporter.exe [input-filename] [output-filename]

   If the output filename is omitted, the input file is given an .html extension and used.

   Errors if the input file does not exist; silently overwrites the output file if it already exists.

