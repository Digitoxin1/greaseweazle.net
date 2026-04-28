Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports System.IO

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed bandwidth runtime state.
    Public Class BandwidthRuntimePreview
        Public Property Live As Boolean
        Public Property Device As String
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

        ' Python map: src/greaseweazle/tools/bandwidth.py::measure_bandwidth
        Public Shared Sub MeasureBandwidth(usbClient As Unit, output As TextWriter)
            output.WriteLine("")
            output.WriteLine(String.Format("{0,-19}{1,-7}/   {2,-7}/   {3,-7}", "", "Min.", "Mean", "Max."))

            Dim seed As UInteger = &H12345678UI
            Dim count = 1000000
            Dim writeBuffer = GenerateRandomBuffer(count, seed)

            Dim sw = Diagnostics.Stopwatch.StartNew()
            Dim ack = usbClient.SinkBytes(writeBuffer, seed)
            sw.Stop()
            Dim avgWrite = (count * 8.0) / (sw.Elapsed.TotalSeconds * 1000000.0)
            Dim writeStats = usbClient.BwStats()
            output.WriteLine(String.Format(Globalization.CultureInfo.InvariantCulture,
                                           "Write Bandwidth: {0,8:F3} / {1,8:F3} / {2,8:F3} Mbps",
                                           writeStats.Item1, avgWrite, writeStats.Item2))
            ' Python: soft-fail on garbled write — print error and return.
            If ack <> 0 Then
                output.WriteLine("ERROR: USB write data garbled (Host -> Device)")
                Return
            End If

            sw.Restart()
            Dim sourceBuffer = usbClient.SourceBytes(count, seed)
            sw.Stop()
            Dim avgRead = (count * 8.0) / (sw.Elapsed.TotalSeconds * 1000000.0)
            Dim readStats = usbClient.BwStats()
            output.WriteLine(String.Format(Globalization.CultureInfo.InvariantCulture,
                                           "Read Bandwidth:  {0,8:F3} / {1,8:F3} / {2,8:F3} Mbps",
                                           readStats.Item1, avgRead, readStats.Item2))
            ' Python: soft-fail on garbled read — print error and return.
            If sourceBuffer IsNot Nothing AndAlso Not sourceBuffer.SequenceEqual(writeBuffer) Then
                output.WriteLine("ERROR: USB read data garbled (Device -> Host)")
                Return
            End If

            Dim estMin = EstimateConsistentMinimumBandwidth(readStats.Item1, writeStats.Item1)
            output.WriteLine("")
            output.WriteLine(String.Format(Globalization.CultureInfo.InvariantCulture, "Estimated Consistent Min. Bandwidth: {0:F3} Mbps", estMin))
            Dim status = BuildBandwidthStatus(estMin)
            If status.StartsWith("warning=", StringComparison.Ordinal) Then
                Dim value = status.Substring("warning=".Length)
                output.WriteLine(String.Format(" -> **WARNING** BELOW REQUIRED MIN.: {0} Mbps", value))
            Else
                Dim parts = status.Split(";"c)
                Dim maxFlux = parts(0).Substring("max_flux=".Length)
                Dim minAve = parts(1).Substring("min_ave_flux=".Length)
                output.WriteLine(String.Format(" -> Max. Flux Rate: {0} Msamples/sec", maxFlux))
                output.WriteLine(String.Format(" -> Min. Ave. Flux: {0} us", minAve))
            End If
        End Sub

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
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As BandwidthRuntimePreview
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
            Return New BandwidthRuntimePreview With {.Live = live, .Device = device}
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
