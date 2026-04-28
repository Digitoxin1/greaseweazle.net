Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb rpm`. Migrated from Rpm.BuildRuntimePreview.
    Public NotInheritable Class RpmOptionsParser

        Private Sub New()
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As RpmOptions
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
            Return New RpmOptions With {
                .Nr = nr,
                .Live = live,
                .Device = device,
                .Drive = ResolveDrive(driveToken)
            }
        End Function

        Private Shared Function ParseUInt(value As String, optionName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid value for {0}: {1}", optionName, value))
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

        Private Shared Function ResolveDrive(token As String) As DriveSpec
            Try
                Return ParserHelpers.Drive(token)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try
        End Function

    End Class

End Namespace
