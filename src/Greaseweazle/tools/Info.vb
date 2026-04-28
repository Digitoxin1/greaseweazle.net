Imports System.Text.RegularExpressions
Imports Greaseweazle.Core
Imports System.IO
Imports System.Net

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `info` action. Public surface for DLL
    ' consumers (the CLI front-end and any external tool); the algorithm in
    ' Info.BuildRuntimePreview parses argv and builds an instance, exposed via
    ' FromArgs().
    Public Class InfoOptions
        Public Property Bootloader As Boolean
        Public Property Live As Boolean = True
        Public Property Device As String
    End Class

    ' Python map: src/greaseweazle/tools/info.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Info

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration TryParseFirmwareTag)
        Public Shared Function TryParseFirmwareTag(tag As String, ByRef major As Integer, ByRef minor As Integer) As Boolean
            Dim m = Regex.Match(tag, "^v(\d+)\.(\d+)")
            If Not m.Success Then
                Return False
            End If
            major = Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
            minor = Integer.Parse(m.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
            Return True
        End Function

        ' Python map: src/greaseweazle/tools/info.py::print_info_line
        Public Shared Function PrintInfoLine(name As String, value As String, Optional tab As Integer = 0) As String
            Return "".PadLeft(tab) & (name & ":").PadRight(12 - tab) & value
        End Function

        ' Python map: src/greaseweazle/tools/info.py::model_id (lookup table for hw_model/hw_submodel pairs).
        Public Shared Function ModelName(hwModel As Integer, hwSubmodel As Integer) As String
            Dim known As New Dictionary(Of Tuple(Of Integer, Integer), String)() From {
                {Tuple.Create(1, 0), "F1"},
                {Tuple.Create(1, 1), "F1 Plus"},
                {Tuple.Create(1, 2), "F1 Plus (Unbuffered)"},
                {Tuple.Create(4, 0), "V4"},
                {Tuple.Create(4, 1), "V4 Slim"},
                {Tuple.Create(4, 2), "V4.1"},
                {Tuple.Create(7, 0), "F7 v1"},
                {Tuple.Create(7, 1), "F7 Plus (Ant Goffart, v1)"},
                {Tuple.Create(7, 2), "F7 Lightning"},
                {Tuple.Create(7, 3), "F7 v2)"},
                {Tuple.Create(7, 4), "F7 Plus (Ant Goffart, v2)"},
                {Tuple.Create(7, 5), "F7 Lightning Plus"},
                {Tuple.Create(7, 6), "F7 Slim"},
                {Tuple.Create(7, 7), "F7 v3 ""Thunderbolt"""},
                {Tuple.Create(8, 0), "Adafruit Floppy Generic"}
            }
            Dim key = Tuple.Create(hwModel, hwSubmodel)
            If known.ContainsKey(key) Then
                Dim base = known(key)
                If hwModel <> 8 Then
                    Return "Greaseweazle " & base
                End If
                Return base
            End If
            Return String.Format(Globalization.CultureInfo.InvariantCulture, "Unknown (0x{0:X2}{1:X2})", hwModel, hwSubmodel)
        End Function

        ' Python map: src/greaseweazle/tools/info.py::mcu_id
        Public Shared Function McuName(mcuId As Integer) As String
            Select Case mcuId
                Case 2 : Return "AT32F403"
                Case 7 : Return "AT32F403A"
                Case 5 : Return "AT32F415"
                Case 0 : Return String.Empty
                Case Else : Return String.Format(Globalization.CultureInfo.InvariantCulture, "Unknown (0x{0:X2})", mcuId)
            End Select
        End Function

        ' Python map: src/greaseweazle/tools/info.py::speed_id
        Public Shared Function UsbSpeedName(usbSpeed As Integer) As String
            Select Case usbSpeed
                Case 0 : Return "Full Speed (12 Mbit/s)"
                Case 1 : Return "High Speed (480 Mbit/s)"
                Case Else : Return String.Format(Globalization.CultureInfo.InvariantCulture, "Unknown (0x{0:X2})", usbSpeed)
            End Select
        End Function

        ' Python map: src/greaseweazle/tools/info.py::latest_firmware
        Public Shared Function LatestFirmware() As Tuple(Of Integer, Integer)
            Dim request = CType(WebRequest.Create("https://api.github.com/repos/keirf/greaseweazle-firmware/releases/latest"), HttpWebRequest)
            request.Timeout = 5000
            request.UserAgent = "gw-vb"

            Using response = CType(request.GetResponse(), HttpWebResponse)
                Dim remaining = response.Headers("X-RateLimit-Remaining")
                If Not String.IsNullOrEmpty(remaining) AndAlso Integer.Parse(remaining, Globalization.CultureInfo.InvariantCulture) = 0 Then
                    Throw New WebException("GitHub API Rate Limit exceeded")
                End If

                Using stream = response.GetResponseStream()
                    Using reader As New StreamReader(stream)
                        Dim json = reader.ReadToEnd()
                        Dim m = Regex.Match(json, """tag_name""\s*:\s*""([^""]+)""")
                        ErrorHandling.Check(m.Success, "Unable to parse firmware tag")
                        Dim major = 0
                        Dim minor = 0
                        ErrorHandling.Check(TryParseFirmwareTag(m.Groups(1).Value, major, minor), "Invalid firmware tag format")
                        Return Tuple.Create(major, minor)
                    End Using
                End Using
            End Using
        End Function

    End Class

End Namespace
