Imports System.Text.RegularExpressions
Imports Greaseweazle.Core
Imports Greaseweazle.Shared

Namespace Greaseweazle.Codecs

    ' Python map: src/greaseweazle/codec/codec.py::_get_diskdef, get_diskdef, print_formats
    Public NotInheritable Class DiskDefParser

        ' Python map: src/greaseweazle/codec/codec.py::ParseMode
        Private Enum ParseMode
            Outer
            Disk
            Track
        End Enum

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB non-instantiable parser utility class constructor)
        Private Sub New()
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::get_diskdef
        Public Shared Function GetDiskdef(formatName As String,
                                          Optional diskDefPath As String = Nothing) As DiskDef
            Dim source = New DiskDefFile(diskDefPath, Nothing)
            Dim disk = GetDiskdefInner(formatName, source.Lines, "", source)
            If disk Is Nothing Then
                Return Nothing
            End If
            disk.Finalise()
            ' Capture the resolved format name so callers that only receive the
            ' DiskDef can still reach back to the canonical format string.
            disk.Name = formatName
            Return disk
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::_get_diskdef
        Private Shared Function GetDiskdefInner(formatName As String,
                                                lines As IEnumerable(Of String),
                                                Optional prefix As String = "",
                                                Optional source As DiskDefFile = Nothing) As DiskDef
            Dim mode = ParseMode.Outer
            Dim active = False
            Dim disk As DiskDef = Nothing
            Dim track As TrackDef = Nothing
            Dim lineNumber = 0

            For Each rawLine In lines
                lineNumber += 1
                Dim trimmed = StripComment(rawLine)
                If trimmed.Length = 0 Then
                    Continue For
                End If

                Try
                    Select Case mode
                        Case ParseMode.Outer
                            Dim diskMatch = Regex.Match(trimmed, "^disk\s+([\w,.-]+)$", RegexOptions.IgnoreCase)
                            If diskMatch.Success Then
                                mode = ParseMode.Disk
                                active = String.Equals((prefix & diskMatch.Groups(1).Value).ToLowerInvariant(),
                                                       formatName.ToLowerInvariant(),
                                                       StringComparison.Ordinal)
                                If active Then
                                    disk = New DiskDef()
                                End If
                                Continue For
                            End If

                            ' Python: error.check(import_match is not None, 'syntax error') --
                            ' once we know the line is not `disk ...` it must be a valid `import` clause.
                            Dim importMatch = Regex.Match(trimmed, "^import\s+([\w,.-]*)\s*""([^""]+)""$", RegexOptions.IgnoreCase)
                            ErrorHandling.Check(importMatch.Success, "syntax error")
                            If source IsNot Nothing Then
                                Dim subPrefix = prefix & importMatch.Groups(1).Value.ToLowerInvariant()
                                If formatName.ToLowerInvariant().StartsWith(subPrefix, StringComparison.Ordinal) Then
                                    Dim child = New DiskDefFile(importMatch.Groups(2).Value, source)
                                    disk = GetDiskdefInner(formatName, child.Lines, subPrefix, child)
                                    If disk IsNot Nothing Then
                                        Exit For
                                    End If
                                End If
                            End If
                            Continue For

                        Case ParseMode.Disk
                            If String.Equals(trimmed, "end", StringComparison.OrdinalIgnoreCase) Then
                                mode = ParseMode.Outer
                                active = False
                                If disk IsNot Nothing Then
                                    Exit For
                                End If
                                Continue For
                            End If

                            Dim tracksMatch = Regex.Match(trimmed, "^tracks\s+([0-9,.*-]+)\s+([\w,.-]+)$", RegexOptions.IgnoreCase)
                            If tracksMatch.Success Then
                                mode = ParseMode.Track
                                If Not active Then
                                    Continue For
                                End If

                                ErrorHandling.Check(disk.Cyls.HasValue, "missing cyls")
                                ErrorHandling.Check(disk.Heads.HasValue, "missing heads")
                                track = CodecRegistry.MkTrackdef(tracksMatch.Groups(2).Value)
                                For Each entry In ExpandTrackSpec(tracksMatch.Groups(1).Value, disk.Cyls.Value, disk.Heads.Value)
                                    disk.TrackMap(Tuple.Create(entry.Item1, entry.Item2)) = track
                                Next
                                Continue For
                            End If

                            If active Then
                                Dim kv = ParseKeyValue(trimmed)
                                disk.AddParam(kv.Item1, kv.Item2)
                            End If

                        Case ParseMode.Track
                            If String.Equals(trimmed, "end", StringComparison.OrdinalIgnoreCase) Then
                                mode = ParseMode.Disk
                                If active AndAlso track IsNot Nothing Then
                                    track.Finalise()
                                    track = Nothing
                                End If
                                Continue For
                            End If
                            If active Then
                                Dim kv = ParseKeyValue(trimmed)
                                track.AddParam(kv.Item1, kv.Item2)
                            End If
                    End Select
                Catch ex As Exception
                    Dim fileName = If(source IsNot Nothing, source.Name, "diskdefs")
                    Throw New FatalException(String.Format("At {0}, line {1}: {2}", fileName, lineNumber, ex.Message))
                End Try
            Next

            Return disk
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::print_formats
        Public Shared Function PrintFormats(formats As IEnumerable(Of String)) As String
            Return ColumnFormatter.Columnify(formats.OrderBy(Function(x) x))
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::print_formats
        Public Shared Function GetAllFormats(Optional diskDefPath As String = Nothing) As List(Of String)
            Dim source = New DiskDefFile(diskDefPath, Nothing)
            Dim formats = CollectAllFormats("", source)
            formats.Sort(StringComparer.Ordinal)
            Return formats
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::get_all_formats
        Private Shared Function CollectAllFormats(prefix As String, source As DiskDefFile) As List(Of String)
            Dim formats As New List(Of String)()
            For Each line In source.Lines
                Dim diskMatch = Regex.Match(line, "^\s*disk\s+([\w,.-]+)", RegexOptions.IgnoreCase)
                If diskMatch.Success Then
                    formats.Add(prefix & diskMatch.Groups(1).Value)
                End If
                Dim importMatch = Regex.Match(line, "^\s*import\s+([\w,.-]*)\s*""([^""]+)""", RegexOptions.IgnoreCase)
                If importMatch.Success Then
                    Dim child = New DiskDefFile(importMatch.Groups(2).Value, source)
                    formats.AddRange(CollectAllFormats(prefix & importMatch.Groups(1).Value, child))
                End If
            Next
            Return formats
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB helper to mirror comment stripping used inline in _get_diskdef)
        Private Shared Function StripComment(line As String) As String
            Dim noComment = line
            Dim hash = noComment.IndexOf("#"c)
            If hash >= 0 Then
                noComment = noComment.Substring(0, hash)
            End If
            Return noComment.Trim()
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB helper centralising key-value parse regex used in _get_diskdef)
        Private Shared Function ParseKeyValue(value As String) As Tuple(Of String, String)
            Dim m = Regex.Match(value, "^([a-zA-Z0-9:,._-]+)\s*=\s*([a-zA-Z0-9:,._*-]+)$")
            ErrorHandling.Check(m.Success, "syntax error")
            Return Tuple.Create(m.Groups(1).Value, m.Groups(2).Value)
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB helper extracted from inline track-spec expansion logic in _get_diskdef)
        Private Shared Function ExpandTrackSpec(spec As String,
                                                cyls As Integer,
                                                heads As Integer) As IEnumerable(Of Tuple(Of Integer, Integer))
            Dim output As New List(Of Tuple(Of Integer, Integer))()
            For Each entry In spec.Split(","c)
                If entry = "*" Then
                    For c = 0 To cyls - 1
                        For h = 0 To heads - 1
                            output.Add(Tuple.Create(c, h))
                        Next
                    Next
                    Continue For
                End If

                Dim m = Regex.Match(entry, "^(\d+)(?:-(\d+))?(?:\.([01]))?$")
                ErrorHandling.Check(m.Success, "bad track specifier")
                Dim s = Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                Dim e = If(String.IsNullOrEmpty(m.Groups(2).Value), s, Integer.Parse(m.Groups(2).Value, Globalization.CultureInfo.InvariantCulture))
                Dim hValue = m.Groups(3).Value
                Dim headList As IEnumerable(Of Integer)
                If String.IsNullOrEmpty(hValue) Then
                    headList = Enumerable.Range(0, heads)
                Else
                    Dim parsedHead = Integer.Parse(hValue, Globalization.CultureInfo.InvariantCulture)
                    ErrorHandling.Check(parsedHead < heads, "head out of range")
                    headList = {parsedHead}
                End If

                ErrorHandling.Check(s >= 0 AndAlso e >= 0 AndAlso s <= e AndAlso s < cyls AndAlso e < cyls, "cylinder out of range")
                For c = s To e
                    For Each h In headList
                        output.Add(Tuple.Create(c, h))
                    Next
                Next
            Next
            Return output
        End Function

    End Class

    ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File
    Public Class DiskDefFile

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File.__init__
        Public Sub New(nameOrPath As String, parent As DiskDefFile)
            If parent Is Nothing Then
                Dim requested = If(String.IsNullOrWhiteSpace(nameOrPath), "diskdefs.xml", nameOrPath.Trim())
                If IsFilePath(requested) AndAlso IO.File.Exists(requested) Then
                    IsEmbedded = False
                    Path = If(IO.Path.IsPathRooted(requested), requested, IO.Path.GetFullPath(requested))
                    Name = IO.Path.GetFileName(Path)
                Else
                    IsEmbedded = True
                    Name = IO.Path.GetFileName(requested)
                    Path = Name
                End If
            ElseIf parent.IsEmbedded Then
                IsEmbedded = True
                Name = IO.Path.GetFileName(nameOrPath)
                Path = Name
            Else
                IsEmbedded = False
                Path = IO.Path.Combine(IO.Path.GetDirectoryName(parent.Path), nameOrPath)
                Name = nameOrPath
            End If

            If Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) Then
                Lines = LoadXmlLines()
            Else
                Lines = LoadCfgLines()
            End If
        End Sub

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File.path
        Public Property Path As String
        ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File.name
        Public Property Name As String
        ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File.lines
        Public Property Lines As List(Of String)
        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB source flag for embedded XML diskdefs resources)
        Public Property IsEmbedded As Boolean

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB helper for detecting path-like values)
        Private Shared Function IsFilePath(value As String) As Boolean
            Return value.IndexOf(IO.Path.DirectorySeparatorChar) >= 0 OrElse
                   value.IndexOf(IO.Path.AltDirectorySeparatorChar) >= 0 OrElse
                   value.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) OrElse
                   value.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File.lines
        Private Function LoadCfgLines() As List(Of String)
            If IsEmbedded Then
                Using stream = OpenEmbeddedStream(Path)
                    If stream Is Nothing Then
                        Throw New IO.FileNotFoundException(String.Format("Embedded diskdefs resource '{0}' not found", Path))
                    End If
                    Using reader As New IO.StreamReader(stream)
                        Dim output As New List(Of String)()
                        While Not reader.EndOfStream
                            output.Add(reader.ReadLine())
                        End While
                        Return output
                    End Using
                End Using
            End If
            Return IO.File.ReadAllLines(Path).ToList()
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::DiskDef_File.lines
        Private Function LoadXmlLines() As List(Of String)
            Dim doc As XDocument
            If IsEmbedded Then
                Using stream = OpenEmbeddedStream(Path)
                    If stream Is Nothing Then
                        Throw New IO.FileNotFoundException(String.Format("Embedded diskdefs resource '{0}' not found", Path))
                    End If
                    doc = XDocument.Load(stream)
                End Using
            Else
                doc = XDocument.Load(Path)
            End If

            Dim lines As New List(Of String)()
            Dim root = doc.Root
            If root Is Nothing Then
                Return lines
            End If

            For Each node In root.Elements()
                Select Case node.Name.LocalName
                    Case "import"
                        Dim importPrefix = node.Attribute("prefix")?.Value
                        Dim importFile = node.Attribute("file")?.Value
                        ErrorHandling.Check(Not String.IsNullOrEmpty(importFile), "import missing file")
                        lines.Add(String.Format("import {0} ""{1}""", If(importPrefix, String.Empty), importFile))
                    Case "disk"
                        Dim diskName = node.Attribute("name")?.Value
                        ErrorHandling.Check(Not String.IsNullOrEmpty(diskName), "disk missing name")
                        lines.Add(String.Format("disk {0}", diskName))
                        For Each child In node.Elements()
                            Select Case child.Name.LocalName
                                Case "option"
                                    Dim key = child.Attribute("key")?.Value
                                    Dim value = child.Attribute("value")?.Value
                                    ErrorHandling.Check(Not String.IsNullOrEmpty(key), "option missing key")
                                    ErrorHandling.Check(value IsNot Nothing, "option missing value")
                                    lines.Add(String.Format("{0} = {1}", key, value))
                                Case "tracks"
                                    Dim spec = child.Attribute("spec")?.Value
                                    Dim formatName = child.Attribute("format")?.Value
                                    ErrorHandling.Check(Not String.IsNullOrEmpty(spec), "tracks missing spec")
                                    ErrorHandling.Check(Not String.IsNullOrEmpty(formatName), "tracks missing format")
                                    lines.Add(String.Format("tracks {0} {1}", spec, formatName))
                                    For Each opt In child.Elements("option")
                                        Dim key = opt.Attribute("key")?.Value
                                        Dim value = opt.Attribute("value")?.Value
                                        ErrorHandling.Check(Not String.IsNullOrEmpty(key), "tracks option missing key")
                                        ErrorHandling.Check(value IsNot Nothing, "tracks option missing value")
                                        lines.Add(String.Format("{0} = {1}", key, value))
                                    Next
                                    lines.Add("end")
                            End Select
                        Next
                        lines.Add("end")
                End Select
            Next
            Return lines
        End Function

        ' Python map: src/greaseweazle/codec/codec.py::(no direct 1:1 symbol; VB helper for opening embedded diskdefs XML streams)
        Private Shared Function OpenEmbeddedStream(fileName As String) As IO.Stream
            Dim asm As Reflection.Assembly = Reflection.Assembly.GetExecutingAssembly()
            Dim candidates = asm.GetManifestResourceNames().
                Where(Function(x) x.EndsWith("." & fileName, StringComparison.OrdinalIgnoreCase) OrElse
                                 String.Equals(x, fileName, StringComparison.OrdinalIgnoreCase)).
                ToList()
            If candidates.Count = 0 Then
                candidates = asm.GetManifestResourceNames().
                    Where(Function(x) x.IndexOf(".data.", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                                     x.EndsWith("." & fileName, StringComparison.OrdinalIgnoreCase)).
                    ToList()
            End If
            If candidates.Count = 0 Then
                Return Nothing
            End If
            Return asm.GetManifestResourceStream(candidates(0))
        End Function
    End Class

End Namespace
