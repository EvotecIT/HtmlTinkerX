using HtmlTinkerX;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace HtmlTinkerX.Tests;

/// <summary>
/// Tests the <see cref="InternalLogger"/> utility.
/// </summary>
public class InternalLoggerTests {
    [Fact]
    public void Events_AreRaisedAndMessagesWritten() {
        InternalLogger logger = new InternalLogger();
        List<LogEventArgs> verboseEvents = new();
        List<LogEventArgs> warningEvents = new();
        List<LogEventArgs> errorEvents = new();
        List<LogEventArgs> debugEvents = new();
        List<LogEventArgs> infoEvents = new();
        List<LogEventArgs> progressEvents = new();

        logger.OnVerboseMessage += (_, e) => verboseEvents.Add(e);
        logger.OnWarningMessage += (_, e) => warningEvents.Add(e);
        logger.OnErrorMessage += (_, e) => errorEvents.Add(e);
        logger.OnDebugMessage += (_, e) => debugEvents.Add(e);
        logger.OnInformationMessage += (_, e) => infoEvents.Add(e);
        logger.OnProgressMessage += (_, e) => progressEvents.Add(e);

        logger.IsVerbose = true;
        logger.IsWarning = true;
        logger.IsError = true;
        logger.IsDebug = true;
        logger.IsInformation = true;
        logger.IsProgress = true;

        using StringWriter sw = new();
        TextWriter originalOut = Console.Out;
        Console.SetOut(sw);
        try {
            logger.WriteVerbose("verbose message");
            logger.WriteWarning("warning message");
            logger.WriteError("error message");
            logger.WriteDebug("debug {0}", 1);
            logger.WriteInformation("info message");
            logger.WriteProgress("activity", "operation", 50, 1, 2);
        } finally {
            Console.SetOut(originalOut);
        }

        string output = sw.ToString();
        Assert.Contains("verbose message", output);
        Assert.Contains("[warning] warning message", output);
        Assert.Contains("[error] error message", output);
        Assert.Contains("[debug] debug 1", output);
        Assert.Contains("[information] info message", output);
        Assert.Contains("[progress] activity: activity / operation: operation / percent completed: 50% (1 out of 2)", output);

        Assert.Single(verboseEvents);
        Assert.Equal("verbose message", verboseEvents[0].Message);
        Assert.Single(warningEvents);
        Assert.Equal("warning message", warningEvents[0].Message);
        Assert.Single(errorEvents);
        Assert.Equal("error message", errorEvents[0].Message);
        Assert.Single(debugEvents);
        Assert.Equal("debug {0}", debugEvents[0].Message);
        Assert.Equal("debug 1", debugEvents[0].FullMessage);
        Assert.Single(infoEvents);
        Assert.Equal("info message", infoEvents[0].Message);
        Assert.Single(progressEvents);
        Assert.Equal("activity", progressEvents[0].ProgressActivity);
        Assert.Equal("operation", progressEvents[0].ProgressCurrentOperation);
        Assert.Equal(50, progressEvents[0].ProgressPercentage);
        Assert.Equal(1, progressEvents[0].ProgressCurrentSteps);
        Assert.Equal(2, progressEvents[0].ProgressTotalSteps);
    }
}