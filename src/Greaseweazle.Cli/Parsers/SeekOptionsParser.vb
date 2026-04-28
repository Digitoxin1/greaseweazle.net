Imports Greaseweazle.Core
Imports Greaseweazle.Tools

Namespace Greaseweazle.Cli.Parsers

    ' Argv parser for `gw-vb seek`. Migrated from Seek.BuildRuntimePreview;
    ' preserves the special handling for negative-cylinder positionals
    ' (otherwise `-3` would be rejected as an unknown flag).
    Public NotInheritable Class SeekOptionsParser

        Private Sub New()
        End Sub

        Public Shared Function Parse(args As IReadOnlyList(Of String)) As SeekOptions
            Dim force = False
            Dim motorOn = False
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
                    Case "--force"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        force = True
                    Case "--motor-on"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        motorOn = True
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
                        If rawToken.StartsWith("-", StringComparison.Ordinal) AndAlso
                           Not IsSignedIntegerToken(rawToken) Then
                            Throw New FatalException(String.Format("unrecognized arguments: {0}", rawToken))
                        End If
                        positionals.Add(rawToken)
                End Select
                i += 1
            End While

            ErrorHandling.Check(positionals.Count >= 1, "seek requires cylinder argument")
            ErrorHandling.Check(positionals.Count = 1, "seek takes exactly one cylinder argument")
            Dim cyl = ParseUint(positionals(0), "cylinder")
            Dim promptNeeded = Greaseweazle.Tools.Seek.ShouldPromptForExtremeCylinder(cyl, force)
            Dim drive As DriveSpec
            Try
                drive = ParserHelpers.Drive(driveToken)
            Catch ex As ArgumentException
                Throw New FatalException(ex.Message)
            End Try

            Return New SeekOptions With {
                .Cyl = cyl,
                .Force = force,
                .MotorOn = motorOn,
                .PromptNeeded = promptNeeded,
                .Live = live,
                .Device = device,
                .Drive = drive
            }
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

        Private Shared Function ParseUint(value As String, fieldName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid {0}: {1}", fieldName, value))
            End If
            Return parsed
        End Function

        Private Shared Function IsSignedIntegerToken(value As String) As Boolean
            Dim parsed As Integer
            Return Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed)
        End Function

    End Class

End Namespace
