Imports Greaseweazle.Core

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed rpm runtime state.
    Public Class RpmRuntimePreview
        Public Property Nr As Integer
        Public Property Live As Boolean
        Public Property Device As String
        Public Property Drive As DriveSpec
    End Class

    ' Python map: src/greaseweazle/tools/rpm.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Rpm

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/rpm.py::speed_str
        Public Shared Function SpeedString(timePerRev As Double) As String
            Return String.Format(
                Globalization.CultureInfo.InvariantCulture,
                "Rate: {0:F3} rpm ; Period: {1:F3} ms",
                60.0 / timePerRev,
                timePerRev * 1000.0)
        End Function

        ' Python map: src/greaseweazle/tools/rpm.py::print_rpm
        Public Shared Function PrintRpm(timePerRev As IReadOnlyList(Of Double)) As List(Of String)
            Dim output As New List(Of String)()
            If timePerRev Is Nothing OrElse timePerRev.Count <= 1 Then
                Return output
            End If

            Dim minValue = timePerRev.Min()
            Dim maxValue = timePerRev.Max()
            Dim meanValue = timePerRev.Average()
            Dim sorted = timePerRev.OrderBy(Function(x) x).ToList()
            Dim median = sorted(sorted.Count \ 2)

            output.Add("FASTEST:  " & SpeedString(minValue))
            output.Add("Ar.Mean:  " & SpeedString(meanValue))
            output.Add("Median:   " & SpeedString(median))
            output.Add("SLOWEST:  " & SpeedString(maxValue))
            Return output
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As RpmRuntimePreview
            Dim nr = 1
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
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
                    Case "--nr"
                        nr = ParseUInt(TakeOptionValue(args, i, token, inlineValue), token)
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
                    Case "--device", "--drive"
                        Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                        If String.Equals(token, "--device", StringComparison.Ordinal) Then
                            device = optionValue
                        Else
                            driveToken = optionValue
                        End If
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
            Return New RpmRuntimePreview With {
                .Nr = nr,
                .Live = live,
                .Device = device,
                .Drive = ResolveDrive(driveToken)
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseUInt)
        Private Shared Function ParseUInt(value As String, optionName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid value for {0}: {1}", optionName, value))
            End If
            Return parsed
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolveDrive)
        Private Shared Function ResolveDrive(token As String) As DriveSpec
            Try
                Return ToolOptions.Drive(token)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
        End Function

    End Class

End Namespace
