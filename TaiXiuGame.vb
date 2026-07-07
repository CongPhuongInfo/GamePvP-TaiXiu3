Option Strict On
Option Explicit On

Imports System.Collections.Generic

''' <summary>
''' Logic thuan tuy (khong dinh UI) cho game "Tai Xiu 3 Xuc Xac":
''' - Host tung 3 xuc xac (1..6 moi con), nguoi choi dat cuoc vao 1 hoac nhieu
'''   loai cuoc khac nhau CUNG LUC trong 1 van (khac Game777Game chi cho 1 cuoc/van).
''' - Cac loai cuoc: Tren/Duoi (x1), Bo Ba tung so 1..6 (x150), Tong diem 4..17
'''   (he so rieng theo tung tong, xem PAYOUT_BY_SUM).
''' - Luat chuan Tai Xiu: neu ra Bo Ba (3 con giong nhau) thi cuoc Tren/Duoi
'''   THUA het (nha cai an), chi cuoc Bo Ba dung so do va cuoc Tong diem (neu
'''   tong do trung, vd Bo Ba 4 -> tong 12) moi duoc tinh thuong binh thuong.
''' Class nay CHI duoc Host dung de tinh toan (RNG + tinh diem). Client chi ve
''' lai ket qua Host gui ve, khong tu tung xuc xac (giu dung nguyen tac cua
''' cac game truoc: Host-authoritative).
''' </summary>
Public Class TaiXiuGame

    Public Const DICE_COUNT As Integer = 3

    ''' <summary>Diem khoi dau cua moi nguoi choi khi vao phong.</summary>
    Public Const STARTING_SCORE As Long = 1000

    ''' <summary>Muc dat cuoc toi thieu / toi da cho MOI loai cuoc (1 nguoi co the
    ''' dat nhieu loai cuoc trong 1 van, moi loai rieng gioi han nay).</summary>
    Public Const MIN_BET As Long = 10
    Public Const MAX_BET As Long = 500

    ''' <summary>Cac loai cuoc co the dat.</summary>
    Public Enum BetKind
        Tren = 0        ' Tong 11..17 (tru truong hop Bo Ba - xem luat o tren), he so x1
        Duoi = 1        ' Tong 4..10 (tru truong hop Bo Ba), he so x1
        BoBa = 2        ' Ca 3 xuc xac cung ra 1 so chi dinh (1..6), he so x150
        TongDiem = 3    ' Tong dung 1 gia tri chi dinh tu 4..17, he so theo bang PAYOUT_BY_SUM
    End Enum

    ''' <summary>He so thuong theo tung gia tri Tong diem (4..17), giong bang cuoc
    ''' trong hinh mau: cang le xay ra (gan giua, vd 10-11) he so cang thap, cang
    ''' hiem (gan 2 dau 4 va 17) he so cang cao.</summary>
    Public Shared ReadOnly PAYOUT_BY_SUM As New Dictionary(Of Integer, Integer) From {
        {4, 50}, {5, 18}, {6, 14}, {7, 12}, {8, 8},
        {9, 7}, {10, 6}, {11, 6}, {12, 7}, {13, 8},
        {14, 12}, {15, 14}, {16, 18}, {17, 50}
    }

    Public Const TRENDUOI_MULTIPLIER As Integer = 1
    Public Const BOBA_MULTIPLIER As Integer = 150

    ''' <summary>1 luot dat cuoc cua 1 seat cho 1 loai cuoc cu the. Moi seat co the co
    ''' NHIEU BetInfo trong cung 1 van (vd vua cuoc Tren, vua cuoc Bo Ba so 6).</summary>
    Public Class BetInfo
        Public Seat As Integer
        Public Kind As BetKind
        Public Value As Integer      ' voi BoBa: so 1..6 ; voi TongDiem: tong 4..17 ; voi Tren/Duoi: khong dung (0)
        Public Amount As Long
    End Class

    ''' <summary>Ket qua thuong/thua cua 1 BetInfo sau khi co ket qua tung xuc xac.</summary>
    Public Class RoundOutcome
        Public Seat As Integer
        Public Kind As BetKind
        Public Value As Integer
        Public Amount As Long
        Public Won As Boolean
        Public Payout As Long        ' duong = duoc them (da gom lai von), am = mat cuoc
        Public NewScore As Long
    End Class

    ' ------- Trang thai van dau hien tai (chi Host dung de xu ly) -------
    Public CurrentRoundNo As Integer = 0

    ''' <summary>seat -> danh sach cuoc cua seat do trong van hien tai (co the > 1 cuoc/seat).</summary>
    Public CurrentBets As New Dictionary(Of Integer, List(Of BetInfo))

    Private rngInstance As New Random()

    ''' <summary>Bat dau van moi: tang so van, xoa het cuoc cu.</summary>
    Public Sub StartNewRound()
        CurrentRoundNo += 1
        CurrentBets.Clear()
    End Sub

    ''' <summary>Tong so cuoc (moi loai) hien tai cua 1 seat trong van - dung de kiem tra
    ''' seat co du diem dat them cuoc moi khong (tong tat ca cuoc khong vuot qua diem hien co).</summary>
    Public Function TotalBetAmount(seat As Integer) As Long
        If Not CurrentBets.ContainsKey(seat) Then Return 0
        Dim total As Long = 0
        For Each b As BetInfo In CurrentBets(seat)
            total += b.Amount
        Next b
        Return total
    End Function

    ''' <summary>Ghi nhan 1 cuoc moi cho seat (cong don vao danh sach cuoc cua van nay).
    ''' Tra False neu du lieu khong hop le (sai gia tri, cuoc ngoai khoang MIN_BET..MAX_BET,
    ''' hoac tong cuoc sau khi them vuot qua diem hien co cua seat).</summary>
    Public Function PlaceBet(seat As Integer, kind As BetKind, value As Integer, amount As Long, currentScore As Long) As Boolean
        If amount < MIN_BET OrElse amount > MAX_BET Then Return False

        Select Case kind
            Case BetKind.BoBa
                If value < 1 OrElse value > 6 Then Return False
            Case BetKind.TongDiem
                If Not PAYOUT_BY_SUM.ContainsKey(value) Then Return False
            Case BetKind.Tren, BetKind.Duoi
                value = 0 ' khong dung, ep ve 0 cho nhat quan
            Case Else
                Return False
        End Select

        If TotalBetAmount(seat) + amount > currentScore Then Return False

        Dim b As New BetInfo()
        b.Seat = seat
        b.Kind = kind
        b.Value = value
        b.Amount = amount

        If Not CurrentBets.ContainsKey(seat) Then CurrentBets(seat) = New List(Of BetInfo)
        CurrentBets(seat).Add(b)
        Return True
    End Function

    Public Function HasAnyBet(seat As Integer) As Boolean
        Return CurrentBets.ContainsKey(seat) AndAlso CurrentBets(seat).Count > 0
    End Function

    ''' <summary>Host tung 3 xuc xac, moi con doc lap 1..6.</summary>
    Public Function RollDice() As Integer()
        Dim result(DICE_COUNT - 1) As Integer
        For i As Integer = 0 To DICE_COUNT - 1
            result(i) = rngInstance.Next(1, 7)
        Next i
        Return result
    End Function

    ''' <summary>Tinh thuong/thua cho tat ca cuoc da dat trong van, dua vao 3 xuc xac ket qua.
    ''' scoresBySeat la diem HIEN TAI cua tung seat (se duoc cong don va cap nhat trong outcome).</summary>
    Public Function ComputePayouts(diceResult As Integer(), scoresBySeat As Dictionary(Of Integer, Long)) As List(Of RoundOutcome)
        Dim sum As Integer = diceResult(0) + diceResult(1) + diceResult(2)
        Dim isTriple As Boolean = (diceResult(0) = diceResult(1) AndAlso diceResult(1) = diceResult(2))
        Dim tripleValue As Integer = If(isTriple, diceResult(0), 0)

        Dim results As New List(Of RoundOutcome)

        For Each kv As KeyValuePair(Of Integer, List(Of BetInfo)) In CurrentBets
            For Each b As BetInfo In kv.Value
                Dim outcome As New RoundOutcome()
                outcome.Seat = b.Seat
                outcome.Kind = b.Kind
                outcome.Value = b.Value
                outcome.Amount = b.Amount

                Select Case b.Kind
                    Case BetKind.Tren
                        ' Tren thang khi tong 11..17 VA khong phai Bo Ba (Bo Ba luon thua Tren/Duoi)
                        If (Not isTriple) AndAlso sum >= 11 AndAlso sum <= 17 Then
                            outcome.Won = True
                            outcome.Payout = b.Amount * CLng(TRENDUOI_MULTIPLIER)
                        Else
                            outcome.Won = False
                            outcome.Payout = -b.Amount
                        End If

                    Case BetKind.Duoi
                        ' Duoi thang khi tong 4..10 VA khong phai Bo Ba
                        If (Not isTriple) AndAlso sum >= 4 AndAlso sum <= 10 Then
                            outcome.Won = True
                            outcome.Payout = b.Amount * CLng(TRENDUOI_MULTIPLIER)
                        Else
                            outcome.Won = False
                            outcome.Payout = -b.Amount
                        End If

                    Case BetKind.BoBa
                        If isTriple AndAlso tripleValue = b.Value Then
                            outcome.Won = True
                            outcome.Payout = b.Amount * CLng(BOBA_MULTIPLIER)
                        Else
                            outcome.Won = False
                            outcome.Payout = -b.Amount
                        End If

                    Case BetKind.TongDiem
                        If sum = b.Value Then
                            Dim mult As Integer = PAYOUT_BY_SUM(b.Value)
                            outcome.Won = True
                            outcome.Payout = b.Amount * CLng(mult)
                        Else
                            outcome.Won = False
                            outcome.Payout = -b.Amount
                        End If
                End Select

                Dim oldScore As Long = 0
                If scoresBySeat.ContainsKey(b.Seat) Then oldScore = scoresBySeat(b.Seat)
                Dim newScore As Long = oldScore + outcome.Payout
                scoresBySeat(b.Seat) = newScore
                outcome.NewScore = newScore

                results.Add(outcome)
            Next b
        Next kv

        Return results
    End Function

End Class
