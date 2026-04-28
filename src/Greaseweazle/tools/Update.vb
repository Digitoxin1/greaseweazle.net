Imports Greaseweazle.Core
Imports Greaseweazle.Infrastructure
Imports System.IO.Compression
Imports System.Net
Imports System.Linq
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Greaseweazle.Tools

    ' Python map: no-1:1 with Python symbols; this DTO captures parsed update runtime state.
    Public Class UpdateRuntimePreview
        Public Property FileValue As String
        Public Property TagValue As String
        Public Property Force As Boolean
        Public Property Bootloader As Boolean
        Public Property Live As Boolean
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration BuildRuntimePreview)
        Public Shared Function BuildRuntimePreview(args As IReadOnlyList(Of String)) As UpdateRuntimePreview
            Dim fileValue As String = Nothing
            Dim tagValue As String = Nothing
            Dim force = False
            Dim bootloader = False
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
                    Case "--file"
                        fileValue = TakeOptionValue(args, i, token, inlineValue)
                    Case "--tag"
                        tagValue = TakeOptionValue(args, i, token, inlineValue)
                    Case "--device"
                        device = TakeOptionValue(args, i, token, inlineValue)
                    Case "--force"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        force = True
                    Case "--bootloader"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        bootloader = True
                    Case "--test"
                        If inlineValue IsNot Nothing Then
                            Throw New FatalException(String.Format("argument {0}: ignored explicit argument '{1}'", token, inlineValue))
                        End If
                        live = False
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
            ValidateTagFileExclusion(fileValue, tagValue)

            Return New UpdateRuntimePreview With {
                .FileValue = fileValue,
                .TagValue = tagValue,
                .Force = force,
                .Bootloader = bootloader,
                .Live = live,
                .Device = device
            }
        End Function

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB function declaration ResolvePayload)
        Public Shared Function ResolvePayload(preview As UpdateRuntimePreview,
                                              Optional progress As IO.TextWriter = Nothing) As UpdatePayload
            If Not String.IsNullOrEmpty(preview.FileValue) Then
                Return New UpdatePayload With {
                    .Name = preview.FileValue,
                    .Data = IO.File.ReadAllBytes(preview.FileValue)
                }
            End If

            If Not String.IsNullOrEmpty(preview.TagValue) Then
                Return DownloadByTag(preview.TagValue, progress)
            End If

            Return DownloadLatest(progress)
        End Function

        ' Python map: src/greaseweazle/tools/update.py::download
        Public Shared Function BuildDownloadLine(name As String) As String
            Return "Downloading latest firmware: " & name
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
        Private Shared Function DownloadLatest(progress As IO.TextWriter) As UpdatePayload
            Dim release = FetchJson(Of GithubRelease)("https://api.github.com/repos/keirf/greaseweazle-firmware/releases/latest")
            Return Download(release, progress)
        End Function

        ' Python map: src/greaseweazle/tools/update.py::download_by_tag
        Private Shared Function DownloadByTag(tag As String, progress As IO.TextWriter) As UpdatePayload
            Dim releases = FetchJson(Of List(Of GithubRelease))("https://api.github.com/repos/keirf/greaseweazle-firmware/releases")
            Dim hit = releases.FirstOrDefault(Function(x) String.Equals(x.TagName, tag, StringComparison.Ordinal))
            ErrorHandling.Check(hit IsNot Nothing, String.Format("Unknown tag name '{0}'", tag))
            Return Download(hit, progress)
        End Function

        ' Python map: src/greaseweazle/tools/update.py::download
        Private Shared Function Download(release As GithubRelease, progress As IO.TextWriter) As UpdatePayload
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
            ' Python update.py:103 prints "Downloading latest firmware: NAME"
            ' BEFORE issuing the actual asset GET so the user sees what is in
            ' flight while the (potentially slow) network fetch is running.
            If progress IsNot Nothing Then
                progress.WriteLine(BuildDownloadLine(updName))
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

        ' Python map: src/greaseweazle/...::(no direct 1:1 symbol; VB sub declaration CheckOptionValue)
        Private Shared Sub CheckOptionValue(args As IReadOnlyList(Of String), index As Integer, optionName As String)
            Dim hasValue = index < args.Count
            If hasValue Then
                Dim value = args(index)
                If value.StartsWith("-", StringComparison.Ordinal) Then
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
