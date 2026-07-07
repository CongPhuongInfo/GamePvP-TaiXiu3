# Tài Xỉu 3 Xúc Xắc

Game đặt cược 3 xúc xắc trên mạng LAN/Online, chuyển thể từ kiến trúc mạng và
cách quản lý điểm người chơi của **Game777** (star-topology, host-authoritative),
kết hợp với animation quay xúc xắc đã tách riêng trước đó.

## Cách chơi (giống bảng cược trong ảnh mẫu)
Mỗi ván, người chơi có thể đặt **nhiều loại cược cùng lúc**:

| Loại cược | Hệ số | Ghi chú |
|---|---|---|
| Trên (tổng 11–17) | x1 | Thua nếu ra Bộ Ba |
| Dưới (tổng 4–10) | x1 | Thua nếu ra Bộ Ba |
| Bộ Ba (1-1-1 .. 6-6-6) | x150 | Cả 3 xúc xắc cùng ra 1 số |
| Tổng điểm 4–17 | x50/x18/x14/x12/x8/x7/x6 (đối xứng qua 10-11) | Trúng đúng tổng |

Luật Bộ Ba: nếu 3 xúc xắc ra cùng 1 số, cược Trên/Dưới luôn thua (nhà cái ăn),
đúng theo luật Tài Xỉu/Sicbo tiêu chuẩn.

## Kiến trúc tái sử dụng
- `NetworkHub.vb` / `NetworkPeer.vb`: **giữ nguyên**, không sửa gì so với Game777.
- `TaiXiuGame.vb`: logic thuần (không dính UI) — `PlaceBet`, `RollDice`,
  `ComputePayouts`. Khác Game777Game ở chỗ 1 seat có thể có nhiều `BetInfo`
  trong cùng 1 van (thay vì chỉ 1 cược/ván).
- `Form1.vb`: giao thức mạng (`TX_HELLO`, `TX_WELCOME`, `TX_ROUND`, `TX_BET`,
  `TX_ROLL`, `TX_RESULT`...), quản lý điểm (`scoresBySeat`), UI đặt cược +
  animation 3 xúc xắc, và menu chuột phải Host nạp/trừ điểm người chơi
  (copy nguyên logic `AdjustPlayerScore` từ Game777).

## Build
Chạy `build.bat` (dùng `vbc.exe` .NET Framework 4.x, không cần Visual Studio).
Copy thư mục `Assets/` (đã có sẵn 10 sprite xúc xắc) cạnh file .exe để hiển thị
hình thật thay vì fallback vẽ số.

## Có thể mở rộng thêm
- Thêm lịch sử các ván gần đây (giống `spinHistory` trong Game777).
- Thêm quỹ Jackpot cho Bộ Ba hiếm giống cơ chế Nổ Hũ.
- Vẽ UI đẹp hơn theo đúng bố cục ảnh mẫu (khung "Trên/Dưới" bên trái, rương ở
  giữa, lưới hệ số bên dưới) — hiện tại đang dùng layout dạng nút bấm đơn giản
  để tập trung vào đúng luật chơi + mạng + điểm trước.
