Import-Module .\PSParseHTML.psd1 -Force

$Script = @'
function greet(name){
    var data = [name];
    return data.map(function(x){
        return "Hello " + x;
    }).join().toUpperCase();
}

greet("World");
'@

$opts = @{
    Content              = $Script
    IndentSize           = 2
    IndentChar           = ' '
    BraceStyle           = 'Expand'
    IndentWithTabs       = $false
    KeepArrayIndentation = $true
    KeepFunctionIndentation = $false
    BreakChainedMethods  = $true
    MaxPreserveNewlines  = 2
    JslintHappy          = $true
}

Format-JavaScript @opts
