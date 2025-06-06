Describe 'Optimize-CSS' {
    It 'Given formatted CSS content - Should minimize it' {
        $CssCode = @"
        .tabsWrapper {
            text-align: center;
            margin: 10px auto;
            font-family: 'Roboto', sans-serif !important;
        }

        .tabs {
            margin-top: 10px;
            font-size: 15px;
            padding: 0px;
            list-style: none;
            background: #fff;
            box-shadow: 0px 5px 20px rgba(0, 0, 0, 0.1);
            border-radius: 4px;
            position: relative;
        }

        .tabs .round {
            border-radius: 4px;
        }

        .tabs a {
            text-decoration: none;
            color: #777;
            text-transform: uppercase;
            padding: 10px 20px;
            display: inline-block;
            position: relative;
            z-index: 1;
            transition-duration: 0.6s;
        }

        .tabs a.active {
            color: #fff;
        }

        .tabs a i {
            margin-right: 5px;
        }

        .tabs .selector {
            display: none;
            height: 100%;
            position: absolute;
            left: 0px;
            top: 0px;
            right: 0px;
            bottom: 0px;
            z-index: 1;
            border-radius: 4px;
            background: ColorSelector;
        }

        .tabs-content {
            display: none;
        }

        .tabs-content.active {
            display: block;
        }
"@

        $ExpectedOutput = ".tabsWrapper{text-align:center;margin:10px auto;font-family:'Roboto',sans-serif!important}.tabs{margin-top:10px;font-size:15px;padding:0;list-style:none;background:#fff;box-shadow:0 5px 20px rgba(0,0,0,.1);border-radius:4px;position:relative}.tabs .round{border-radius:4px}.tabs a{text-decoration:none;color:#777;text-transform:uppercase;padding:10px 20px;display:inline-block;position:relative;z-index:1;transition-duration:.6s}.tabs a.active{color:#fff}.tabs a i{margin-right:5px}.tabs .selector{display:none;height:100%;position:absolute;left:0;top:0;right:0;bottom:0;z-index:1;border-radius:4px;background:ColorSelector}.tabs-content{display:none}.tabs-content.active{display:block}"
        $Output = Optimize-CSS -Css $CssCode
        $Output | Should -Be $ExpectedOutput
    }
}