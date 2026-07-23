from PIL import Image, ImageDraw, ImageFont
import os

out = r"c:\Users\dev\Projects\track\docs\audit"
os.makedirs(out, exist_ok=True)

try:
    f_title = ImageFont.truetype("segoeui.ttf", 22)
    f_h = ImageFont.truetype("segoeuib.ttf", 18)
    f = ImageFont.truetype("segoeui.ttf", 13)
    f_sm = ImageFont.truetype("segoeui.ttf", 11)
except OSError:
    f_title = f_h = f = f_sm = ImageFont.load_default()

# Settings 720x520
W, H = 720, 520
img = Image.new("RGB", (W, H), "#F7F5F2")
d = ImageDraw.Draw(img)
d.rectangle((0, 0, 200, H), fill="#1C1917")
d.text((16, 16), "Track", fill="white", font=f_h)
d.text((16, 60), "Connections", fill="white", font=f)
d.text((16, 96), "General", fill="#A8A29E", font=f)
d.text((16, 140), "Alerts & budgets in Phase 2", fill="#78716C", font=f_sm)
d.text((224, 24), "Connections", fill="#1C1917", font=f_title)
d.text((224, 56), "Cursor links via your local Cursor login (no API key).", fill="#78716C", font=f_sm)
d.text((224, 72), "OpenAI/Claude Admin keys come in Phase 2.", fill="#78716C", font=f_sm)


def card(y, name, status, btn):
    d.rounded_rectangle((224, y, 696, y + 78), radius=10, fill="white", outline="#E7E5E4")
    d.text((238, y + 14), name, fill="#1C1917", font=f_h)
    d.text((238, y + 42), status, fill="#78716C", font=f_sm)
    d.rounded_rectangle((600, y + 24, 680, y + 52), radius=4, fill="#F7F5F2", outline="#E7E5E4")
    tw = d.textlength(btn, font=f_sm)
    d.text((640 - tw / 2, y + 30), btn, fill="#1C1917", font=f_sm)


card(100, "Cursor", "Subscription · Ok — limit reached", "Refresh")
card(190, "OpenAI API", "API cost · Not linked — Admin key in Phase 2", "Connect")
card(280, "Claude API", "API cost · Not linked — Admin key in Phase 2", "Connect")
settings_path = os.path.join(out, "settings.png")
img.save(settings_path)
print("settings", os.path.getsize(settings_path))

# Flyout 380x460
W2, H2 = 380, 460
fly = Image.new("RGB", (W2, H2), "white")
d2 = ImageDraw.Draw(fly)
d2.rectangle((0, 0, W2 - 1, H2 - 1), outline="#E7E5E4")
d2.text((16, 16), "Track", fill="#1C1917", font=f_h)
d2.text((250, 20), "Updated 4:52 PM", fill="#78716C", font=f_sm)
d2.ellipse((16, 52, 28, 64), outline="#0F766E", width=2)
d2.text((34, 50), "Monthly", fill="#1C1917", font=f)
d2.ellipse((110, 52, 122, 64), outline="#A8A29E")
d2.text((128, 50), "Weekly", fill="#78716C", font=f)
d2.rounded_rectangle((16, 80, 364, 400), radius=8, fill="#F7F5F2")
d2.text((28, 92), "Cursor", fill="#1C1917", font=f_h)
d2.text((320, 96), "Pro", fill="#78716C", font=f_sm)
d2.text((28, 124), "Total: $0.00 left ($20/$20) · over pace", fill="#1C1917", font=f)
d2.text((28, 146), "Recommended 35% · actual 100% (+65%)", fill="#78716C", font=f_sm)

cx, cy, cw, ch = 28, 180, 320, 140
d2.rectangle((cx, cy, cx + cw, cy + ch), fill="white", outline="#E7E5E4")
for pct in (0, 50, 100):
    y = cy + ch - int(ch * pct / 100)
    d2.line((cx, y, cx + cw, y), fill="#E7E5E4")
for i in range(0, cw, 8):
    x1 = cx + i
    y1 = cy + ch - int(ch * i / cw)
    x2 = cx + min(i + 4, cw)
    y2 = cy + ch - int(ch * min(i + 4, cw) / cw)
    d2.line((x1, y1, x2, y2), fill="#A8A29E")
ax = cx + int(cw * 0.35)
d2.line((cx, cy + ch, ax, cy), fill="#C2410C", width=3)
d2.ellipse((ax - 4, cy - 4, ax + 4, cy + 4), fill="#C2410C")
d2.text((cx + 4, cy + 4), "recommended ->", fill="#A8A29E", font=f_sm)
d2.text((28, 332), "Auto: 85% left · Under pace", fill="#1C1917", font=f)
d2.text((28, 354), "API: 100% left · Under pace", fill="#1C1917", font=f)
d2.text((28, 376), "Bonus spend: $25.73", fill="#78716C", font=f_sm)
d2.text((16, 430), "Dashed = recommended. Solid = actual.", fill="#78716C", font=f_sm)

flyout_path = os.path.join(out, "flyout.png")
fly.save(flyout_path)
print("flyout", os.path.getsize(flyout_path))
