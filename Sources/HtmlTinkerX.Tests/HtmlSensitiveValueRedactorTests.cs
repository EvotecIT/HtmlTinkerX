namespace HtmlTinkerX.Tests;

public class HtmlSensitiveValueRedactorTests {
    [Fact]
    public void RedactSensitiveStructuredText_RedactsStructuredOAuthState() {
        string redacted = HtmlSensitiveValueRedactor.RedactSensitiveStructuredText(
            "{ \"state\": \"oauth-state-secret\", appState: \"visible-app-state\" }; window.state = 'window-state-secret'; window.appState = 'visible-window-state';");

        Assert.DoesNotContain("oauth-state-secret", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("window-state-secret", redacted, System.StringComparison.Ordinal);
        Assert.Contains("visible-app-state", redacted, System.StringComparison.Ordinal);
        Assert.Contains("visible-window-state", redacted, System.StringComparison.Ordinal);
    }

    [Fact]
    public void RedactSensitiveQueryValues_RedactsOAuthErrorDescription() {
        string redacted = HtmlSensitiveValueRedactor.RedactSensitiveQueryValues(
            "https://login.example/callback?error=access_denied&error_description=user-secret-detail&session_state=session-secret");

        Assert.Contains("error=<redacted>", redacted, System.StringComparison.Ordinal);
        Assert.Contains("error_description=<redacted>", redacted, System.StringComparison.Ordinal);
        Assert.Contains("session_state=<redacted>", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("user-secret-detail", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("session-secret", redacted, System.StringComparison.Ordinal);
    }

    [Fact]
    public void RedactSensitiveEvidenceText_RedactsSensitiveTextareasAndPasswordInputs() {
        string redacted = HtmlSensitiveValueRedactor.RedactSensitiveEvidenceText(
            "<form><textarea name=\"SAMLResponse\">textarea-saml-secret</textarea><textarea name=\"notes\">visible note</textarea><input type=\"password\" value=\"password-secret\"><input name=\"display\" value=\"visible-value\"></form>");

        Assert.Contains("<textarea name=\"SAMLResponse\"><redacted></textarea>", redacted, System.StringComparison.Ordinal);
        Assert.Contains("<textarea name=\"notes\">visible note</textarea>", redacted, System.StringComparison.Ordinal);
        Assert.Contains("type=\"password\" value=\"<redacted>\"", redacted, System.StringComparison.Ordinal);
        Assert.Contains("name=\"display\" value=\"visible-value\"", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("textarea-saml-secret", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("password-secret", redacted, System.StringComparison.Ordinal);
    }
}
