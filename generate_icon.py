"""Generate ImeLocker application icon (.ico) with multiple sizes.

Design: "IM" text (top-left) + padlock (bottom-right), overlapping.
Style: clean, tech-oriented, near-white on transparent.
"""

from PIL import Image, ImageDraw, ImageFont
import struct
import io


def draw_lock(draw, ox, oy, size, color, outline_width):
    """Draw a padlock at (ox, oy) with given size."""
    body_h = size * 0.55
    body_w = size * 0.85
    body_x = ox + (size - body_w) / 2
    body_y = oy + size - body_h
    r = size * 0.08

    # Shackle arc
    shackle_w = size * 0.48
    shackle_h = size * 0.50
    shackle_x = ox + (size - shackle_w) / 2
    shackle_y = body_y - shackle_h / 2
    draw.arc(
        [shackle_x, shackle_y, shackle_x + shackle_w, shackle_y + shackle_h],
        180, 360,
        fill=color,
        width=max(2, int(outline_width * 1.2)),
    )

    # Lock body
    draw.rounded_rectangle(
        [body_x, body_y, body_x + body_w, body_y + body_h],
        radius=r,
        fill=color,
    )

    # Keyhole
    kh_r = size * 0.08
    kh_cx = body_x + body_w / 2
    kh_cy = body_y + body_h * 0.38
    draw.ellipse(
        [kh_cx - kh_r, kh_cy - kh_r, kh_cx + kh_r, kh_cy + kh_r],
        fill=(30, 30, 30),
    )


def create_icon_image(size):
    """Create a single icon image at the given size."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    color = (255, 255, 255, 255)

    # "IM" text — large and bold, anchored top-left
    font_size = int(size * 0.72)
    font_paths = [
        "/usr/share/fonts/truetype/Lato-Heavy.ttf",
        "/usr/share/fonts/truetype/Lato-Bold.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ]
    font = None
    for fp in font_paths:
        try:
            font = ImageFont.truetype(fp, font_size)
            break
        except (OSError, IOError):
            continue
    if font is None:
        font = ImageFont.load_default()

    text = "IM"
    bbox = draw.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]

    tx = (size - tw) / 2 - size * 0.10
    ty = (size - th) / 2 - size * 0.16
    draw.text((tx, ty), text, fill=color, font=font)

    # Lock overlay at bottom-right, overlapping text
    lock_size = int(size * 0.50)
    lock_x = size - lock_size
    lock_y = size - lock_size
    outline_w = max(2, int(size * 0.06))
    draw_lock(draw, lock_x, lock_y, lock_size, color, outline_w)

    return img


def build_ico(output_path, sizes=(16, 20, 24, 32, 48, 64, 128, 256)):
    """Build a multi-size .ico file manually (ICO format spec)."""
    png_data_list = []
    for s in sizes:
        img = create_icon_image(s)
        buf = io.BytesIO()
        img.save(buf, format="PNG")
        png_data_list.append(buf.getvalue())

    num = len(sizes)
    # ICO header: reserved(2) + type(2) + count(2) = 6 bytes
    header = struct.pack("<HHH", 0, 1, num)

    # Each directory entry: 16 bytes
    dir_entries = b""
    # Data starts after header + all directory entries
    data_offset = 6 + 16 * num

    for i, s in enumerate(sizes):
        png_bytes = png_data_list[i]
        # Width/height: 0 means 256
        w = 0 if s >= 256 else s
        h = 0 if s >= 256 else s
        entry = struct.pack(
            "<BBBBHHII",
            w,            # width
            h,            # height
            0,            # color palette
            0,            # reserved
            1,            # color planes
            32,           # bits per pixel
            len(png_bytes),  # image data size
            data_offset,  # offset to image data
        )
        dir_entries += entry
        data_offset += len(png_bytes)

    with open(output_path, "wb") as f:
        f.write(header)
        f.write(dir_entries)
        for png_bytes in png_data_list:
            f.write(png_bytes)

    total = sum(len(d) for d in png_data_list)
    print(f"Generated {output_path} ({6 + 16*num + total} bytes) with sizes: {sizes}")


if __name__ == "__main__":
    build_ico("src/ImeLocker/app.ico")
