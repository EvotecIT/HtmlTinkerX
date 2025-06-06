Describe 'Optimize-JavaScript' {
    It 'Given formatted JS content - Should minimize it' {
        $JSFormatted = @"
(function() {
    function main() {
        var tabButtons = [].slice.call(document.querySelectorAll('ul.tab-nav li a.buttonTab'));
        tabButtons.map(function(button) {
            button.addEventListener('click', function() {
                document.querySelector('li a.active.buttonTab').classList.remove('active');
                button.classList.add('active');
                document.querySelector('.tab-pane.active').classList.remove('active');
                document.querySelector(button.getAttribute('href')).classList.add('active')
            })
        })
    }
    if (document.readyState !== 'loading') {
        main()
    } else {
        document.addEventListener('DOMContentLoaded', main)
    }
})();
"@
        $ExpectedOutput = '(function(){function n(){var n=[].slice.call(document.querySelectorAll("ul.tab-nav li a.buttonTab"));n.map(function(n){n.addEventListener("click",function(){document.querySelector("li a.active.buttonTab").classList.remove("active");n.classList.add("active");document.querySelector(".tab-pane.active").classList.remove("active");document.querySelector(n.getAttribute("href")).classList.add("active")})})}document.readyState!=="loading"?n():document.addEventListener("DOMContentLoaded",n)})()'
        $Output = Optimize-JavaScript -Content $JSFormatted
        $Output | Should -Be $ExpectedOutput
    }
}
