Imports System.Globalization
Imports System.IO
Imports Greaseweazle.Actions

Namespace Greaseweazle.Cli.Formatters

    ' Subscribes to RpmCommand events and renders the matching legacy
    ' lines:
    '   "Rate: {rpm:F3} rpm ; Period: {period_ms:F3} ms"   (per sample)
    '   "***"                                              (separator)
    '   "FASTEST:  Rate: ..."                              (summary)
    '   "Ar.Mean:  Rate: ..."
    '   "Median:   Rate: ..."
    '   "SLOWEST:  Rate: ..."
    Public NotInheritable Class RpmFormatter
        Implements IDisposable

        Private ReadOnly _command As RpmCommand
        Private ReadOnly _output As TextWriter
        Private ReadOnly _sampleHandler As EventHandler(Of RpmSampleMeasuredEventArgs)
        Private ReadOnly _summaryHandler As EventHandler(Of RpmSummaryReadyEventArgs)
        Private _disposed As Boolean

        Public Sub New(command As RpmCommand, output As TextWriter)
            _command = command
            _output = output
            _sampleHandler = AddressOf OnSampleMeasured
            _summaryHandler = AddressOf OnSummaryReady
            AddHandler _command.SampleMeasured, _sampleHandler
            AddHandler _command.SummaryReady, _summaryHandler
        End Sub

        Private Sub OnSampleMeasured(sender As Object, e As RpmSampleMeasuredEventArgs)
            _output.WriteLine(FormatSpeed(e.TimePerRev))
        End Sub

        Private Sub OnSummaryReady(sender As Object, e As RpmSummaryReadyEventArgs)
            Dim s = e.Summary
            _output.WriteLine("***")
            _output.WriteLine("FASTEST:  " & FormatSpeed(s.Fastest))
            _output.WriteLine("Ar.Mean:  " & FormatSpeed(s.Mean))
            _output.WriteLine("Median:   " & FormatSpeed(s.Median))
            _output.WriteLine("SLOWEST:  " & FormatSpeed(s.Slowest))
        End Sub

        Private Shared Function FormatSpeed(timePerRev As Double) As String
            Return String.Format(
                CultureInfo.InvariantCulture,
                "Rate: {0:F3} rpm ; Period: {1:F3} ms",
                60.0 / timePerRev,
                timePerRev * 1000.0)
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            RemoveHandler _command.SampleMeasured, _sampleHandler
            RemoveHandler _command.SummaryReady, _summaryHandler
        End Sub

    End Class

End Namespace
