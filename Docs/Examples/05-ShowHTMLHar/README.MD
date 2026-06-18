# `Show-HtmlBrowserHar`

## 📖 Summary

`Show-HtmlBrowserHar` parses a HAR file and returns a PowerShell object. It can also
generate a small HTML viewer showing the captured requests. Use `-Open` to
launch the HTML file after creation.

---

## 💡 Example

```powershell
$har = Show-HtmlBrowserHar -Path './traffic.har' -Open
$har.Log.Entries | Format-Table
```

This command creates `traffic.html` next to the original HAR file, opens it in
your default browser, and returns the parsed HAR object for further processing.
