Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports System.IO
Imports Microsoft.VisualBasic

''' <summary>
''' Game "Tai Xiu 3 Xuc Xac": toi da 4 nguoi choi (1 Host + 3 Client) cung dat cuoc
''' tren 1 van chung. Moi van, 1 nguoi co the dat NHIEU loai cuoc cung luc (Tren/Duoi,
''' Bo Ba 1 so cu the, Tong diem cu the 4..17). Host tung 3 xuc xac, tinh thuong/thua
''' cho tat ca cuoc dua vao TaiXiuGame.ComputePayouts(). Kien truc mang tai su dung
''' nguyen NetworkHub.vb (Host) / NetworkPeer.vb (Client) tu bo game co san.
''' </summary>
Public Class Form1
    Inherits Form

    Private Const DEFAULT_PORT As Integer = 9051
    Private Const BETTING_SECONDS As Integer = 20
    Private Const DICE_ANIM_STEPS As Integer = 9
    Private Const DICE_ANIM_INTERVAL_MS As Integer = 70

    Private Enum RoundState
        Idle
        Betting
        Rolling
        ShowingResult
    End Enum

    ' ------------------- Mang -------------------
    Private hub As NetworkHub
    Private peer As NetworkPeer
    Private isHost As Boolean = False
    Private localSeat As Integer = -1  ' 0 = Host, 1..3 = Client
    Private playerNames(3) As String
    Private playerConnected(3) As Boolean

    ' ------------------- Game -------------------
    Private game As New TaiXiuGame()
    Private scoresBySeat As New Dictionary(Of Integer, Long)
    Private state As RoundState = RoundState.Idle
    Private secondsLeft As Integer = 0
    Private countdownTimer As Timer

    ' Tong cuoc da dat (tat ca loai cong lai) cua tung seat trong van hien tai - de hien thi + kiem tra local
    Private myBetsThisRound As New List(Of String)  ' hien thi dang text: "Trên: 50", "Bộ Ba 4: 20"...
    Private myBetKindsThisRound As New List(Of TaiXiuGame.BetKind) ' song song voi myBetsThisRound, de undo dung khi bi TX_BET_FAIL
    Private myHasNumberBet As Boolean = False  ' da chon 1 so (Bo Ba HOAC Tong diem) trong van nay chua
    Private myHasSideBet As Boolean = False    ' da chon 1 cua (Tren HOAC Duoi) trong van nay chua
    Private mySelectedNumberBtn As Button      ' nut so da chon, dung de highlight/bo highlight
    Private mySelectedSideBtn As Button        ' nut cua da chon, dung de highlight/bo highlight

    ' ------------------- Xuc xac + animation (tai su dung tu app Quay Xuc Xac) -------------------
    Private diceSprite(6) As Image
    Private diceTumbleSprite(4) As Image
    Private spritesLoaded As Boolean = False
    Private picDice(2) As Panel
    Private diceAnimTimer As Timer
    Private diceAnimStep As Integer = 0
    Private diceAnimTargetValue(2) As Integer
    Private diceTumbleIndex(2) As Integer
    Private diceFaceShown(2) As Integer
    Private diceAnimRng As New Random()
    Private diceStopStep(2) As Integer
    Private lastDiceResult(2) As Integer

    ' ------------------- UI: Connect panel -------------------
    Private pnlConnect As Panel
    Private txtName As TextBox
    Private txtIP As TextBox
    Private txtPort As TextBox
    Private btnHost As Button
    Private btnJoin As Button
    Private lblConnectStatus As Label

    ' ------------------- UI: Game panel -------------------
    Private pnlGame As Panel
    Private lblRoundInfo As Label
    Private lblCountdown As Label
    Private nudBet As NumericUpDown
    Private btnBetTren As Button
    Private btnBetDuoi As Button
    Private btnBetBoBa(6) As Button       ' index 1..6
    Private btnBetTong(17) As Button      ' index 4..17
    Private lstMyBets As ListBox
    Private btnHostNewRound As Button
    Private btnHostRoll As Button
    Private pnlPlayers(3) As Panel
    Private lblCardStatus(3) As Label
    Private lblCardStats(3) As Label
    Private pnlRoundHistory As Panel
    Private roundHistory As New List(Of RoundHistoryEntry)
    Private currentRoundNoLocal As Integer = 0

    ' ------------------- Sprite trang tri (nen go, ruong, xu) -------------------
    Private sprBgWood As Image
    Private sprChest As Image
    Private sprCoin As Image
    Private picChest As PictureBox

    ' ------------------- UI: Chat panel -------------------
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private btnSend As Button

    Public Sub New()
        Me.Text = "Tài Xỉu 3 Xúc Xắc"
        Me.ClientSize = New Size(980, 720)
        Me.StartPosition = FormStartPosition.CenterScreen
        Dim i As Integer
        For i = 0 To 3
            playerNames(i) = "Người chơi " & (i + 1).ToString()
            playerConnected(i) = False
            scoresBySeat(i) = TaiXiuGame.STARTING_SCORE
        Next i
        LoadSprites()
        BuildConnectPanel()
    End Sub

    ' ============================================================
    '  LOAD SPRITE XUC XAC (fallback ve hinh khoi + so neu thieu file)
    ' ============================================================
    Private Sub LoadSprites()
        Try
            Dim dir As String = Path.Combine(Application.StartupPath, "Assets")
            Dim v As Integer
            For v = 1 To 6
                diceSprite(v) = LoadImg(dir, "dice_" & v.ToString() & ".png")
            Next v
            Dim t As Integer
            For t = 1 To 4
                diceTumbleSprite(t) = LoadImg(dir, "dice_roll_" & t.ToString() & ".png")
            Next t
            spritesLoaded = True

            ' Sprite trang tri: khong bat buoc, thieu file nao thi fallback mau phang cho phan do (xem cac Sub Build...)
            sprBgWood = LoadImg(dir, "bg_wood_table.png")
            sprChest = LoadImg(dir, "chest_icon.png")
            sprCoin = LoadImg(dir, "coin_icon.png")
        Catch
            spritesLoaded = False
        End Try
    End Sub

    Private Function LoadImg(dir As String, name As String) As Image
        Dim p As String = Path.Combine(dir, name)
        If File.Exists(p) Then Return Image.FromFile(p)
        Return Nothing
    End Function

    ' ============================================================
    '  UI: KET NOI
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Dock = DockStyle.Fill
        pnlConnect.BackColor = Color.FromArgb(245, 245, 240)

        Dim lblTitle As New Label()
        lblTitle.Text = "TÀI XỈU 3 XÚC XẮC"
        lblTitle.Font = New Font("Segoe UI", 18.0!, FontStyle.Bold)
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(40, 30)
        pnlConnect.Controls.Add(lblTitle)

        Dim lblName As New Label() : lblName.Text = "Tên của bạn:" : lblName.AutoSize = True
        lblName.Location = New Point(40, 100)
        pnlConnect.Controls.Add(lblName)
        txtName = New TextBox() : txtName.Location = New Point(40, 122) : txtName.Size = New Size(220, 24)
        txtName.Text = "Người chơi"
        pnlConnect.Controls.Add(txtName)

        Dim lblPort As New Label() : lblPort.Text = "Cổng (Port):" : lblPort.AutoSize = True
        lblPort.Location = New Point(40, 160)
        pnlConnect.Controls.Add(lblPort)
        txtPort = New TextBox() : txtPort.Location = New Point(40, 182) : txtPort.Size = New Size(220, 24)
        txtPort.Text = DEFAULT_PORT.ToString()
        pnlConnect.Controls.Add(txtPort)

        btnHost = New Button() : btnHost.Text = "Tạo phòng (Host)"
        btnHost.Location = New Point(40, 220) : btnHost.Size = New Size(220, 34)
        AddHandler btnHost.Click, AddressOf BtnHost_Click
        pnlConnect.Controls.Add(btnHost)

        Dim lblIP As New Label() : lblIP.Text = "IP của Host:" : lblIP.AutoSize = True
        lblIP.Location = New Point(40, 280)
        pnlConnect.Controls.Add(lblIP)
        txtIP = New TextBox() : txtIP.Location = New Point(40, 302) : txtIP.Size = New Size(220, 24)
        txtIP.Text = "127.0.0.1"
        pnlConnect.Controls.Add(txtIP)

        btnJoin = New Button() : btnJoin.Text = "Vào phòng (Join)"
        btnJoin.Location = New Point(40, 336) : btnJoin.Size = New Size(220, 34)
        AddHandler btnJoin.Click, AddressOf BtnJoin_Click
        pnlConnect.Controls.Add(btnJoin)

        lblConnectStatus = New Label()
        lblConnectStatus.Location = New Point(40, 390) : lblConnectStatus.Size = New Size(500, 60)
        lblConnectStatus.ForeColor = Color.DimGray
        pnlConnect.Controls.Add(lblConnectStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    Private Sub BtnHost_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text.Trim(), port) Then
            MessageBox.Show("Port không hợp lệ.") : Return
        End If
        isHost = True
        localSeat = 0
        playerNames(0) = SafeName(txtName.Text)
        playerConnected(0) = True

        hub = New NetworkHub(Me)
        AddHandler hub.ClientConnected, AddressOf Hub_ClientConnected
        AddHandler hub.ClientDisconnected, AddressOf Hub_ClientDisconnected
        AddHandler hub.LineReceivedFromClient, AddressOf Hub_LineReceived
        hub.StartListening(port)

        lblConnectStatus.Text = "Đang chờ người chơi kết nối trên cổng " & port.ToString() & " ..."
        ShowGamePanel()
    End Sub

    Private Sub BtnJoin_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text.Trim(), port) Then
            MessageBox.Show("Port không hợp lệ.") : Return
        End If
        isHost = False
        playerNames(0) = SafeName(txtName.Text)

        peer = New NetworkPeer(Me)
        AddHandler peer.Connected, AddressOf Peer_Connected
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        peer.ConnectToHost(txtIP.Text.Trim(), port)

        lblConnectStatus.Text = "Đang kết nối đến " & txtIP.Text.Trim() & ":" & port.ToString() & " ..."
    End Sub

    Private Function SafeName(raw As String) As String
        Dim s As String = raw.Trim()
        If s = "" Then Return "Người chơi"
        If s.Length > 16 Then s = s.Substring(0, 16)
        Return s
    End Function

    ' ============================================================
    '  SU KIEN MANG - CLIENT
    ' ============================================================
    Private Sub Peer_Connected()
        peer.SendLine("TX_HELLO:" & playerNames(0))
    End Sub

    Private Sub Peer_Disconnected()
        AppendChat("[Hệ thống] Mất kết nối tới Host.")
    End Sub

    Private Sub Peer_LineReceived(line As String)
        HandleProtocolLine(line, -1)
    End Sub

    ' ============================================================
    '  SU KIEN MANG - HOST
    ' ============================================================
    Private Sub Hub_ClientConnected(seatIndex As Integer)
        playerConnected(seatIndex) = True
        hub.SendToClient(seatIndex, "TX_WELCOME:" & seatIndex.ToString())
        BroadcastNames()
        BroadcastScores()
        BroadcastConnected()
        RefreshPlayerCards()
        AppendChat("[Hệ thống] Player " & (seatIndex + 1).ToString() & " đã vào phòng.")
    End Sub

    Private Sub Hub_ClientDisconnected(seatIndex As Integer)
        playerConnected(seatIndex) = False
        playerNames(seatIndex) = "Người chơi " & (seatIndex + 1).ToString()
        BroadcastNames()
        BroadcastConnected()
        RefreshPlayerCards()
        AppendChat("[Hệ thống] Player " & (seatIndex + 1).ToString() & " đã rời phòng.")
    End Sub

    Private Sub Hub_LineReceived(seatIndex As Integer, line As String)
        HandleProtocolLine(line, seatIndex)
    End Sub

    ' ============================================================
    '  GIAO THUC CHUNG
    '  fromSeat = -1: Client dang nhan tu Host. fromSeat = 0..3: Host dang nhan tu Client do.
    ' ============================================================
    Private Sub HandleProtocolLine(line As String, fromSeat As Integer)
        If line Is Nothing OrElse line = "" Then Return
        Dim idx As Integer = line.IndexOf(":"c)
        Dim msgType As String = If(idx >= 0, line.Substring(0, idx), line)
        Dim payload As String = If(idx >= 0, line.Substring(idx + 1), "")

        Select Case msgType
            Case "CHAT"
                Dim p2 As Integer = payload.IndexOf(":"c)
                If p2 >= 0 Then AppendChat(payload.Substring(0, p2) & ": " & payload.Substring(p2 + 1))
                If isHost Then hub.BroadcastExcept("CHAT:" & payload, fromSeat)

            Case "TX_WELCOME"
                localSeat = Integer.Parse(payload, CultureInfo.InvariantCulture)
                ShowGamePanel()
                lblConnectStatus.Text = "Đã vào phòng, bạn là Player " & (localSeat + 1).ToString()

            Case "TX_HELLO"
                If fromSeat >= 0 Then
                    playerNames(fromSeat) = SafeName(payload)
                    BroadcastNames()
                    RefreshPlayerCards()
                End If

            Case "TX_NAMES"
                Dim parts As String() = payload.Split("|"c)
                Dim i As Integer
                For i = 0 To Math.Min(3, parts.Length - 1)
                    If parts(i) <> "" Then playerNames(i) = parts(i)
                Next i
                RefreshPlayerCards()

            Case "TX_SCORES"
                Dim sp As String() = payload.Split("|"c)
                Dim i2 As Integer
                For i2 = 0 To Math.Min(3, sp.Length - 1)
                    Dim v As Long
                    If Long.TryParse(sp(i2), NumberStyles.Integer, CultureInfo.InvariantCulture, v) Then
                        scoresBySeat(i2) = v
                    End If
                Next i2
                RefreshPlayerCards()

            Case "TX_CONN"
                Dim cp As String() = payload.Split("|"c)
                Dim i3 As Integer
                For i3 = 0 To Math.Min(3, cp.Length - 1)
                    playerConnected(i3) = (cp(i3).Trim() = "1")
                Next i3
                RefreshPlayerCards()

            Case "TX_ROUND"
                Dim rp As String() = payload.Split("|"c)
                Dim roundNo As Integer = Integer.Parse(rp(0), CultureInfo.InvariantCulture)
                Dim secs As Integer = Integer.Parse(rp(1), CultureInfo.InvariantCulture)
                BeginBettingLocal(roundNo, secs)

            Case "TX_BET"
                If fromSeat >= 0 AndAlso isHost Then
                    Dim bp As String() = payload.Split("|"c)
                    Dim kind As TaiXiuGame.BetKind = CType(Integer.Parse(bp(0), CultureInfo.InvariantCulture), TaiXiuGame.BetKind)
                    Dim value As Integer = Integer.Parse(bp(1), CultureInfo.InvariantCulture)
                    Dim amount As Long = Long.Parse(bp(2), CultureInfo.InvariantCulture)
                    ProcessBetFromSeat(fromSeat, kind, value, amount)
                End If

            Case "TX_BET_FAIL"
                Dim reasonMsg As String = If(payload.Trim() <> "", payload, "Cược không hợp lệ (sai số điểm hoặc không đủ điểm).")
                UndoLastMyBetDisplay()
                MessageBox.Show(reasonMsg)

            Case "TX_ROLL"
                Dim dp As String() = payload.Split(","c)
                Dim d(2) As Integer
                Dim k As Integer
                For k = 0 To 2
                    d(k) = Integer.Parse(dp(k), CultureInfo.InvariantCulture)
                Next k
                StartDiceAnim(d)

            Case "TX_RESULT"
                ' payload: "d0,d1,d2|seat,kind,value,amount,won,payout,newScore;seat,..."
                Dim barIdx As Integer = payload.IndexOf("|"c)
                Dim diceStr As String = payload.Substring(0, barIdx)
                Dim entriesStr As String = payload.Substring(barIdx + 1)
                ApplyResult(diceStr, entriesStr)

        End Select
    End Sub

    ' ============================================================
    '  BROADCAST HELPER (chi Host dung)
    ' ============================================================
    Private Sub BroadcastNames()
        If Not isHost Then Return
        Dim s As String = playerNames(0) & "|" & playerNames(1) & "|" & playerNames(2) & "|" & playerNames(3)
        hub.Broadcast("TX_NAMES:" & s)
    End Sub

    Private Sub BroadcastScores()
        If Not isHost Then Return
        Dim s As String = scoresBySeat(0).ToString() & "|" & scoresBySeat(1).ToString() & "|" &
                           scoresBySeat(2).ToString() & "|" & scoresBySeat(3).ToString()
        hub.Broadcast("TX_SCORES:" & s)
    End Sub

    Private Sub BroadcastConnected()
        If Not isHost Then Return
        Dim s As String = (If(playerConnected(0), "1", "0")) & "|" & (If(playerConnected(1), "1", "0")) & "|" &
                           (If(playerConnected(2), "1", "0")) & "|" & (If(playerConnected(3), "1", "0"))
        hub.Broadcast("TX_CONN:" & s)
    End Sub

    ' ============================================================
    '  UI: GAME PANEL
    ' ============================================================
    Private Sub ShowGamePanel()
        pnlConnect.Visible = False
        If pnlGame IsNot Nothing Then Return ' chi dung 1 lan

        pnlGame = New Panel()
        pnlGame.Dock = DockStyle.Fill
        pnlGame.BackColor = Color.FromArgb(30, 40, 35)
        If sprBgWood IsNot Nothing Then
            pnlGame.BackgroundImage = sprBgWood
            pnlGame.BackgroundImageLayout = ImageLayout.Stretch
        End If
        Me.Controls.Add(pnlGame)

        BuildDicePanel()
        BuildBetPanel()
        BuildPlayerCards()
        BuildChatPanel()
        BuildHostControls()
        BuildRoundHistoryPanel()

        RefreshPlayerCards()
        UpdateBetButtonsEnabled()
    End Sub

    Private Sub BuildRoundHistoryPanel()
        Dim lblTitle As New Label()
        lblTitle.Text = "Nhật ký kết quả (gần đây nhất bên trái):"
        lblTitle.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        lblTitle.ForeColor = Color.White
        lblTitle.BackColor = Color.Transparent
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(700, 68)
        pnlGame.Controls.Add(lblTitle)
        lblTitle.BringToFront()

        pnlRoundHistory = New Panel()
        pnlRoundHistory.Location = New Point(700, 90)
        pnlRoundHistory.Size = New Size(250, 240)
        pnlRoundHistory.BackColor = Color.FromArgb(12, 16, 20)
        pnlRoundHistory.BorderStyle = BorderStyle.FixedSingle
        pnlRoundHistory.AutoScroll = True
        AddHandler pnlRoundHistory.Paint, AddressOf RoundHistoryPanel_Paint
        pnlGame.Controls.Add(pnlRoundHistory)
        pnlRoundHistory.BringToFront()
    End Sub

    ''' <summary>Ve toan bo nhat ky: moi van la 1 cot gom nhan "Trên/Dưới/-" + 3 vien tron nho
    ''' chua so xuc xac, van gan nhat nam ben trai nhat (giong dung kieu nhat ky trong
    ''' GamePvP-VongQuayRong: HistoryPanel_Paint tu ve toan bo, khong dung ListBox/child control).</summary>
    Private Sub RoundHistoryPanel_Paint(sender As Object, e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        Const colW As Integer = 40
        Const circleSize As Integer = 20
        Const circleGap As Integer = 2
        Dim x As Integer = 6

        For Each entry As RoundHistoryEntry In roundHistory
            Dim labelColor As Color = Color.FromArgb(220, 195, 140)
            Using fnt As New Font("Segoe UI", 7.5!, FontStyle.Bold)
                Using txtBrush As New SolidBrush(labelColor)
                    g.DrawString(entry.SideLabel, fnt, txtBrush, New PointF(x, 4))
                End Using
            End Using

            For i As Integer = 0 To 2
                Dim cy As Integer = 22 + i * (circleSize + circleGap)
                Dim rect As New Rectangle(x, cy, circleSize, circleSize)
                Using bg As New SolidBrush(Color.FromArgb(225, 210, 175))
                    g.FillEllipse(bg, rect)
                End Using
                Using pen As New Pen(Color.FromArgb(120, 95, 55), 1.0F)
                    g.DrawEllipse(pen, rect)
                End Using
                Using sf As New StringFormat()
                    sf.Alignment = StringAlignment.Center
                    sf.LineAlignment = StringAlignment.Center
                    Using fnt As New Font("Segoe UI", 8.5!, FontStyle.Bold)
                        Using txtBrush As New SolidBrush(Color.FromArgb(70, 45, 20))
                            g.DrawString(entry.Dice(i).ToString(), fnt, txtBrush, New RectangleF(rect.X, rect.Y, rect.Width, rect.Height), sf)
                        End Using
                    End Using
                End Using
            Next i

            x += colW
        Next entry

        pnlRoundHistory.AutoScrollMinSize = New Size(Math.Max(pnlRoundHistory.Width, x + 10), 0)
    End Sub

    ''' <summary>1 dong du lieu nhat ky: nhan cua/tren-duoi + 3 mat xuc xac cua van do.</summary>
    Private Class RoundHistoryEntry
        Public SideLabel As String
        Public Dice As Integer()
    End Class

    ''' <summary>Them 1 van vua ket thuc vao dau danh sach nhat ky (van gan nhat o ben trai nhat,
    ''' giong quy uoc trong GamePvP-VongQuayRong), gioi han toi da 25 van gan nhat.</summary>
    Private Sub AddRoundHistoryEntry(roundNo As Integer, dice As Integer(), sum As Integer, myNetPayout As Long, hasMyOutcome As Boolean)
        Dim isTriple As Boolean = (dice(0) = dice(1) AndAlso dice(1) = dice(2))
        Dim sideLabel As String
        If isTriple Then
            sideLabel = "-"
        ElseIf sum >= 11 Then
            sideLabel = "Trên"
        Else
            sideLabel = "Dưới"
        End If

        Dim entry As New RoundHistoryEntry()
        entry.SideLabel = sideLabel
        entry.Dice = New Integer() {dice(0), dice(1), dice(2)}
        roundHistory.Insert(0, entry)
        If roundHistory.Count > 25 Then roundHistory.RemoveAt(roundHistory.Count - 1)

        If pnlRoundHistory IsNot Nothing Then
            pnlRoundHistory.Invalidate()
        End If
    End Sub

    Private Sub BuildDicePanel()
        lblRoundInfo = New Label()
        lblRoundInfo.Text = "Chờ Host bắt đầu ván mới..."
        lblRoundInfo.Font = New Font("Segoe UI", 11.0!, FontStyle.Bold)
        lblRoundInfo.ForeColor = Color.White
        lblRoundInfo.Location = New Point(20, 15)
        lblRoundInfo.Size = New Size(400, 24)
        pnlGame.Controls.Add(lblRoundInfo)

        lblCountdown = New Label()
        lblCountdown.Text = ""
        lblCountdown.Font = New Font("Segoe UI", 11.0!, FontStyle.Bold)
        lblCountdown.ForeColor = Color.Gold
        lblCountdown.Location = New Point(430, 15)
        lblCountdown.Size = New Size(120, 24)
        pnlGame.Controls.Add(lblCountdown)

        Dim diceSize As Integer = 70
        Dim gap As Integer = 16
        Dim startX As Integer = 20
        For i As Integer = 0 To 2
            Dim p As New Panel()
            p.Location = New Point(startX + i * (diceSize + gap), 50)
            p.Size = New Size(diceSize, diceSize)
            p.BackColor = Color.FromArgb(245, 245, 240)
            Dim idx As Integer = i
            AddHandler p.Paint, Sub(sender As Object, e As PaintEventArgs) PaintOneDice(e.Graphics, p, idx)
            pnlGame.Controls.Add(p)
            picDice(i) = p
        Next i

        ' Ruong kho bau: chi trang tri, dat vao khoang trong ben phai 3 xuc xac
        If sprChest IsNot Nothing Then
            picChest = New PictureBox()
            picChest.Image = sprChest
            picChest.SizeMode = PictureBoxSizeMode.Zoom
            picChest.Location = New Point(280, 42)
            picChest.Size = New Size(120, 90)
            picChest.BackColor = Color.Transparent
            pnlGame.Controls.Add(picChest)
            picChest.SendToBack()
        End If
    End Sub

    Private Sub BuildBetPanel()
        Dim lblAmount As New Label()
        lblAmount.Text = "Số điểm cược mỗi lần bấm:"
        lblAmount.ForeColor = Color.White
        lblAmount.AutoSize = True
        lblAmount.Location = New Point(20, 135)
        pnlGame.Controls.Add(lblAmount)

        nudBet = New NumericUpDown()
        nudBet.Location = New Point(230, 132)
        nudBet.Size = New Size(90, 24)
        nudBet.Minimum = CDec(TaiXiuGame.MIN_BET)
        nudBet.Maximum = CDec(TaiXiuGame.MAX_BET)
        nudBet.Value = CDec(TaiXiuGame.MIN_BET)
        nudBet.Increment = 10
        nudBet.BackColor = Color.FromArgb(250, 250, 245)
        nudBet.ForeColor = Color.Black
        nudBet.Font = New Font("Segoe UI", 9.5!, FontStyle.Bold)
        pnlGame.Controls.Add(nudBet)

        If sprCoin IsNot Nothing Then
            Dim picCoin As New PictureBox()
            picCoin.Image = sprCoin
            picCoin.SizeMode = PictureBoxSizeMode.Zoom
            picCoin.Location = New Point(328, 130)
            picCoin.Size = New Size(20, 20)
            picCoin.BackColor = Color.Transparent
            pnlGame.Controls.Add(picCoin)
        End If

        ' --- Trên / Dưới (dung khung anh neu co, fallback mau phang) ---
        btnBetTren = New Button()
        btnBetTren.Text = "TRÊN (11-17) x1"
        btnBetTren.Location = New Point(20, 165) : btnBetTren.Size = New Size(150, 40)
        StyleBetButton(btnBetTren, Color.FromArgb(178, 58, 46))
        AddHandler btnBetTren.Click, Sub(s As Object, e As EventArgs) TryPlaceBet(TaiXiuGame.BetKind.Tren, 0, CType(s, Button))
        pnlGame.Controls.Add(btnBetTren)

        btnBetDuoi = New Button()
        btnBetDuoi.Text = "DƯỚI (4-10) x1"
        btnBetDuoi.Location = New Point(180, 165) : btnBetDuoi.Size = New Size(150, 40)
        StyleBetButton(btnBetDuoi, Color.FromArgb(40, 105, 150))
        AddHandler btnBetDuoi.Click, Sub(s As Object, e As EventArgs) TryPlaceBet(TaiXiuGame.BetKind.Duoi, 0, CType(s, Button))
        pnlGame.Controls.Add(btnBetDuoi)

        ' --- Bo Ba 1..6 ---
        Dim lblBoBa As New Label()
        lblBoBa.Text = "Bộ Ba (x150):"
        lblBoBa.ForeColor = Color.Gold
        lblBoBa.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        lblBoBa.AutoSize = True
        lblBoBa.Location = New Point(20, 215)
        pnlGame.Controls.Add(lblBoBa)

        For v As Integer = 1 To 6
            Dim btn As New Button()
            btn.Text = v.ToString() & "-" & v.ToString() & "-" & v.ToString()
            btn.Location = New Point(20 + (v - 1) * 58, 238) : btn.Size = New Size(54, 40)
            StyleBetButton(btn, Color.FromArgb(150, 110, 30))
            Dim vv As Integer = v
            AddHandler btn.Click, Sub(s As Object, e As EventArgs) TryPlaceBet(TaiXiuGame.BetKind.BoBa, vv, CType(s, Button))
            pnlGame.Controls.Add(btn)
            btnBetBoBa(v) = btn
        Next v

        ' --- Tong diem 4..17 (luoi giong bang cuoc trong anh) ---
        Dim lblTong As New Label()
        lblTong.Text = "Tổng điểm (hệ số hiện trên nút):"
        lblTong.ForeColor = Color.Gold
        lblTong.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        lblTong.AutoSize = True
        lblTong.Location = New Point(20, 290)
        pnlGame.Controls.Add(lblTong)

        Dim colsPerRow As Integer = 7
        Dim col As Integer = 0
        Dim row As Integer = 0
        For sum As Integer = 4 To 17
            Dim mult As Integer = TaiXiuGame.PAYOUT_BY_SUM(sum)
            Dim btn As New Button()
            btn.Text = sum.ToString() & vbCrLf & "x" & mult.ToString()
            btn.Location = New Point(20 + col * 58, 313 + row * 46) : btn.Size = New Size(54, 42)
            StyleBetButton(btn, Color.FromArgb(70, 95, 65))
            Dim ss As Integer = sum
            AddHandler btn.Click, Sub(s As Object, e As EventArgs) TryPlaceBet(TaiXiuGame.BetKind.TongDiem, ss, CType(s, Button))
            pnlGame.Controls.Add(btn)
            btnBetTong(sum) = btn
            col += 1
            If col >= colsPerRow Then
                col = 0
                row += 1
            End If
        Next sum

        lstMyBets = New ListBox()
        lstMyBets.Location = New Point(430, 45) : lstMyBets.Size = New Size(220, 200)
        lstMyBets.BackColor = Color.FromArgb(250, 250, 245)
        lstMyBets.ForeColor = Color.Black
        lstMyBets.Font = New Font("Segoe UI", 9.5!)
        pnlGame.Controls.Add(lstMyBets)
        Dim lblMyBets As New Label()
        lblMyBets.Text = "Cược của bạn ván này:"
        lblMyBets.ForeColor = Color.White
        lblMyBets.AutoSize = True
        lblMyBets.Location = New Point(430, 22)
        pnlGame.Controls.Add(lblMyBets)
    End Sub

    ''' <summary>Ap dung mau nen sang ro + chu trang dam + FlatStyle cho nut cuoc, de de nhin
    ''' tren nen toi cua bang game (khac phan hoi: nut/o qua toi kho doc). Luu lai style goc +
    ''' vi tri goc vao Tag de sau nay UnhighlightBetButton() co the phuc hoi dung nguyen trang.</summary>
    Private Sub StyleBetButton(btn As Button, backColor As Color)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderColor = Color.FromArgb(230, 230, 220)
        btn.FlatAppearance.BorderSize = 1
        btn.BackColor = backColor
        btn.BackgroundImage = Nothing
        btn.ForeColor = Color.White
        btn.Font = New Font("Segoe UI", 8.5!, FontStyle.Bold)
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.25F)
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.15F)
        btn.Tag = Tuple.Create(Of Image, Color, Rectangle)(Nothing, backColor, btn.Bounds)
    End Sub

    ''' <summary>Neu co anh khung trang tri (frameImg) thi dung lam BackgroundImage cho nut (chu
    ''' trang dam, co do bong de doc duoc tren moi nen khung); neu khong co anh thi fallback ve
    ''' StyleBetButton mau phang nhu cu. Luu lai style goc + vi tri goc vao Tag.</summary>
    Private Sub StyleBetButtonWithFrame(btn As Button, frameImg As Image, fallbackColor As Color)
        If frameImg Is Nothing Then
            StyleBetButton(btn, fallbackColor)
            Return
        End If
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 0
        btn.BackgroundImage = frameImg
        btn.BackgroundImageLayout = ImageLayout.Stretch
        btn.BackColor = Color.Transparent
        btn.ForeColor = Color.White
        btn.Font = New Font("Segoe UI", 8.5!, FontStyle.Bold)
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 255, 255, 255)
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 0, 0, 0)
        btn.Tag = Tuple.Create(Of Image, Color, Rectangle)(frameImg, fallbackColor, btn.Bounds)
    End Sub

    ''' <summary>Lam nut "noi bat" len khi nguoi choi vua chon: phong to nhe (+6px moi chieu),
    ''' vien vang sang day, dua len tren cung (BringToFront) de khong bi nut ben canh de len.</summary>
    Private Sub HighlightBetButton(btn As Button)
        If btn Is Nothing Then Return
        Dim original As Rectangle = btn.Bounds
        If TypeOf btn.Tag Is Tuple(Of Image, Color, Rectangle) Then
            original = CType(btn.Tag, Tuple(Of Image, Color, Rectangle)).Item3
        End If
        btn.FlatAppearance.BorderSize = 4
        btn.FlatAppearance.BorderColor = Color.FromArgb(255, 225, 60) ' vang sang, noi bat ro
        btn.Bounds = New Rectangle(original.X - 3, original.Y - 3, original.Width + 6, original.Height + 6)
        btn.BringToFront()
    End Sub

    ''' <summary>Tra nut ve trang thai binh thuong (vi tri + style goc da luu trong Tag).</summary>
    Private Sub UnhighlightBetButton(btn As Button)
        If btn Is Nothing Then Return
        If Not (TypeOf btn.Tag Is Tuple(Of Image, Color, Rectangle)) Then Return
        Dim data As Tuple(Of Image, Color, Rectangle) = CType(btn.Tag, Tuple(Of Image, Color, Rectangle))
        btn.Bounds = data.Item3
        If data.Item1 IsNot Nothing Then
            StyleBetButtonWithFrame(btn, data.Item1, data.Item2)
        Else
            StyleBetButton(btn, data.Item2)
        End If
    End Sub

    Private Sub BuildHostControls()
        btnHostNewRound = New Button()
        btnHostNewRound.Text = "Bắt đầu ván mới (Host)"
        btnHostNewRound.Location = New Point(430, 250) : btnHostNewRound.Size = New Size(220, 36)
        StyleBetButton(btnHostNewRound, Color.FromArgb(60, 130, 70))
        AddHandler btnHostNewRound.Click, AddressOf BtnHostNewRound_Click
        pnlGame.Controls.Add(btnHostNewRound)

        btnHostRoll = New Button()
        btnHostRoll.Text = "Tung xúc xắc ngay (Host)"
        btnHostRoll.Location = New Point(430, 292) : btnHostRoll.Size = New Size(220, 36)
        StyleBetButton(btnHostRoll, Color.FromArgb(150, 120, 30))
        btnHostRoll.Enabled = False
        AddHandler btnHostRoll.Click, AddressOf BtnHostRoll_Click
        pnlGame.Controls.Add(btnHostRoll)

        btnHostNewRound.Visible = isHost
        btnHostRoll.Visible = isHost
    End Sub

    ''' <summary>Mau rieng cho tung seat (giong quy uoc trong GamePvP-VongQuayRong): do/xanh duong/
    ''' xanh la/tim, dung cho vach mau ben trai the va tieu de ten nguoi choi.</summary>
    Private Function PlayerColor(seat As Integer) As Color
        Select Case seat
            Case 0 : Return Color.FromArgb(200, 40, 40)
            Case 1 : Return Color.FromArgb(30, 110, 200)
            Case 2 : Return Color.FromArgb(30, 150, 70)
            Case Else : Return Color.FromArgb(160, 90, 190)
        End Select
    End Function

    Private Sub BuildPlayerCards()
        For p As Integer = 0 To 3
            Dim card As New Panel()
            card.Location = New Point(430 + (p Mod 2) * 130, 340 + (p \ 2) * 90)
            card.Size = New Size(120, 80)
            card.BackColor = Color.White
            card.BorderStyle = BorderStyle.FixedSingle
            pnlGame.Controls.Add(card)
            pnlPlayers(p) = card

            Dim bar As New Panel()
            bar.Location = New Point(0, 0)
            bar.Size = New Size(6, 80)
            bar.BackColor = PlayerColor(p)
            card.Controls.Add(bar)

            Dim title As New Label()
            title.Name = "title"
            title.Text = "Player " & (p + 1).ToString()
            title.ForeColor = PlayerColor(p)
            title.Font = New Font("Segoe UI", 8.5!, FontStyle.Bold)
            title.Location = New Point(14, 6) : title.Size = New Size(100, 16)
            card.Controls.Add(title)

            Dim status As New Label()
            status.ForeColor = Color.DimGray
            status.Font = New Font("Segoe UI", 8.5!)
            status.Location = New Point(14, 26) : status.Size = New Size(100, 40)
            card.Controls.Add(status)
            lblCardStatus(p) = status

            If isHost AndAlso p <> 0 Then
                Dim seatCaptured As Integer = p
                Dim cms As New ContextMenuStrip()
                Dim itemAdd As New ToolStripMenuItem("Nạp điểm cho người chơi này...")
                AddHandler itemAdd.Click, Sub(s As Object, e As EventArgs) AdjustPlayerScore(seatCaptured, True)
                cms.Items.Add(itemAdd)
                Dim itemSub As New ToolStripMenuItem("Trừ điểm của người chơi này...")
                AddHandler itemSub.Click, Sub(s As Object, e As EventArgs) AdjustPlayerScore(seatCaptured, False)
                cms.Items.Add(itemSub)
                card.ContextMenuStrip = cms
                title.ContextMenuStrip = cms
                status.ContextMenuStrip = cms
            End If
        Next p
    End Sub

    Private Sub BuildChatPanel()
        pnlChat = New Panel()
        pnlChat.Location = New Point(20, 540)
        pnlChat.Size = New Size(630, 160)
        pnlGame.Controls.Add(pnlChat)

        lstChat = New ListBox()
        lstChat.Location = New Point(0, 0) : lstChat.Size = New Size(630, 120)
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(0, 126) : txtChatInput.Size = New Size(520, 24)
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gửi"
        btnSend.Location = New Point(526, 124) : btnSend.Size = New Size(100, 28)
        StyleBetButton(btnSend, Color.FromArgb(60, 100, 130))
        AddHandler btnSend.Click, AddressOf BtnSend_Click
        pnlChat.Controls.Add(btnSend)
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As EventArgs)
        Dim msg As String = txtChatInput.Text.Trim()
        If msg = "" Then Return
        Dim mySeat As Integer = If(isHost, 0, localSeat)
        Dim line As String = playerNames(If(mySeat >= 0, mySeat, 0)) & ":" & msg
        AppendChat(playerNames(If(mySeat >= 0, mySeat, 0)) & ": " & msg)
        If isHost Then
            hub.Broadcast("CHAT:" & line)
        Else
            peer.SendLine("CHAT:" & line)
        End If
        txtChatInput.Text = ""
    End Sub

    Private Sub AppendChat(msg As String)
        If lstChat Is Nothing Then Return
        lstChat.Items.Add(msg)
        lstChat.TopIndex = Math.Max(0, lstChat.Items.Count - 1)
    End Sub

    ' ============================================================
    '  DIEU KHIEN VAN DAU (HOST)
    ' ============================================================
    Private Sub BtnHostNewRound_Click(sender As Object, e As EventArgs)
        If Not isHost Then Return
        game.StartNewRound()
        secondsLeft = BETTING_SECONDS
        state = RoundState.Betting
        myBetsThisRound.Clear()
        lstMyBets.Items.Clear()

        hub.Broadcast("TX_ROUND:" & game.CurrentRoundNo.ToString() & "|" & secondsLeft.ToString())
        BeginBettingLocal(game.CurrentRoundNo, secondsLeft)

        If countdownTimer Is Nothing Then
            countdownTimer = New Timer()
            countdownTimer.Interval = 1000
            AddHandler countdownTimer.Tick, AddressOf CountdownTimer_Tick
        End If
        countdownTimer.Start()
    End Sub

    Private Sub CountdownTimer_Tick(sender As Object, e As EventArgs)
        secondsLeft -= 1
        lblCountdown.Text = "Còn " & Math.Max(0, secondsLeft).ToString() & " giây"
        If secondsLeft <= 0 Then
            countdownTimer.Stop()
            If isHost Then DoHostRoll()
        End If
    End Sub

    Private Sub BtnHostRoll_Click(sender As Object, e As EventArgs)
        If Not isHost Then Return
        If countdownTimer IsNot Nothing Then countdownTimer.Stop()
        DoHostRoll()
    End Sub

    Private Sub DoHostRoll()
        If Not isHost Then Return
        state = RoundState.Rolling
        btnHostRoll.Enabled = False
        UpdateBetButtonsEnabled()

        Dim dice As Integer() = game.RollDice()
        hub.Broadcast("TX_ROLL:" & dice(0).ToString() & "," & dice(1).ToString() & "," & dice(2).ToString())
        StartDiceAnim(dice)

        ' Tinh thuong/thua ngay (khong can doi animation tren May Host de dam bao dong bo diem),
        ' animation chi la hieu ung hien thi.
        Dim outcomes As List(Of TaiXiuGame.RoundOutcome) = game.ComputePayouts(dice, scoresBySeat)

        Dim sb As New StringBuilder()
        sb.Append(dice(0).ToString()).Append(",").Append(dice(1).ToString()).Append(",").Append(dice(2).ToString()).Append("|")
        Dim first As Boolean = True
        For Each o As TaiXiuGame.RoundOutcome In outcomes
            If Not first Then sb.Append(";")
            first = False
            sb.Append(o.Seat.ToString()).Append(",").Append(CInt(o.Kind).ToString()).Append(",").Append(o.Value.ToString()).Append(",")
            sb.Append(o.Amount.ToString()).Append(",").Append(If(o.Won, "1", "0")).Append(",").Append(o.Payout.ToString()).Append(",").Append(o.NewScore.ToString())
        Next o

        Dim payload As String = sb.ToString()
        Dim barIdx As Integer = payload.IndexOf("|"c)
        hub.Broadcast("TX_RESULT:" & payload)
        ApplyResult(payload.Substring(0, barIdx), payload.Substring(barIdx + 1))
        BroadcastScores()
    End Sub

    Private Sub BeginBettingLocal(roundNo As Integer, secs As Integer)
        state = RoundState.Betting
        secondsLeft = secs
        currentRoundNoLocal = roundNo
        lblRoundInfo.Text = "Ván số " & roundNo.ToString() & " - Đang đặt cược..."
        lblCountdown.Text = "Còn " & secs.ToString() & " giây"
        myBetsThisRound.Clear()
        myBetKindsThisRound.Clear()
        lstMyBets.Items.Clear()
        myHasNumberBet = False
        myHasSideBet = False
        UnhighlightBetButton(mySelectedNumberBtn)
        UnhighlightBetButton(mySelectedSideBtn)
        mySelectedNumberBtn = Nothing
        mySelectedSideBtn = Nothing
        UpdateBetButtonsEnabled()
        If isHost Then btnHostRoll.Enabled = True
    End Sub

    ' ============================================================
    '  DAT CUOC (CLIENT + HOST)
    ' ============================================================
    ''' <summary>True neu kind la 1 trong 2 loai "chon 1 so" (Bo Ba hoac Tong diem).</summary>
    Private Function IsNumberBetKind(kind As TaiXiuGame.BetKind) As Boolean
        Return kind = TaiXiuGame.BetKind.BoBa OrElse kind = TaiXiuGame.BetKind.TongDiem
    End Function

    ''' <summary>True neu kind la 1 trong 2 loai "chon cua" (Tren hoac Duoi).</summary>
    Private Function IsSideBetKind(kind As TaiXiuGame.BetKind) As Boolean
        Return kind = TaiXiuGame.BetKind.Tren OrElse kind = TaiXiuGame.BetKind.Duoi
    End Function

    Private Sub TryPlaceBet(kind As TaiXiuGame.BetKind, value As Integer, clickedBtn As Button)
        If state <> RoundState.Betting Then
            MessageBox.Show("Chưa tới lượt đặt cược. Chờ Host bắt đầu ván mới.")
            Return
        End If

        ' Luat moi: moi van chi duoc chon DUY NHAT 1 so (Bo Ba HOAC Tong diem) va
        ' toi da 1 cua (Tren HOAC Duoi), khong duoc chon nhieu so cung luc.
        If IsNumberBetKind(kind) AndAlso myHasNumberBet Then
            MessageBox.Show("Mỗi ván chỉ được chọn 1 số duy nhất (Bộ Ba hoặc Tổng điểm).")
            Return
        End If
        If IsSideBetKind(kind) AndAlso myHasSideBet Then
            MessageBox.Show("Mỗi ván chỉ được chọn 1 cửa (Trên hoặc Dưới).")
            Return
        End If

        Dim amount As Long = CLng(nudBet.Value)
        Dim mySeat As Integer = If(isHost, 0, localSeat)
        If mySeat < 0 Then Return

        If isHost Then
            ProcessBetFromSeat(0, kind, value, amount)
        Else
            peer.SendLine("TX_BET:" & CInt(kind).ToString() & "|" & value.ToString() & "|" & amount.ToString())
            ' Ghi tam vao danh sach hien thi cuc bo; neu Host tu choi se bao qua TX_BET_FAIL
            AddMyBetDisplay(kind, value, amount)
        End If

        If IsNumberBetKind(kind) Then
            myHasNumberBet = True
            mySelectedNumberBtn = clickedBtn
            HighlightBetButton(clickedBtn)
        End If
        If IsSideBetKind(kind) Then
            myHasSideBet = True
            mySelectedSideBtn = clickedBtn
            HighlightBetButton(clickedBtn)
        End If
        UpdateBetButtonsEnabled()
    End Sub

    ''' <summary>Chi Host goi: kiem tra hop le (bao gom luat "1 so + 1 cua") qua TaiXiuGame.PlaceBet()
    ''' va SeatHasNumberBet/SeatHasSideBet, neu OK thi ghi nhan; neu FAIL bao TX_BET_FAIL.</summary>
    Private Sub ProcessBetFromSeat(seat As Integer, kind As TaiXiuGame.BetKind, value As Integer, amount As Long)
        If IsNumberBetKind(kind) AndAlso SeatHasNumberBet(seat) Then
            RejectBet(seat, "Người chơi này đã chọn 1 số trong ván này rồi.")
            Return
        End If
        If IsSideBetKind(kind) AndAlso SeatHasSideBet(seat) Then
            RejectBet(seat, "Người chơi này đã chọn cửa Trên/Dưới trong ván này rồi.")
            Return
        End If

        Dim currentScore As Long = 0L
        If scoresBySeat.ContainsKey(seat) Then currentScore = scoresBySeat(seat)

        If Not game.PlaceBet(seat, kind, value, amount, currentScore) Then
            RejectBet(seat, "Cược không hợp lệ (kiểm tra số điểm tối thiểu/tối đa hoặc điểm hiện có).")
            Return
        End If

        If seat = 0 Then
            AddMyBetDisplay(kind, value, amount)
        End If
        AppendChat("[Hệ thống] Player " & (seat + 1).ToString() & " đã đặt cược.")
    End Sub

    Private Sub RejectBet(seat As Integer, reason As String)
        If seat = 0 Then
            MessageBox.Show(reason)
        Else
            hub.SendToClient(seat, "TX_BET_FAIL:" & reason)
        End If
    End Sub

    ''' <summary>Kiem tra (theo du lieu chinh thuc tren Host, game.CurrentBets) xem seat da co
    ''' cuoc loai "chon so" (Bo Ba/Tong diem) trong van hien tai chua.</summary>
    Private Function SeatHasNumberBet(seat As Integer) As Boolean
        If Not game.CurrentBets.ContainsKey(seat) Then Return False
        For Each b As TaiXiuGame.BetInfo In game.CurrentBets(seat)
            If IsNumberBetKind(b.Kind) Then Return True
        Next b
        Return False
    End Function

    ''' <summary>Kiem tra xem seat da co cuoc loai "chon cua" (Tren/Duoi) trong van hien tai chua.</summary>
    Private Function SeatHasSideBet(seat As Integer) As Boolean
        If Not game.CurrentBets.ContainsKey(seat) Then Return False
        For Each b As TaiXiuGame.BetInfo In game.CurrentBets(seat)
            If IsSideBetKind(b.Kind) Then Return True
        Next b
        Return False
    End Function

    Private Sub AddMyBetDisplay(kind As TaiXiuGame.BetKind, value As Integer, amount As Long)
        Dim label As String = BetLabel(kind, value) & ": " & amount.ToString() & " điểm"
        myBetsThisRound.Add(label)
        myBetKindsThisRound.Add(kind)
        lstMyBets.Items.Add(label)
    End Sub

    ''' <summary>Goi khi Host tu choi 1 cuoc da hien thi tam thoi phia Client (TX_BET_FAIL):
    ''' xoa dong hien thi cuoi cung va mo lai nut tuong ung de nguoi choi chon lai.</summary>
    Private Sub UndoLastMyBetDisplay()
        If myBetKindsThisRound.Count = 0 Then Return
        Dim lastIdx As Integer = myBetKindsThisRound.Count - 1
        Dim lastKind As TaiXiuGame.BetKind = myBetKindsThisRound(lastIdx)
        myBetKindsThisRound.RemoveAt(lastIdx)
        myBetsThisRound.RemoveAt(lastIdx)
        If lstMyBets.Items.Count > 0 Then lstMyBets.Items.RemoveAt(lstMyBets.Items.Count - 1)

        If IsNumberBetKind(lastKind) Then
            myHasNumberBet = False
            UnhighlightBetButton(mySelectedNumberBtn)
            mySelectedNumberBtn = Nothing
        End If
        If IsSideBetKind(lastKind) Then
            myHasSideBet = False
            UnhighlightBetButton(mySelectedSideBtn)
            mySelectedSideBtn = Nothing
        End If
        UpdateBetButtonsEnabled()
    End Sub

    Private Function BetLabel(kind As TaiXiuGame.BetKind, value As Integer) As String
        Select Case kind
            Case TaiXiuGame.BetKind.Tren : Return "Trên"
            Case TaiXiuGame.BetKind.Duoi : Return "Dưới"
            Case TaiXiuGame.BetKind.BoBa : Return "Bộ Ba " & value.ToString()
            Case TaiXiuGame.BetKind.TongDiem : Return "Tổng " & value.ToString()
        End Select
        Return "?"
    End Function

    Private Sub UpdateBetButtonsEnabled()
        Dim canBet As Boolean = (state = RoundState.Betting)
        Dim canPickSide As Boolean = canBet AndAlso Not myHasSideBet
        Dim canPickNumber As Boolean = canBet AndAlso Not myHasNumberBet

        ' Nut da duoc chon (highlight) van giu Enabled=True de khong bi WinForms ve mau xam mac dinh,
        ' cac nut con lai cung nhom bi khoa (disabled) cho toi khi sang van moi.
        btnBetTren.Enabled = canPickSide OrElse (btnBetTren Is mySelectedSideBtn)
        btnBetDuoi.Enabled = canPickSide OrElse (btnBetDuoi Is mySelectedSideBtn)
        For v As Integer = 1 To 6
            btnBetBoBa(v).Enabled = canPickNumber OrElse (btnBetBoBa(v) Is mySelectedNumberBtn)
        Next v
        For sum As Integer = 4 To 17
            btnBetTong(sum).Enabled = canPickNumber OrElse (btnBetTong(sum) Is mySelectedNumberBtn)
        Next sum
    End Sub

    ' ============================================================
    '  KET QUA
    ' ============================================================
    Private Sub ApplyResult(diceStr As String, entriesStr As String)
        state = RoundState.ShowingResult
        UpdateBetButtonsEnabled()

        Dim diceParts As String() = diceStr.Split(","c)
        Dim diceArr(2) As Integer
        Dim k As Integer
        For k = 0 To 2
            diceArr(k) = Integer.Parse(diceParts(k), CultureInfo.InvariantCulture)
        Next k
        Dim diceSum As Integer = diceArr(0) + diceArr(1) + diceArr(2)

        lblRoundInfo.Text = "Kết quả: " & diceStr.Replace(","c, " - ") & "   (Tổng " & diceSum.ToString() & ")"
        lblCountdown.Text = ""

        Dim myNetPayout As Long = 0
        Dim hasMyOutcome As Boolean = False

        If entriesStr.Trim() <> "" Then
            Dim entries As String() = entriesStr.Split(";"c)
            Dim summaryForMe As New List(Of String)
            Dim mySeat As Integer = If(isHost, 0, localSeat)
            For Each entry As String In entries
                Dim f As String() = entry.Split(","c)
                Dim seat As Integer = Integer.Parse(f(0), CultureInfo.InvariantCulture)
                Dim kind As TaiXiuGame.BetKind = CType(Integer.Parse(f(1), CultureInfo.InvariantCulture), TaiXiuGame.BetKind)
                Dim value As Integer = Integer.Parse(f(2), CultureInfo.InvariantCulture)
                Dim amount As Long = Long.Parse(f(3), CultureInfo.InvariantCulture)
                Dim won As Boolean = (f(4) = "1")
                Dim payout As Long = Long.Parse(f(5), CultureInfo.InvariantCulture)
                Dim newScore As Long = Long.Parse(f(6), CultureInfo.InvariantCulture)

                scoresBySeat(seat) = newScore
                If seat = mySeat Then
                    Dim outcomeText As String = BetLabel(kind, value) & ": " & If(won, "THẮNG +", "thua ") & Math.Abs(payout).ToString()
                    summaryForMe.Add(outcomeText)
                    myNetPayout += payout
                    hasMyOutcome = True
                End If
            Next entry
            If summaryForMe.Count > 0 Then
                AppendChat("--- Kết quả ván của bạn ---")
                For Each t As String In summaryForMe
                    AppendChat(t)
                Next t
                AppendChat("Tổng ván: " & If(myNetPayout >= 0, "+" & myNetPayout.ToString(), myNetPayout.ToString()))
            End If
        End If

        AddRoundHistoryEntry(currentRoundNoLocal, diceArr, diceSum, myNetPayout, hasMyOutcome)
        RefreshPlayerCards()
    End Sub

    ' ============================================================
    '  NAP / TRU DIEM (CHI HOST) - dua tren pattern cua Game777 Form1.vb
    ' ============================================================
    Private Sub AdjustPlayerScore(seat As Integer, isTopUp As Boolean)
        If Not isHost Then Return
        If seat = 0 Then Return

        If Not playerConnected(seat) Then
            MessageBox.Show("Player " & (seat + 1).ToString() & " hiện chưa có ai, không thể chỉnh điểm.")
            Return
        End If

        Dim actionLabel As String = If(isTopUp, "nạp cho", "trừ của")
        Dim promptMsg As String = "Nhập số điểm muốn " & actionLabel & " Player " & (seat + 1).ToString() & " (" & playerNames(seat) & ")." & vbCrLf &
            If(isTopUp, "Số điểm này sẽ được TRỪ trực tiếp từ điểm của bạn (Host).", "Số điểm này sẽ được CỘNG trực tiếp vào điểm của bạn (Host).")
        Dim dialogTitle As String = If(isTopUp, "Nạp điểm cho người chơi", "Trừ điểm của người chơi")
        Dim raw As String = InputBox(promptMsg, dialogTitle, "50")
        If raw Is Nothing OrElse raw.Trim() = "" Then Return

        Dim amount As Long
        If Not Long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, amount) Then
            MessageBox.Show("Số điểm không hợp lệ.")
            Return
        End If
        If amount <= 0 Then
            MessageBox.Show("Số điểm phải lớn hơn 0.")
            Return
        End If

        Dim hostScore As Long = 0
        If scoresBySeat.ContainsKey(0) Then hostScore = scoresBySeat(0)
        Dim targetScore As Long = 0
        If scoresBySeat.ContainsKey(seat) Then targetScore = scoresBySeat(seat)

        Dim sysMsg As String
        If isTopUp Then
            If amount > hostScore Then
                MessageBox.Show("Bạn không đủ điểm để nạp (bạn đang có " & hostScore.ToString() & " điểm).")
                Return
            End If
            scoresBySeat(0) = hostScore - amount
            scoresBySeat(seat) = targetScore + amount
            sysMsg = "Host đã nạp " & amount.ToString() & " điểm cho Player " & (seat + 1).ToString() & " (" & playerNames(seat) & ")."
        Else
            If amount > targetScore Then
                MessageBox.Show("Player " & (seat + 1).ToString() & " không đủ điểm để trừ (hiện có " & targetScore.ToString() & " điểm).")
                Return
            End If
            scoresBySeat(seat) = targetScore - amount
            scoresBySeat(0) = hostScore + amount
            sysMsg = "Host đã trừ " & amount.ToString() & " điểm của Player " & (seat + 1).ToString() & " (" & playerNames(seat) & ")."
        End If

        AppendChat("Hệ thống: " & sysMsg)
        hub.Broadcast("CHAT:Hệ thống:" & sysMsg)
        BroadcastScores()
        RefreshPlayerCards()
    End Sub

    Private Sub RefreshPlayerCards()
        For p As Integer = 0 To 3
            If pnlPlayers(p) Is Nothing Then Continue For
            Dim titleLbl As Label = CType(pnlPlayers(p).Controls("title"), Label)
            Dim suffix As String = ""
            If p = localSeat Then suffix = " (Bạn)"
            titleLbl.Text = "Player " & (p + 1).ToString() & suffix

            If p = 0 OrElse playerConnected(p) Then
                lblCardStatus(p).Text = playerNames(p) & vbCrLf & "Điểm: " & scoresBySeat(p).ToString()
            Else
                lblCardStatus(p).Text = "(Trống)"
            End If
        Next p
    End Sub

    ' ============================================================
    '  ANIMATION QUAY XUC XAC (tai su dung tu app Quay Xuc Xac doc lap)
    ' ============================================================
    Private Sub StartDiceAnim(targetValues As Integer())
        For i As Integer = 0 To 2
            diceAnimTargetValue(i) = targetValues(i)
            diceTumbleIndex(i) = 1
            diceFaceShown(i) = diceAnimRng.Next(1, 7)
            diceStopStep(i) = DICE_ANIM_STEPS + i * 2
            lastDiceResult(i) = targetValues(i)
        Next i
        diceAnimStep = 0

        If diceAnimTimer Is Nothing Then
            diceAnimTimer = New Timer()
            diceAnimTimer.Interval = DICE_ANIM_INTERVAL_MS
            AddHandler diceAnimTimer.Tick, AddressOf DiceAnimTimer_Tick
        End If
        For i As Integer = 0 To 2
            picDice(i).Invalidate()
        Next i
        diceAnimTimer.Start()
    End Sub

    Private Sub DiceAnimTimer_Tick(sender As Object, e As EventArgs)
        diceAnimStep += 1
        Dim allStopped As Boolean = True
        For i As Integer = 0 To 2
            If diceAnimStep >= diceStopStep(i) Then
                diceFaceShown(i) = diceAnimTargetValue(i)
            Else
                diceFaceShown(i) = diceAnimRng.Next(1, 7)
                diceTumbleIndex(i) = ((diceAnimStep - 1) Mod 4) + 1
                allStopped = False
            End If
            picDice(i).Invalidate()
        Next i
        If allStopped Then diceAnimTimer.Stop()
    End Sub

    Private Sub PaintOneDice(g As Graphics, panel As Panel, idx As Integer)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim rect As New Rectangle(0, 0, panel.Width, panel.Height)
        Dim accent As Color = Color.SteelBlue
        Dim isAnimating As Boolean = (diceAnimTimer IsNot Nothing AndAlso diceAnimTimer.Enabled AndAlso diceAnimStep < diceStopStep(idx))

        If isAnimating AndAlso spritesLoaded AndAlso diceTumbleSprite(diceTumbleIndex(idx)) IsNot Nothing Then
            Dim oldTransform As Matrix = g.Transform
            Dim angle As Single = CSng(((diceAnimStep * 53 + idx * 17) Mod 40) - 20)
            Dim cx As Single = rect.Width / 2.0F
            Dim cy As Single = rect.Height / 2.0F
            g.TranslateTransform(cx, cy)
            g.RotateTransform(angle)
            g.TranslateTransform(-cx, -cy)
            g.DrawImage(diceTumbleSprite(diceTumbleIndex(idx)), rect)
            g.Transform = oldTransform
        ElseIf diceFaceShown(idx) >= 1 AndAlso diceFaceShown(idx) <= 6 AndAlso spritesLoaded AndAlso diceSprite(diceFaceShown(idx)) IsNot Nothing Then
            g.DrawImage(diceSprite(diceFaceShown(idx)), rect)
        ElseIf diceFaceShown(idx) >= 1 AndAlso diceFaceShown(idx) <= 6 Then
            Using b As New SolidBrush(Color.White)
                g.FillRectangle(b, rect)
            End Using
            Using pen As New Pen(accent, 3)
                g.DrawRectangle(pen, 1, 1, rect.Width - 2, rect.Height - 2)
            End Using
            Using sf As New StringFormat()
                sf.Alignment = StringAlignment.Center
                sf.LineAlignment = StringAlignment.Center
                Using fnt As New Font("Segoe UI", 22.0!, FontStyle.Bold)
                    Using textBrush As New SolidBrush(accent)
                        g.DrawString(diceFaceShown(idx).ToString(), fnt, textBrush, New RectangleF(rect.X, rect.Y, rect.Width, rect.Height), sf)
                    End Using
                End Using
            End Using
        Else
            Using pen As New Pen(Color.LightGray, 2)
                g.DrawRectangle(pen, 1, 1, rect.Width - 2, rect.Height - 2)
            End Using
        End If
    End Sub

End Class
