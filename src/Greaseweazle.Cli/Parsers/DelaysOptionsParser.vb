Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb delays`. Migrated from
    ' Delays.BuildRuntimePreview; preserves the optionsWithValues table
    ' that lets the CLI accept any of --select/--step/--settle/--motor/
    ' --watchdog/--pre-write/--post-write/--index-mask/--device.
    Public NotInheritable Class DelaysOptionsParser

        Private Const ActionName As String = "delays"

        Private Sub New()
        End Sub

        Private Shared Sub Argparse(message As String)
            ParserHelpers.Argparse(ActionName, message)
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As DelaysOptions
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
                        ' Library accepts domain keys (without leading "--") so
                        ' DelaysOptions.Values stays free of CLI argv flag names.
                        Dim domainKey = token.Substring(2)
                        values(domainKey) = ParseUInt(optionValue, token)
                    End If
                ElseIf String.Equals(token, "--test", StringComparison.Ordinal) Then
                    If inlineValue IsNot Nothing Then
                        Argparse(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                    End If
                    live = False
                Else
                    If rawToken.StartsWith("-", StringComparison.Ordinal) Then
                        Argparse(String.Format("unrecognized arguments: {0}", rawToken))
                    End If
                    positionals.Add(rawToken)
                End If
                i += 1
            End While

            If positionals.Count > 0 Then
                Argparse(String.Format("unrecognized arguments: {0}", String.Join(" ", positionals)))
            End If
            Return New DelaysOptions With {.Values = values, .Live = live, .Device = device}
        End Function

        Private Shared Function ParseUInt(value As String, optionName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Argparse(String.Format("invalid value for {0}: {1}", optionName, value))
            End If
            Return parsed
        End Function

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
