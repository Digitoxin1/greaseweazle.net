Imports Greaseweazle.Core

Namespace Greaseweazle.Codecs

    ' Shared bit/byte plumbing used by every fixed / GCR codec and by EDSK,
    ' DMK, IBMFixed, HpMmfm, etc.
    '
    ' Why this module exists
    ' ----------------------
    ' Every codec previously shipped a private copy of `BytesToBits`,
    ' `BitsToBytes`, and (where applicable) `FindPatternOffsets`. The
    ' implementations were near-identical bit-by-bit loops; the duplication
    ' wasted both source bytes and JIT cache, and it made it impossible to
    ' upgrade them in one place. This module:
    '
    '   - replaces the per-codec helpers with a single, table-friendly
    '     implementation,
    '   - unrolls the inner per-byte loop (no shifts in a 0..7 loop, no
    '     `If(..., 1, 0)`), and
    '   - exposes a 'padded' variant for callers that need the IBM-style
    '     pad-to-multiple-of-8 behaviour (only IBMFixed.DecMmfmEncode uses
    '     this).
    '
    ' Bit ordering is MSB-first to match every existing codec and match
    ' Python's `struct.unpack('>...', bits.tobytes())` semantics in
    ' src/greaseweazle/codec/ibm/ibm.py and friends.
    Public NotInheritable Class BitHelpers

        Private Sub New()
        End Sub

        ' MSB-first byte -> 8 bool array.
        Public Shared Function BytesToBits(data As Byte()) As Boolean()
            Dim out(data.Length * 8 - 1) As Boolean
            Dim k As Integer = 0
            For Each b As Byte In data
                out(k) = (b And &H80) <> 0 : k += 1
                out(k) = (b And &H40) <> 0 : k += 1
                out(k) = (b And &H20) <> 0 : k += 1
                out(k) = (b And &H10) <> 0 : k += 1
                out(k) = (b And &H8) <> 0 : k += 1
                out(k) = (b And &H4) <> 0 : k += 1
                out(k) = (b And &H2) <> 0 : k += 1
                out(k) = (b And &H1) <> 0 : k += 1
            Next
            Return out
        End Function

        ' Single-byte MSB-first overload. Several codecs / images need this in
        ' inline pattern construction (see TrackModel weak-pattern builder
        ' and Apple2 GCR sector header expansion).
        Public Shared Function BytesToBits(b As Byte) As Boolean()
            Return New Boolean() {
                (b And &H80) <> 0,
                (b And &H40) <> 0,
                (b And &H20) <> 0,
                (b And &H10) <> 0,
                (b And &H8) <> 0,
                (b And &H4) <> 0,
                (b And &H2) <> 0,
                (b And &H1) <> 0
            }
        End Function

        ' MSB-first bit list -> bytes. Assumes bits.Count is a multiple of 8.
        ' Trailing bits past the last full byte boundary are silently dropped,
        ' which mirrors all the per-codec copies this replaces.
        Public Shared Function BitsToBytes(bits As IList(Of Boolean)) As Byte()
            Dim n = bits.Count \ 8
            If n = 0 Then Return New Byte() {}
            Dim out(n - 1) As Byte
            Dim p As Integer = 0
            For i = 0 To n - 1
                Dim v As Integer = 0
                If bits(p) Then v = v Or &H80
                If bits(p + 1) Then v = v Or &H40
                If bits(p + 2) Then v = v Or &H20
                If bits(p + 3) Then v = v Or &H10
                If bits(p + 4) Then v = v Or &H8
                If bits(p + 5) Then v = v Or &H4
                If bits(p + 6) Then v = v Or &H2
                If bits(p + 7) Then v = v Or &H1
                out(i) = CByte(v)
                p += 8
            Next
            Return out
        End Function

        ' Same as BitsToBytes but pads the last partial byte with zeros on the
        ' low (right) side. Mirrors the IBMFixed.DecMmfmEncode caller, which
        ' may produce a residual fragment after pattern replacement.
        Public Shared Function BitsToBytesPadded(bits As IList(Of Boolean)) As Byte()
            Dim count = bits.Count
            If count = 0 Then Return New Byte() {}
            Dim n = (count + 7) \ 8
            Dim out(n - 1) As Byte
            Dim p As Integer = 0
            For i = 0 To n - 1
                Dim v As Integer = 0
                Dim mask As Integer = &H80
                For j = 0 To 7
                    If p < count AndAlso bits(p) Then v = v Or mask
                    p += 1
                    mask = mask >> 1
                Next
                out(i) = CByte(v)
            Next
            Return out
        End Function

        ' Bit-level pattern search over an MSB-first bit list. Replaces the
        ' near-identical Iterator implementations that lived in every codec
        ' (each one walking the bits one position at a time and exiting at
        ' the first mismatch).
        '
        ' For pattern.Length <= 64, this packs the haystack into 64-bit words
        ' (once per call) and then slides a 64-bit window with a single XOR-
        ' and-mask check per candidate offset. That collapses the 'compare M
        ' bits at every offset' inner loop into 3 word ops, which is roughly
        ' a 10x speedup for the typical 16- to 50-bit sync patterns scanned
        ' across ~100K bits per track. Patterns longer than 64 bits fall back
        ' to a tight bit-by-bit scan because they can't fit in a single ULong
        ' window and the longer-pattern callers (DecMmfm) are cold.
        '
        ' Bit packing: bit `i` of the haystack lives in word `i \ 64` at bit
        ' position `63 - (i mod 64)`. The window starting at bit `i` is
        ' aligned in the high `M` bits of the resulting ULong, where the
        ' pattern is also pre-packed; the low `64 - M` bits are masked off
        ' before comparison.
        Public Shared Iterator Function FindPatternOffsets(bits As IList(Of Boolean),
                                                           pattern As Boolean()) As IEnumerable(Of Integer)
            Dim n As Integer = bits.Count
            Dim m As Integer = pattern.Length
            If m = 0 OrElse n < m Then Return

            If m > 64 Then
                ' Cold path: long patterns (>64 bits) fall back to bit-level
                ' scan with first-bit early-out. Used by DecMmfm only.
                For i = 0 To n - m
                    Dim ok As Boolean = True
                    For j = 0 To m - 1
                        If bits(i + j) <> pattern(j) Then
                            ok = False
                            Exit For
                        End If
                    Next
                    If ok Then Yield i
                Next
                Return
            End If

            ' Pack haystack into ULong words.
            Dim wordCount As Integer = (n + 63) \ 64
            Dim packed(wordCount - 1) As ULong
            Dim w As ULong = 0UL
            Dim bitPos As Integer = 63
            Dim wIdx As Integer = 0
            For i = 0 To n - 1
                If bits(i) Then w = w Or (1UL << bitPos)
                bitPos -= 1
                If bitPos < 0 Then
                    packed(wIdx) = w
                    wIdx += 1
                    w = 0UL
                    bitPos = 63
                End If
            Next
            If bitPos < 63 Then
                packed(wIdx) = w
            End If

            ' Pack pattern into a single ULong (high m bits) plus mask.
            Dim patBits As ULong = 0UL
            For j = 0 To m - 1
                If pattern(j) Then patBits = patBits Or (1UL << (63 - j))
            Next
            Dim mask As ULong
            If m = 64 Then
                mask = ULong.MaxValue
            Else
                mask = ((1UL << m) - 1UL) << (64 - m)
            End If

            ' Slide window across all candidate offsets.
            Dim last As Integer = n - m
            For i = 0 To last
                Dim wi As Integer = i >> 6
                Dim sh As Integer = i And &H3F
                Dim window As ULong
                If sh = 0 Then
                    window = packed(wi)
                Else
                    Dim hi As ULong = packed(wi) << sh
                    Dim lo As ULong = 0UL
                    If wi + 1 < wordCount Then
                        lo = packed(wi + 1) >> (64 - sh)
                    End If
                    window = hi Or lo
                End If
                If (window And mask) = patBits Then Yield i
            Next
        End Function

        ' Convenience wrapper for the two codecs that only need the first
        ' match (DataGeneral, NorthStar). Returns -1 if no match.
        Public Shared Function FindPatternOffset(bits As IList(Of Boolean),
                                                  pattern As Boolean()) As Integer
            For Each off In FindPatternOffsets(bits, pattern)
                Return off
            Next
            Return -1
        End Function

        ' Parses a "01"-style ASCII string into an MSB-first Boolean array.
        ' '1' becomes True, anything else becomes False (callers always pass
        ' a string of just '0' / '1'). Replaces three identical local copies
        ' (IBMFixedCodec, IBMScanCodec, C64GcrCodec) used to construct the
        ' DEC MMFM, FM-sync, and similar long sync patterns at module-load
        ' time.
        Public Shared Function BitsFrom01(spec As String) As Boolean()
            Dim bits(spec.Length - 1) As Boolean
            For i = 0 To spec.Length - 1
                bits(i) = (spec(i) = "1"c)
            Next
            Return bits
        End Function

    End Class

End Namespace
