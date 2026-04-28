Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `pin` action.
    Public Class PinOptions
        Public Property Mode As String
        Public Property Live As Boolean = True
        Public Property Device As String
        Public Property Drive As DriveSpec
        Public Property Pin As Integer
        Public Property Level As Boolean

        Public Shared Function FromArgs(args As IReadOnlyList(Of String)) As PinOptions
            Return Greaseweazle.Tools.Pin.BuildRuntimePreview(args)
        End Function
    End Class

    ' Python map: src/greaseweazle/tools/pin.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Pin

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/tools/pin.py::_pin_get
        Public Shared Function PinGetInner(usbClient As Unit, pin As Integer) As Boolean
            Return usbClient.GetPin(pin)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration DispatchPinSubcommand)
        Public Shared Function DispatchPinSubcommand(argv As IReadOnlyList(Of String)) As String
            If argv.Count < 3 Then
                Return "usage"
            End If
            If String.Equals(argv(2), "get", StringComparison.Ordinal) Then
                Return "get"
            End If
            If String.Equals(argv(2), "set", StringComparison.Ordinal) Then
                Return "set"
            End If
            Return "usage"
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As PinOptions
            If args.Count = 0 Then
                Return New PinOptions With {.Mode = "usage"}
            End If

            Dim subcommand = args(0)
            Dim live = True
            Dim device As String = Nothing
            Dim driveToken = "A"
            Dim positionals As New List(Of String)()
            Dim i = 1
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

            If String.Equals(subcommand, "set", StringComparison.Ordinal) Then
                ErrorHandling.Check(positionals.Count = 2, "pin set requires <pin> <level>")
                Dim pin = ParseUInt(positionals(0), "pin")
                Dim level As Boolean
                Try
                    level = ToolOptions.Level(positionals(1))
                Catch ex As ArgumentException
                    Throw New FatalException(ex.Message)
                End Try
                Return New PinOptions With {
                    .Mode = "set",
                    .Live = live,
                    .Device = device,
                    .Drive = ResolveDrive(driveToken),
                    .Pin = pin,
                    .Level = level
                }
            End If
            If String.Equals(subcommand, "get", StringComparison.Ordinal) Then
                ErrorHandling.Check(positionals.Count = 1, "pin get requires <pin>")
                Dim pin = ParseUInt(positionals(0), "pin")
                Return New PinOptions With {
                    .Mode = "get",
                    .Live = live,
                    .Device = device,
                    .Drive = ResolveDrive(driveToken),
                    .Pin = pin
                }
            End If
            Return New PinOptions With {.Mode = "usage"}
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ParseUInt)
        Private Shared Function ParseUInt(value As String, fieldName As String) As Integer
            Dim parsed As Integer
            If Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, parsed) OrElse parsed < 0 Then
                Throw New FatalException(String.Format("invalid {0}: {1}", fieldName, value))
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
