---
external help file: PSParseHTML-help.xml
Module Name: PSParseHTML
online version: https://github.com/EvotecIT/HtmlTinkerX
schema: 2.0.0
---
# Complete-HtmlRoute
## SYNOPSIS
Cmdlet that fulfills an intercepted browser route with a mocked response.

## SYNTAX
### Body (Default)
```powershell
Complete-HtmlRoute [-Route] <Object> [[-Body] <string>] [-Status <int>] [-ContentType <string>] [-Header <IDictionary>] [-Options <IDictionary>] [<CommonParameters>]
```

### BodyBytes
```powershell
Complete-HtmlRoute [-Route] <Object> -BodyBytes <byte[]> [-Status <int>] [-ContentType <string>] [-Header <IDictionary>] [-Options <IDictionary>] [<CommonParameters>]
```

### Json
```powershell
Complete-HtmlRoute [-Route] <Object> -Json <Object> [-Status <int>] [-ContentType <string>] [-Header <IDictionary>] [-Options <IDictionary>] [<CommonParameters>]
```

### Path
```powershell
Complete-HtmlRoute [-Route] <Object> -Path <string> [-Status <int>] [-ContentType <string>] [-Header <IDictionary>] [-Options <IDictionary>] [<CommonParameters>]
```

## DESCRIPTION
Cmdlet that fulfills an intercepted browser route with a mocked response.

## EXAMPLES

### EXAMPLE 1
```powershell
Register-HtmlRoute -Session $session -Pattern '**/api/data' -ScriptBlock {
    param($route)
    Complete-HtmlRoute -Route $route -Status 200 -ContentType 'application/json' -Body '{"status":"ok"}'
}
```


### EXAMPLE 2
```powershell
$route | Complete-HtmlRoute -Json @{ status = 'ok'; count = 1 }
```


## PARAMETERS

### -Body
Text response body.

```yaml
Type: String
Parameter Sets: Body
Aliases: None
Possible values:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -BodyBytes
Binary response body.

```yaml
Type: Byte[]
Parameter Sets: BodyBytes
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ContentType
Response content type.

```yaml
Type: String
Parameter Sets: Body, BodyBytes, Json, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Header
Response headers.

```yaml
Type: IDictionary
Parameter Sets: Body, BodyBytes, Json, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Json
Object serialized by Playwright as a JSON response.

```yaml
Type: Object
Parameter Sets: Json
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Options
Response options supplied as a hashtable.

```yaml
Type: IDictionary
Parameter Sets: Body, BodyBytes, Json, Path
Aliases: Option
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
File path used as the response body.

```yaml
Type: String
Parameter Sets: Path
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Route
Route object received by a Register-HtmlRoute script block.

```yaml
Type: Object
Parameter Sets: Body, BodyBytes, Json, Path
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Status
HTTP status code to return.

```yaml
Type: Int32
Parameter Sets: Body, BodyBytes, Json, Path
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `System.Object`

## OUTPUTS

- `None`

## RELATED LINKS

- None
