# `Show-HTMLHar`

## 📖 Summary

`Show-HTMLHar` converts a HAR file into a small HTML viewer. It reads the HAR and
creates an `.html` page with a simple table listing the captured requests.
Pass `-Open` to automatically view the generated file.

---

## 💡 Example

```powershell
Show-HTMLHar -Path './traffic.har' -Open
```

This command creates `traffic.html` next to the original HAR file and opens it in
your default browser.
