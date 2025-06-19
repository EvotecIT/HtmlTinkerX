Import-Module .\PSParseHTML.psd1 -Force

$Script = 'function greet(name){var data=[name];return data.map(function(x){return "Hello "+x;}).join().toUpperCase();}greet("World");'

Format-JavaScript -Content $Script -IndentSize 2 -BraceStyle Expand -IndentWithTabs -KeepArrayIndentation -BreakChainedMethods
