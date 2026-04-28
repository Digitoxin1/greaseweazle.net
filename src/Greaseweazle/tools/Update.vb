Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports System.IO.Compression
Imports System.Net
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Greaseweazle.Tools

    ' Strongly-typed options for the `update` action.
    Public Class UpdateOptions
        Public Property FileValue As String
        Public Property TagValue As String
        Public Property Force As Boolean
        Public Property Bootloader As Boolean
        Public Property Live As Boolean = True
        Public Property Device As String
    End Class

    ' Python map: no-1:1 with Python symbols; this DTO carries selected firmware payload bytes in managed flow.
    Public Class UpdatePayload
        Public Property Name As String
        Public Property Data As Byte()
    End Class

    ' Python map: no-1:1 with Python symbols; this DTO carries validated extracted firmware metadata + bytes.
    Public Class ExtractedUpdate
        Public Property Major As Integer
        Public Property Minor As Integer
        Public Property Payload As Byte()
    End Class

    ' Python map: src/greaseweazle/tools/update.py::SkipUpdate
    Public Class SkipUpdate
        Inherits Exception
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

    ' Python map: src/greaseweazle/tools/update.py (direct command-algorithm parity mapping).
    Public NotInheritable Class Update

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration New)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration ValidateTagFileExclusion)
        Public Shared Sub ValidateTagFileExclusion(fileValue As String, tagValue As String)
            If Not String.IsNullOrEmpty(fileValue) AndAlso Not String.IsNullOrEmpty(tagValue) Then
                Throw New FatalException("File and tag both specified. Only one is allowed.")
            End If
        End Sub

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolvePayload)
        '
        ' Loads the requested payload from disk or downloads it from
        ' GitHub. `onDownloadStarting`, when supplied, is invoked with
        ' the resolved asset filename right before the asset GET so the
        ' caller can render Python's "Downloading latest firmware: NAME"
        ' line. The local-file path doesn't fire the callback (Python
        ' only emits that line for downloads).
        Public Shared Function ResolvePayload(preview As UpdateOptions,
                                              Optional onDownloadStarting As Action(Of String) = Nothing) As UpdatePayload
            If Not String.IsNullOrEmpty(preview.FileValue) Then
                Return New UpdatePayload With {
                    .Name = preview.FileValue,
                    .Data = IO.File.ReadAllBytes(preview.FileValue)
                }
            End If

            If Not String.IsNullOrEmpty(preview.TagValue) Then
                Return DownloadByTag(preview.TagValue, onDownloadStarting)
            End If

            Return DownloadLatest(onDownloadStarting)
        End Function

        ' Python map: src/greaseweazle/tools/update.py::extract_update
        Public Shared Function ExtractUpdate(info As FirmwareInfo,
                                             payload As UpdatePayload,
                                             bootloader As Boolean) As ExtractedUpdate
            Dim dat = payload.Data
            ErrorHandling.Check(dat IsNot Nothing AndAlso dat.Length >= 8, String.Format("{0}: Not a valid UPD file", payload.Name))
            ErrorHandling.Check(Encoding.ASCII.GetString(dat, 0, 4) = "GWUP", String.Format("{0}: Not a valid UPD file", payload.Name))
            ErrorHandling.Check(ComputeCrc32Mpeg(dat) = 0UI, String.Format("{0}: UPD file is corrupt", payload.Name))

            Dim reqType = If(bootloader, "BL", "GW")
            Dim body = dat.Skip(4).Take(dat.Length - 8).ToArray()

            Dim found As Byte() = Nothing
            Dim major = 0
            Dim minor = 0
            Dim offset = 0
            While offset + 4 <= body.Length
                Dim updLen = CInt(BitConverter.ToUInt16(body, offset))
                Dim hwModel = CInt(BitConverter.ToUInt16(body, offset + 2))
                ErrorHandling.Check(updLen > 0, String.Format("{0}: Bad update file", payload.Name))
                If offset + 4 + updLen > body.Length Then
                    Exit While
                End If

                Dim entry = body.Skip(offset + 4).Take(updLen).ToArray()
                If entry.Length >= 8 Then
                    ' Python extract_update() matches on the 4-byte tuple at
                    ' dat[upd_len-4:upd_len], which maps to entry[len-8:len-4].
                    Dim updType = Encoding.ASCII.GetString(entry, entry.Length - 8, 2)
                    Dim entryMajor = CInt(entry(entry.Length - 6))
                    Dim entryMinor = CInt(entry(entry.Length - 5))
                    If hwModel = info.HwModel AndAlso String.Equals(updType, reqType, StringComparison.Ordinal) Then
                        found = entry
                        major = entryMajor
                        minor = entryMinor
                        Exit While
                    End If
                End If

                offset += updLen + 4
            End While

            ErrorHandling.Check(found IsNot Nothing,
                                String.Format("{0}: F{1} {2} update not found",
                                              payload.Name,
                                              info.HwModel,
                                              If(bootloader, "bootloader", "firmware")))

            ErrorHandling.Check((found.Length And 3) = 0 AndAlso found.Length >= 8, String.Format("{0}: Bad update file", payload.Name))
            Dim sig = Encoding.ASCII.GetString(found, found.Length - 8, 2)
            Dim footerHwModel = CInt(BitConverter.ToUInt16(found, found.Length - 4))
            ErrorHandling.Check(String.Equals(sig, reqType, StringComparison.Ordinal) AndAlso footerHwModel = info.HwModel,
                                String.Format("{0}: Bad update file", payload.Name))
            ErrorHandling.Check(ComputeCrc16CcittFalse(found) = 0US, String.Format("{0}: Bad CRC", payload.Name))

            Return New ExtractedUpdate With {
                .Major = major,
                .Minor = minor,
                .Payload = found
            }
        End Function

        ' Python map: src/greaseweazle/tools/update.py::update_firmware
        Public Shared Function UpdateFirmware(usbClient As Unit, payload As Byte(), bootloader As Boolean) As Integer
            Return If(bootloader,
                      usbClient.UpdateBootloader(payload),
                      usbClient.UpdateMainFirmware(payload))
        End Function

        ' Python map: src/greaseweazle/tools/update.py::download_latest
        Private Shared Function DownloadLatest(onDownloadStarting As Action(Of String)) As UpdatePayload
            Dim release = FetchJson(Of GithubRelease)("https://api.github.com/repos/keirf/greaseweazle-firmware/releases/latest")
            Return Download(release, onDownloadStarting)
        End Function

        ' Python map: src/greaseweazle/tools/update.py::download_by_tag
        Private Shared Function DownloadByTag(tag As String, onDownloadStarting As Action(Of String)) As UpdatePayload
            Dim releases = FetchJson(Of List(Of GithubRelease))("https://api.github.com/repos/keirf/greaseweazle-firmware/releases")
            Dim hit = releases.FirstOrDefault(Function(x) String.Equals(x.TagName, tag, StringComparison.Ordinal))
            ErrorHandling.Check(hit IsNot Nothing, String.Format("Unknown tag name '{0}'", tag))
            Return Download(hit, onDownloadStarting)
        End Function

        ' Python map: src/greaseweazle/tools/update.py::download
        Private Shared Function Download(release As GithubRelease, onDownloadStarting As Action(Of String)) As UpdatePayload
            ErrorHandling.Check(release IsNot Nothing AndAlso release.Assets IsNot Nothing, "GitHub release metadata is missing assets")
            Dim chosenUrl As String = Nothing
            Dim baseName As String = Nothing
            For Each asset In release.Assets
                If asset Is Nothing OrElse String.IsNullOrEmpty(asset.BrowserDownloadUrl) Then
                    Continue For
                End If
                Dim m = Regex.Match(asset.BrowserDownloadUrl, ".*/(greaseweazle-firmware-.+)\.zip$", RegexOptions.IgnoreCase)
                If m.Success Then
                    chosenUrl = asset.BrowserDownloadUrl
                    baseName = m.Groups(1).Value
                    Exit For
                End If
            Next
            ErrorHandling.Check(Not String.IsNullOrEmpty(chosenUrl), "No firmware release asset found")

            Dim updName = baseName & ".upd"
            ' Python update.py:103 fires the "Downloading latest firmware: NAME"
            ' notification BEFORE issuing the actual asset GET so the user
            ' sees what is in flight while the (potentially slow) network
            ' fetch is running.
            If onDownloadStarting IsNot Nothing Then
                onDownloadStarting(updName)
            End If
            Dim zipBytes = GhRequestGet(chosenUrl, timeoutMs:=10000)
            Using ms As New IO.MemoryStream(zipBytes)
                Using za As New ZipArchive(ms, ZipArchiveMode.Read, leaveOpen:=False)
                    Dim expectedPath = baseName & "/" & updName
                    Dim entry = za.GetEntry(expectedPath)
                    If entry Is Nothing Then
                        entry = za.Entries.FirstOrDefault(Function(x) x.FullName.EndsWith("/" & updName, StringComparison.OrdinalIgnoreCase) OrElse
                                                                      String.Equals(x.Name, updName, StringComparison.OrdinalIgnoreCase))
                    End If
                    ErrorHandling.Check(entry IsNot Nothing, String.Format("Firmware package missing '{0}'", updName))
                    Using s = entry.Open()
                        Using outMs As New IO.MemoryStream()
                            s.CopyTo(outMs)
                            Return New UpdatePayload With {
                                .Name = updName,
                                .Data = outMs.ToArray()
                            }
                        End Using
                    End Using
                End Using
            End Using
        End Function

        ' Python map: src/greaseweazle/tools/update.py::gh_request_get
        Private Shared Function GhRequestGet(url As String, timeoutMs As Integer) As Byte()
            Dim req = CType(WebRequest.Create(url), HttpWebRequest)
            req.Method = "GET"
            req.Timeout = timeoutMs
            req.ReadWriteTimeout = timeoutMs
            req.UserAgent = "gw-vb"
            req.AutomaticDecompression = DecompressionMethods.GZip Or DecompressionMethods.Deflate
            Using rsp = CType(req.GetResponse(), HttpWebResponse)
                Dim remaining = rsp.Headers("X-RateLimit-Remaining")
                If Not String.IsNullOrEmpty(remaining) AndAlso Integer.Parse(remaining, Globalization.CultureInfo.InvariantCulture) = 0 Then
                    Throw New WebException("GitHub API Rate Limit exceeded")
                End If
                Using s = rsp.GetResponseStream()
                    Using ms As New IO.MemoryStream()
                        s.CopyTo(ms)
                        Return ms.ToArray()
                    End Using
                End Using
            End Using
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration FetchJson)
        Private Shared Function FetchJson(Of T)(url As String) As T
            Dim bytes = GhRequestGet(url, timeoutMs:=5000)
            Using ms As New IO.MemoryStream(bytes)
                Dim ser As New DataContractJsonSerializer(GetType(T))
                Return CType(ser.ReadObject(ms), T)
            End Using
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeCrc16CcittFalse)
        Private Shared Function ComputeCrc16CcittFalse(data As Byte()) As UShort
            Dim crc As UInteger = &HFFFFUI
            For Each b In data
                crc = crc Xor (CUInt(b) << 8)
                For i = 0 To 7
                    If (crc And &H8000UI) <> 0UI Then
                        crc = ((crc << 1) Xor &H1021UI) And &HFFFFUI
                    Else
                        crc = (crc << 1) And &HFFFFUI
                    End If
                Next
            Next
            Return CUShort(crc And &HFFFFUI)
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ComputeCrc32Mpeg)
        Private Shared Function ComputeCrc32Mpeg(data As Byte()) As UInteger
            Dim crc As UInteger = &HFFFFFFFFUI
            For Each b In data
                crc = crc Xor (CUInt(b) << 24)
                For i = 0 To 7
                    If (crc And &H80000000UI) <> 0UI Then
                        crc = (crc << 1) Xor &H4C11DB7UI
                    Else
                        crc <<= 1
                    End If
                Next
            Next
            Return crc
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration GithubRelease)
        <DataContract>
        Private Class GithubRelease ' Python map: no-1:1 with Python symbols; this private DataContract models GitHub release JSON schema for managed deserialization.
            <DataMember(Name:="tag_name")>
            Public Property TagName As String
            <DataMember(Name:="assets")>
            Public Property Assets As List(Of GithubAsset)
        End Class

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB class declaration GithubAsset)
        <DataContract>
        Private Class GithubAsset ' Python map: no-1:1 with Python symbols; this private DataContract models GitHub asset JSON schema for managed deserialization.
            <DataMember(Name:="browser_download_url")>
            Public Property BrowserDownloadUrl As String
        End Class

    End Class

End Namespace
