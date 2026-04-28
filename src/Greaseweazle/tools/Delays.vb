Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `delays` action.
    Public Class DelaysOptions
        Public Property Values As Dictionary(Of String, Integer)
        Public Property Live As Boolean = True
        Public Property Device As String

        Public Shared Function FromArgs(args As IReadOnlyList(Of String)) As DelaysOptions
            Return Delays.BuildRuntimePreview(args)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/delays.py::Delays
    Public NotInheritable Class Delays

        ' Python map: src/greaseweazle/tools/delays.py::Delays.__init__
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/delays.py::print_info_line
        Public Shared Function PrintInfoLine(name As String, value As String, Optional tab As Integer = 0) As String
            Dim prefix = New String(" "c, Math.Max(tab, 0))
            Dim left = (name & ":").PadRight(Math.Max(14 - tab, 0))
            Return prefix & left & value
        End Function

        ' Python map: src/greaseweazle/tools/delays.py::Delays.update
        Public Shared Sub Update(usbClient As Unit, paramSize As Integer, values As UShort())
            Dim outDat(paramSize - 1) As Byte
            For i = 0 To (paramSize \ 2) - 1
                Dim packed = BitConverter.GetBytes(values(i))
                outDat(i * 2) = packed(0)
                outDat(i * 2 + 1) = packed(1)
            Next
            usbClient.SetParams(UsbProtocol.Params.Delays, outDat)
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As DelaysOptions
            Dim values As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            Dim live = True
            Dim device As String = Nothing
            Dim positionals As New List(Of String)()
            Dim optionsWithValues As String() = {
                "--select", "--step", "--settle", "--motor", "--watchdog",
                "--pre-write", "--post-write", "--index-mask", "--device"
            }

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
                If optionsWithValues.Contains(token, StringComparer.Ordinal) Then
                    Dim optionValue = TakeOptionValue(args, i, token, inlineValue)
                    If String.Equals(token, "--device", StringComparison.Ordinal) Then
                        device = optionValue
                    Else
                        values(token) = ParseUInt(optionValue, token)
                    End If
                ElseIf String.Equals(token, "--test", StringComparison.Ordinal) Then
                    If inlineValue IsNot Nothing Then
                        Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                    End If
                    live = False
                Else
                    If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                        Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                    End If
                    positionals.Add(rawToken)
                End If
                i += 1
            End While

            If positionals.Count > 0 Then
                Throw New FatalException(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            Return New DelaysOptions With {.Values = values, .Live = live, .Device = device}
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

    End Class

End Namespace
