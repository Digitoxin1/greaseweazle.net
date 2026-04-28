Namespace Greaseweazle.Actions

    ' Per-cell verdict in the post-Convert/post-Read sector grid.
    Public Enum SectorSummaryCell
        ' Cylinder/head not visited, or this row's sector index is past
        ' Codec.Nsec for that track. Renders as a single space.
        Empty = 0

        ' Codec.HasSec returned True for this (cyl, head, sec) triple.
        ' Renders as ".".
        Good = 1

        ' Track was decoded but the specific sector at this row is
        ' missing or corrupt. Renders as "X".
        Bad = 2
    End Enum

    ' One row of the post-decode sector grid: a single (head, sector)
    ' pair across every cylinder in the TrackSet. Cells.Count equals
    ' the number of cylinders.
    Public NotInheritable Class SectorSummaryRow

        Public Sub New(head As Integer,
                       sector As Integer,
                       cells As IReadOnlyList(Of SectorSummaryCell))
            Me.Head = head
            Me.Sector = sector
            Me.Cells = cells
        End Sub

        Public ReadOnly Property Head As Integer
        Public ReadOnly Property Sector As Integer
        Public ReadOnly Property Cells As IReadOnlyList(Of SectorSummaryCell)

    End Class

    ' Structured summary of a Convert/Read run. The CLI renders this
    ' into Python's "Cyl-> / H. S: / 0. 0:" grid plus the trailing
    ' "Found N sectors of M (P%)" tally; library consumers can walk
    ' Rows/Cells to surface the same data in a UI without parsing text.
    Public NotInheritable Class SectorSummaryGrid

        Public Sub New(cyls As IReadOnlyList(Of Integer),
                       heads As IReadOnlyList(Of Integer),
                       rows As IReadOnlyList(Of SectorSummaryRow),
                       totalSectors As Integer,
                       goodSectors As Integer)
            Me.Cyls = cyls
            Me.Heads = heads
            Me.Rows = rows
            Me.TotalSectors = totalSectors
            Me.GoodSectors = goodSectors
        End Sub

        Public ReadOnly Property Cyls As IReadOnlyList(Of Integer)
        Public ReadOnly Property Heads As IReadOnlyList(Of Integer)
        Public ReadOnly Property Rows As IReadOnlyList(Of SectorSummaryRow)
        Public ReadOnly Property TotalSectors As Integer
        Public ReadOnly Property GoodSectors As Integer

        ' True if the grid contains any content worth rendering. Used
        ' by formatters to skip the entire table when no codec
        ' decoded any sectors (Python behaviour).
        Public ReadOnly Property HasContent As Boolean
            Get
                Return Rows IsNot Nothing AndAlso Rows.Count > 0
            End Get
        End Property

    End Class

End Namespace
