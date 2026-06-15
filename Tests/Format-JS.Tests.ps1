Describe 'Format-JS' {
    It 'Given minified JS content - Should Format it' {
        $CompareTo = @"
(function() {
    function main() {
        var tabButtons = [].slice.call(document.querySelectorAll("ul.tab-nav li a.buttonTab"));
        tabButtons.map(function(button) {
            button.addEventListener("click", function() {
                document.querySelector("li a.active.buttonTab").classList.remove("active");
                button.classList.add("active");
                document.querySelector(".tab-pane.active").classList.remove("active");
                document.querySelector(button.getAttribute("href")).classList.add("active")
            })
        })
    }
    if (document.readyState !== "loading") {
        main()
    } else {
        document.addEventListener("DOMContentLoaded", main)
    }
})();
"@.Replace("`r`n", "`n")
        $JSContent = '(function(){function main(){var tabButtons = [].slice.call(document.querySelectorAll("ul.tab-nav li a.buttonTab"));tabButtons.map(function(button){button.addEventListener("click",function(){document .querySelector("li a.active.buttonTab") .classList.remove("active");button.classList.add("active");document .querySelector(".tab-pane.active") .classList.remove("active");document .querySelector(button.getAttribute("href")) .classList.add("active")})})}if(document.readyState!== "loading"){main()}else{document.addEventListener("DOMContentLoaded",main)}})();'
        $cmd = Get-Command Format-JavaScript
        $cmd.CommandType | Should -Be 'Cmdlet'
        $Output = Format-JavaScript -Content $JSContent
        $Output | Should -Be $CompareTo
    }

    It 'Supports custom options' {
        $Content = 'function x(){return 1;};'
        $Expected = @"
function x()
{
  return 1;
};
"@
        $Output = Format-JavaScript -Content $Content -IndentSize 2 -BraceStyle Expand
        $Output.Replace("`r`n", "`n") | Should -Be $Expected.Replace("`r`n", "`n").TrimEnd()
    }

    It 'Exposes wrapping and long string splitting options' {
        $Content = "var payload='abcdefghijkl';"
        $Output = Format-JavaScript -Content $Content -SplitLongLine -MaxStringLiteralLength 4
        $Output.Replace("`r`n", "`n") | Should -Be @"
var payload = ('abcd' +
    'efgh' +
    'ijkl');
"@.Replace("`r`n", "`n").TrimEnd()
    }

    It 'Wraps before a long array string argument when requested' {
        $Content = "! function(n, r, e) { (r = e(2)(!1)).push([n.i, 'my really long string', `"`"]), n.exports = r }"
        $Output = Format-JavaScript -Content $Content -WrapLineLength 40
        $Output.Replace("`r`n", "`n") | Should -Be @"
! function(n, r, e) {
    (r = e(2)(!1)).push([n.i,
        'my really long string', ""]),
        n.exports = r
}
"@.Replace("`r`n", "`n").TrimEnd()
    }
}
