Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports System.IO

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `bandwidth` action.
    Public Class BandwidthOptions
        Public Property Live As Boolean = True
        Public Property Device As String

        Public Shared Function FromArgs(args As IReadOnlyList(Of String)) As BandwidthOptions
            Return Bandwidth.BuildRuntimePreview(args)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/bandwidth.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Bandwidth

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/bandwidth.py::generate_random_buffer
        Public Shared Function GenerateRandomBuffer(count As Integer, seed As UInteger) As Byte()
            Dim output As New List(Of Byte)(Math.Max(count, 0))
            Dim r = seed
            For i = 0 To count - 1
                output.Add(CByte(r And &HFFUI))
                If (r And 1UI) <> 0UI Then
                    r = (r >> 1) Xor &H80000062UI
                Else
                    r >>= 1
                End If
            Next
            Return output.ToArray()
        End Function

        ' Python map: src/greaseweazle/tools/bandwidth.py::measure_bandwidth.
        ' Pure-data version: does the round-trip USB I/O, returns a typed
        ' BandwidthResult capturing every measurement the legacy text-mode
        ' helper used to print. The CLI front-end's BandwidthFormatter
        ' renders the structured result to the console.
        Public Shared Function Measure(usbClient As Unit) As Greaseweazle.Actions.BandwidthResult
            Dim seed As UInteger = &H12345678UI
            Dim count = 1000000
            Dim writeBuffer = GenerateRandomBuffer(count, seed)

            Dim sw = Diagnostics.Stopwatch.StartNew()
            Dim ack = usbClient.SinkBytes(writeBuffer, seed)
            sw.Stop()
            Dim avgWrite = (count * 8.0) / (sw.Elapsed.TotalSeconds * 1000000.0)
            Dim writeStats = usbClient.BwStats()
            Dim writeRow As New Greaseweazle.Actions.BandwidthRow(
                writeStats.Item1, avgWrite, writeStats.Item2)

            ' Python: soft-fail on garbled write — emit the row, set the
            ' garble flag, and return; the caller surfaces the error and
            ' skips the read measurement.
            If ack <> 0 Then
                Return New Greaseweazle.Actions.BandwidthResult(
                    writeRow:=writeRow,
                    writeGarbled:=True,
                    readRow:=Nothing,
                    readGarbled:=False,
                    summary:=Nothing)
            End If

            sw.Restart()
            Dim sourceBuffer = usbClient.SourceBytes(count, seed)
            sw.Stop()
            Dim avgRead = (count * 8.0) / (sw.Elapsed.TotalSeconds * 1000000.0)
            Dim readStats = usbClient.BwStats()
            Dim readRow As New Greaseweazle.Actions.BandwidthRow(
                readStats.Item1, avgRead, readStats.Item2)

            ' Python: soft-fail on garbled read — emit the row, set the
            ' garble flag, and skip the summary.
            If sourceBuffer IsNot Nothing AndAlso Not sourceBuffer.SequenceEqual(writeBuffer) Then
                Return New Greaseweazle.Actions.BandwidthResult(
                    writeRow:=writeRow,
                    writeGarbled:=False,
                    readRow:=readRow,
                    readGarbled:=True,
                    summary:=Nothing)
            End If

            Dim estMin = EstimateConsistentMinimumBandwidth(readStats.Item1, writeStats.Item1)
            Dim required = ComputeRequiredMinimumBandwidth()
            Dim summary As Greaseweazle.Actions.BandwidthSummary
            If required > estMin Then
                summary = New Greaseweazle.Actions.BandwidthSummary(
                    estimatedMinMbps:=estMin,
                    belowRequirement:=True,
                    requiredMinMbps:=required,
                    maxFluxRateMsps:=0,
                    minAvgFluxUs:=0)
            Else
                ' Same arithmetic as BuildBandwidthStatus.
                Dim maxFluxRateHz = ((estMin * 0.9) * 1000000.0) / 8.0
                summary = New Greaseweazle.Actions.BandwidthSummary(
                    estimatedMinMbps:=estMin,
                    belowRequirement:=False,
                    requiredMinMbps:=0,
                    maxFluxRateMsps:=maxFluxRateHz / 1000000.0,
                    minAvgFluxUs:=1000000.0 / maxFluxRateHz)
            End If

            Return New Greaseweazle.Actions.BandwidthResult(
                writeRow:=writeRow,
                writeGarbled:=False,
                readRow:=readRow,
                readGarbled:=False,
                summary:=summary)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeRequiredMinimumBandwidth)
        Public Shared Function ComputeRequiredMinimumBandwidth() As Double
            Dim twoByteUs = 249.0 / 72.0
            Return 16.0 / twoByteUs
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration EstimateConsistentMinimumBandwidth)
        Public Shared Function EstimateConsistentMinimumBandwidth(minRead As Double, minWrite As Double) As Double
            Return 0.9 * Math.Min(minRead, minWrite)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildBandwidthStatus)
        Public Shared Function BuildBandwidthStatus(estimatedMin As Double) As String
            Dim required = ComputeRequiredMinimumBandwidth()
            If required > estimatedMin Then
                Return String.Format(Globalization.CultureInfo.InvariantCulture, "warning={0:F3}", required)
            End If
            Dim maxFluxRate = ((estimatedMin * 0.9) * 1000000.0) / 8.0
            Return String.Format(
                Globalization.CultureInfo.InvariantCulture,
                "max_flux={0:F3};min_ave_flux={1:F3}",
                maxFluxRate / 1000000.0,
                1000000.0 / maxFluxRate)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As BandwidthOptions
            Dim live = True
            Dim device As String = Nothing
            Dim positionals As New List(Of String)()
            Dim i = 0
            While i < args.Count
                Dim rawToken = args(i)
                If String.Equals(rawToken, "--", StringComparison.Ordinal) Then
                    For j = i To args.Count - 1
                        positionals.Add(args(j))
                    Next
                    Exit While
                End If
                Dim token = rawToken
                Dim inlineValue As String = Nothing
                Dim equalsIndex = rawToken.IndexOf("="c)
                If rawToken.StartsWith("--", StringComparison.Ordinal) AndAlso equalsIndex > 2 Then
                    token = rawToken.Substring(0, equalsIndex)
                    inlineValue = rawToken.Substring(equalsIndex + 1)
                End If
                Select Case token
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case "--device"
                        device = TakeOptionValue(args, i, token, inlineValue)
                    Case Else
                        If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While
            If positionals.Count > 0 Then
                Throw New FatalException(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            Return New BandwidthOptions With {.Live = live, .Device = device}
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CheckOptionValue)
        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("--", StringComparison.Ordinal) Then
                    hasValue = False
                End If
            End If
            ErrorHandling.Check(hasValue, String.Format("missing value for option {0}", optionName))
        End Sub

        Private Shared Function TakeOptionValue(args As IReadOnlyList(Of String),
                                                ByRef index As Integer,
                                                optionName As String,
                                                inlineValue As String) As String
            If inlineValue IsNot Nothing Then
                Return inlineValue
            End If
            index += 1
            CheckOptionValue(args, index, optionName)
            Return args(index)
        End Function

    End Class

End Namespace
