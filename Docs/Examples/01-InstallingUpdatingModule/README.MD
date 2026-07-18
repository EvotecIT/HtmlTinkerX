### Install from PSGallery

```powershell
Install-Module -Name PSParseHTML -AllowClobber -Force
```

`-Force` reinstalls the selected version when necessary. `-AllowClobber` permits
commands with the same names as commands already available in the session.

### Update from PSGallery

```powershell
Update-Module -Name PSParseHTML
```

If the module is already imported, start a new PowerShell session before using
the newly installed version. Pin a tested version in production automation when
parameter or behavior changes would be disruptive.
