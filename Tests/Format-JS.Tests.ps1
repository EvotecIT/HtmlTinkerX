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
}