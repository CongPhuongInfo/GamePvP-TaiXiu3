# Tài Xỉu 3 Xúc Xắc

Trò chơi đặt cược 3 xúc xắc nhiều người chơi qua mạng LAN hoặc Online.
Mỗi ván, người chơi chọn tối đa 1 số (Bộ Ba hoặc Tổng điểm) và 1 cửa
(Trên/Dưới), Host tung 3 xúc xắc — trúng thì nhân hệ số, trật thì mất
cược. Kiến trúc star-topology (NetworkHub.vb / NetworkPeer.vb), Host
điều phối toàn bộ ván đấu và tính điểm.

# Các tính năng chính
- Hỗ trợ tối đa 4 người chơi (1 Host + 3 Client) cùng lúc qua mạng LAN/Online
- Mỗi ván chỉ được chọn **1 số duy nhất** (Bộ Ba hoặc Tổng điểm) + tối đa
  **1 cửa** (Trên hoặc Dưới) — không cho đặt tràn lan nhiều số cùng lúc
- Luật Bộ Ba chuẩn: nếu ra 3 xúc xắc giống nhau, cửa Trên/Dưới **luôn thua**
- Mỗi người bắt đầu với **1000 điểm**, đặt cược **10–500 điểm/lần bấm**
- Animation quay 3 xúc xắc lệch nhịp tự nhiên, không dừng đồng loạt
- Host có thể **nạp/trừ điểm** trực tiếp cho từng người chơi (chuột phải vào thẻ người chơi)
- Sprite xúc xắc + khung trang trí (nền gỗ, khung Trên/Dưới, Bộ Ba, rương,
  icon xu, ruy băng kết thúc) từ thư mục Assets (tự fallback vẽ khối/màu
  phẳng nếu thiếu ảnh)

# Bảng cược & hệ số thưởng
| Loại cược | Hệ số | Ghi chú |
|-----------|-------|---------|
| Trên (tổng 11–17) | ×1 | Thua nếu ra Bộ Ba |
| Dưới (tổng 4–10) | ×1 | Thua nếu ra Bộ Ba |
| Bộ Ba (1-1-1 .. 6-6-6) | ×150 | Cả 3 xúc xắc cùng ra 1 số |
| Tổng điểm 4 hoặc 17 | ×50 | |
| Tổng điểm 5 hoặc 16 | ×18 | |
| Tổng điểm 6 hoặc 15 | ×14 | |
| Tổng điểm 7 hoặc 14 | ×12 | |
| Tổng điểm 8 hoặc 13 | ×8 | |
| Tổng điểm 9 hoặc 12 | ×7 | |
| Tổng điểm 10 hoặc 11 | ×6 | |

> Hệ số Tổng điểm đối xứng qua giữa 10–11, đúng theo bảng cược gốc.

# Cách tính thắng/thua
| Kết quả | Điểm nhận |
|---------|-----------|
| Trúng cửa Trên/Dưới (không phải Bộ Ba) | +Cược × 1 |
| Trúng Bộ Ba | +Cược × 150 |
| Trúng Tổng điểm | +Cược × hệ số tương ứng |
| Trật | -Cược |

# Cách build
Yêu cầu: **.NET Framework 4.x** đã cài sẵn trên Windows.

```
build.bat
```

File `.exe` xuất ra cùng thư mục với tên `TaiXiu3XucXac.exe`.

> Đặt thư mục `Assets/` (chứa `dice_1.png`..`dice_6.png`, `dice_roll_1.png`..
> `dice_roll_4.png`, `bg_wood_table.png`, `frame_tren.png`, `frame_duoi.png`,
> `frame_boba.png`, `coin_icon.png`, `chest_icon.png`, `banner_ended.png`)
> cạnh file `.exe` để hiển thị đầy đủ sprite/khung trang trí.

# Cách chơi

**Host (tạo phòng):**
1. Chọn **Tạo phòng (Host)** → nhập port (mặc định `9051`) → bấm Host
2. Chờ người chơi khác vào phòng
3. Bấm **Bắt đầu ván mới (Host)** để mở giờ đặt cược (20 giây)
4. Bấm **Tung xúc xắc ngay (Host)** để kết thúc sớm, hoặc đợi hết giờ tự động tung

**Client (vào phòng):**
1. Chọn **Vào phòng (Join)** → nhập IP của Host và port → bấm Join
2. Nhập số điểm cược, chọn 1 số (Bộ Ba hoặc Tổng điểm) và/hoặc 1 cửa (Trên/Dưới)
3. Chờ Host tung xúc xắc và xem kết quả trong khung chat

# Cấu trúc file
| File | Vai trò |
|------|---------|
| `TaiXiuGame.vb` | Logic game: đặt cược, tung xúc xắc, tính thưởng theo bảng hệ số |
| `Form1.vb` | Giao diện, giao thức mạng, animation xúc xắc, đặt cược, nạp/trừ điểm |
| `NetworkHub.vb` | Phía Host: quản lý nhiều kết nối Client (star-topology) |
| `NetworkPeer.vb` | Phía Client: kết nối đến Host |
| `Program.vb` | Entry point |
| `build.bat` | Script build bằng vbc.exe |
